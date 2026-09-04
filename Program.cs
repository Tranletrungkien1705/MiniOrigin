using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Serilog;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
FleetObs.ConfigureLogger("miniorigin");

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

var conn = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=miniorigin.db";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (DbUtil.IsPostgres(conn)) o.UseNpgsql(DbUtil.ToNpgsql(conn));
    else o.UseSqlite(conn);
});
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IOriginService, OriginService>();
builder.Services.AddFleetObs();
builder.Services.AddControllersWithViews();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await Seeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

app.UseFleetObs();
FleetObs.ReportLicense(Environment.GetEnvironmentVariable("SSO_AUTHORITY") ?? "https://minisso.onrender.com", "miniorigin");

app.Use(async (ctx, next) =>
{
    var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(key)) ctx.Request.Cookies.TryGetValue(TenantContext.CookieName, out key);
    if (!string.IsNullOrWhiteSpace(key))
    {
        using var lookup = app.Services.CreateScope();
        var ldb = lookup.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = await ldb.Orgs.FirstOrDefaultAsync(o => o.ApiKey == key);
        if (org != null) ctx.RequestServices.GetRequiredService<ITenantContext>().OrgId = org.Id;
    }
    await next();
});

app.UseStaticFiles();
app.MapGet("/healthz", () => "ok");
app.MapGet("/api/summary", async (IOriginService svc) =>
{
    var d = await svc.DashboardAsync();
    return Results.Ok(new { lots = d.Lots, events = d.Events, ctes = d.Ctes, glns = d.Glns });
});

// Tra cứu nguồn gốc công khai theo mã lô (xuyên tenant).
app.MapGet("/api/trace/{code}", async (string code, IOriginService svc) =>
{
    var t = await svc.TraceByCodeAsync(code);
    if (t == null) return Results.NotFound(new { error = "Không tìm thấy mã lô." });
    object Map(LotTrace x) => new
    {
        code = x.Lot.Code, product = x.Lot.ProductName, origin = x.Lot.OriginGln?.Name,
        events = x.Events.Select(e => new { e.Ev.CteName, when = e.Ev.EventTime, at = e.Ev.GlnName, e.Ev.Operator, kde = e.Kdes.Select(k => new { k.Label, k.Value, k.Unit }) }),
        parents = x.Parents.Select(Map)
    };
    return Results.Ok(Map(t));
});

app.MapPost("/api/orgs/register", async (RegisterOrgDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest(new { error = "Cần Name." });
    var org = new Org { Name = dto.Name.Trim(), ApiKey = "origin_" + Guid.NewGuid().ToString("N") };
    db.Orgs.Add(org); await db.SaveChangesAsync();
    return Results.Ok(new { orgId = org.Id, apiKey = org.ApiKey });
});

// Import địa điểm GLN thật từ Mst_Dealer (dedupe theo Code)
app.MapPost("/api/import/glns", async (List<ImportGlnDto> rows, AppDbContext db, ITenantContext tc) =>
{
    if (rows == null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu." });
    int added = 0, skipped = 0;
    var orgId = tc.OrgId;
    var existCodes = db.Glns.Where(g => g.OrgId == orgId).Select(g => g.Code).ToHashSet();
    foreach (var row in rows)
    {
        if (string.IsNullOrWhiteSpace(row.Code)) { skipped++; continue; }
        if (existCodes.Contains(row.Code.Trim())) { skipped++; continue; }
        db.Glns.Add(new Gln { OrgId = orgId, Code = row.Code.Trim(), Name = row.Name?.Trim() ?? row.Code.Trim(), Type = GlnType.Store, Address = row.Address });
        existCodes.Add(row.Code.Trim()); added++;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { added, skipped, total = added + skipped });
});

// Import sản phẩm truy xuất từ Mst_CarModel (dedupe theo Code)
app.MapPost("/api/import/products", async (List<ImportOriginProdDto> rows, AppDbContext db, ITenantContext tc) =>
{
    if (rows == null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu." });
    int added = 0, skipped = 0;
    var orgId = tc.OrgId;
    var existCodes = db.Products.Where(p => p.OrgId == orgId).Select(p => p.Code).ToHashSet();
    foreach (var row in rows)
    {
        if (string.IsNullOrWhiteSpace(row.Code)) { skipped++; continue; }
        if (existCodes.Contains(row.Code.Trim())) { skipped++; continue; }
        db.Products.Add(new Product { OrgId = orgId, Code = row.Code.Trim(), Name = row.Name?.Trim() ?? row.Code.Trim(), Unit = row.Unit });
        existCodes.Add(row.Code.Trim()); added++;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { added, skipped, total = added + skipped });
});

// Import lô hàng thật từ Car_VIN (dedupe theo Code, lookup Product+GLN theo code)
app.MapPost("/api/import/lots", async (List<ImportLotDto> rows, AppDbContext db, ITenantContext tc) =>
{
    if (rows == null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu." });
    int added = 0, skipped = 0;
    var orgId = tc.OrgId;
    var existCodes = db.Lots.Where(l => l.OrgId == orgId).Select(l => l.Code).ToHashSet();
    foreach (var row in rows)
    {
        if (string.IsNullOrWhiteSpace(row.Code)) { skipped++; continue; }
        var code = row.Code.Trim();
        if (existCodes.Contains(code)) { skipped++; continue; }
        int? prodId = null; string prodName = row.ProductName ?? code;
        if (!string.IsNullOrWhiteSpace(row.ProductCode))
        {
            var prod = db.Products.FirstOrDefault(p => p.OrgId == orgId && p.Code == row.ProductCode.Trim());
            if (prod != null) { prodId = prod.Id; prodName = prod.Name; }
        }
        int? glnId = null;
        if (!string.IsNullOrWhiteSpace(row.GlnCode))
        {
            var gln = db.Glns.FirstOrDefault(g => g.OrgId == orgId && g.Code == row.GlnCode.Trim());
            glnId = gln?.Id;
        }
        db.Lots.Add(new Lot { OrgId = orgId, Code = code, ProductId = prodId, ProductName = prodName, OriginGlnId = glnId, Quantity = 1, Unit = "chiếc", Status = LotStatus.Shipped });
        existCodes.Add(code); added++;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { added, skipped, total = added + skipped });
});

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

record RegisterOrgDto(string Name);
record ImportGlnDto(string? Code, string? Name, string? Address);
record ImportOriginProdDto(string? Code, string? Name, string? Unit);
record ImportLotDto(string? Code, string? ProductCode, string? ProductName, string? GlnCode);
