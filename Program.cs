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
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<IProductColorService, ProductColorService>();
builder.Services.AddScoped<IAuthenticityService, AuthenticityService>();
builder.Services.AddScoped<IVerifyBatchService, VerifyBatchService>();
builder.Services.AddScoped<IPackingService, PackingService>();
builder.Services.AddScoped<ISearchLogService, SearchLogService>();
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
app.MapGet("/api/trace/{code}", async (string code, IOriginService svc, ISearchLogService logs) =>
{
    var t = await svc.TraceByCodeAsync(code);
    await logs.RecordAsync(code, SearchType.Trace, t != null);
    if (t == null) return Results.NotFound(new { error = "Không tìm thấy mã lô." });
    object Map(LotTrace x) => new
    {
        code = x.Lot.Code, product = x.Lot.ProductName, origin = x.Lot.OriginGln?.Name,
        events = x.Events.Select(e => new { e.Ev.CteName, when = e.Ev.EventTime, at = e.Ev.GlnName, e.Ev.Operator, kde = e.Kdes.Select(k => new { k.Label, k.Value, k.Unit }) }),
        parents = x.Parents.Select(Map)
    };
    return Results.Ok(Map(t));
});

// Danh mục Thương hiệu (nguồn gốc thương hiệu) — port từ Mst_Brand của InBrand.
app.MapGet("/api/brands", async (string? q, bool? active, IBrandService svc) =>
    Results.Ok((await svc.ListAsync(q, active)).Select(v => new
    {
        v.Brand.Id, v.Brand.Code, v.Brand.Name, v.Brand.Active, v.ProductCount
    })));

app.MapPost("/api/brands", async (BrandReq r, IBrandService svc) =>
{
    var (ok, msg, id) = await svc.CreateAsync(r.Code ?? "", r.Name ?? "", r.Active);
    return ok ? Results.Ok(new { id }) : Results.BadRequest(new { error = msg });
});

app.MapPut("/api/brands/{id:int}", async (int id, BrandReq r, IBrandService svc) =>
{
    var (ok, msg) = await svc.UpdateAsync(id, r.Name ?? "", r.Active);
    return ok ? Results.Ok(new { ok }) : Results.BadRequest(new { error = msg });
});

app.MapDelete("/api/brands/{id:int}", async (int id, IBrandService svc) =>
{
    var (ok, msg) = await svc.DeleteAsync(id);
    return ok ? Results.Ok(new { ok }) : Results.BadRequest(new { error = msg });
});

// Danh mục Màu sắc sản phẩm (nguồn gốc thương hiệu) — port từ Mst_PartColor của InBrand.
app.MapGet("/api/colors", async (string? q, bool? active, IProductColorService svc) =>
    Results.Ok((await svc.ListColorsAsync(q, active)).Select(v => new
    {
        v.Color.Id, v.Color.Code, v.Color.Name, v.Color.NameVn, v.Color.Active, v.ProductCount
    })));

app.MapPost("/api/colors", async (ColorReq r, IProductColorService svc) =>
{
    var (ok, msg, id) = await svc.CreateColorAsync(r.Code ?? "", r.Name ?? "", r.NameVn ?? "", r.Active);
    return ok ? Results.Ok(new { id }) : Results.BadRequest(new { error = msg });
});

app.MapPut("/api/colors/{id:int}", async (int id, ColorReq r, IProductColorService svc) =>
{
    var (ok, msg) = await svc.UpdateColorAsync(id, r.Name ?? "", r.NameVn ?? "", r.Active);
    return ok ? Results.Ok(new { ok }) : Results.BadRequest(new { error = msg });
});

app.MapDelete("/api/colors/{id:int}", async (int id, IProductColorService svc) =>
{
    var (ok, msg) = await svc.DeleteColorAsync(id);
    return ok ? Results.Ok(new { ok }) : Results.BadRequest(new { error = msg });
});

// Gán màu cho sản phẩm — port từ Mst_MapPartColor của InBrand.
// Luật: mỗi sản phẩm chỉ có tối đa 1 màu mặc định.
app.MapGet("/api/color-maps", async (int? productId, string? q, IProductColorService svc) =>
    Results.Ok((await svc.ListMapsAsync(productId, q)).Select(v => new
    {
        v.Map.Id, v.Map.ProductId, productName = v.ProductName, v.Map.ColorId,
        colorName = v.ColorName, colorNameVn = v.ColorNameVn, v.Map.IsDefault, v.Map.Active
    })));

app.MapPost("/api/color-maps", async (ColorMapReq r, IProductColorService svc) =>
{
    var (ok, msg, id) = await svc.AssignAsync(r.ProductId, r.ColorId, r.IsDefault);
    return ok ? Results.Ok(new { id }) : Results.BadRequest(new { error = msg });
});

app.MapPut("/api/color-maps/{id:int}/default", async (int id, ColorMapDefaultReq r, IProductColorService svc) =>
{
    var (ok, msg) = await svc.SetMapDefaultAsync(id, r.IsDefault);
    return ok ? Results.Ok(new { ok }) : Results.BadRequest(new { error = msg });
});

app.MapDelete("/api/color-maps/{id:int}", async (int id, IProductColorService svc) =>
{
    var (ok, msg) = await svc.RemoveMapAsync(id);
    return ok ? Results.Ok(new { ok }) : Results.BadRequest(new { error = msg });
});

// Xác thực sản phẩm chính hãng (nguồn gốc thương hiệu) — port từ module BrandPositioning của InBrand.
// Tra cứu công khai theo serial (không lộ mã bí mật).
app.MapGet("/api/authenticity/{serial}", async (string serial, IAuthenticityService svc, ISearchLogService logs) =>
{
    var u = await svc.LookupAsync(serial);
    await logs.RecordAsync(serial, SearchType.Authenticity, u != null);
    return u == null ? Results.NotFound(new { error = "Không tìm thấy sản phẩm." }) : Results.Ok(u);
});

// Xác thực cặp Serial + mã bí mật (PIN).
app.MapPost("/api/authenticity/verify", async (VerifyReq r, IAuthenticityService svc) =>
{
    var res = await svc.VerifyAsync(r.Serial ?? "", r.Pin ?? "");
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
});

// Kích hoạt bảo hành (lần xác thực đầu) + ghi thông tin khách hàng.
app.MapPost("/api/authenticity/activate", async (ActivateReq r, IAuthenticityService svc) =>
{
    var res = await svc.ActivateAsync(r.Serial ?? "", r.Pin ?? "", r.CustomerName, r.Phone, r.Address);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
});

// Danh sách đơn vị sản phẩm (admin).
app.MapGet("/api/units", async (string? q, IAuthenticityService svc) =>
    Results.Ok((await svc.ListAsync(q)).Select(u => new
    {
        u.Id, u.SerialNo, u.ProductName, brand = u.Brand?.Name, u.LotCode, u.Origin,
        u.Activated, u.WarrantyDateStart, u.WarrantyMonths, u.VerifyCount
    })));

app.MapPost("/api/units", async (UnitReq r, IAuthenticityService svc) =>
{
    var (ok, msg, id) = await svc.CreateAsync(r.Serial ?? "", r.Pin ?? "", r.ProductId, r.BrandId, r.LotCode, r.Origin, r.WarrantyMonths);
    return ok ? Results.Ok(new { id }) : Results.BadRequest(new { error = msg });
});

// Lần xuất ghép (batch xác thực) — port từ Inv_VerifiedIDInOut của InBrand.
// Gom nhiều tem (IDNo) đã xác thực để ghép với 1 đơn hàng/phiếu xuất.
app.MapGet("/api/verify-batches", async (string? q, IVerifyBatchService svc) =>
    Results.Ok((await svc.ListAsync(q)).Select(s => new
    {
        s.Batch.Id, s.Batch.Code, s.Batch.ProductName, s.Batch.RefNo, s.Batch.RefNoSys,
        s.Batch.PlateNo, s.Batch.ReceivePlace, s.Batch.QtyPlan, s.Batch.QtyInit, s.Batch.QtyVerified,
        status = s.Batch.Status.ToString(), s.QtyOK, s.QtyNG, s.QtyRemain, s.Batch.CreatedAt
    })));

app.MapGet("/api/verify-batches/{id:int}", async (int id, IVerifyBatchService svc) =>
{
    var s = await svc.GetAsync(id);
    if (s == null) return Results.NotFound(new { error = "Không tìm thấy lần xuất ghép." });
    return Results.Ok(new
    {
        s.Batch.Id, s.Batch.Code, s.Batch.ProductName, s.Batch.RefNo, s.Batch.RefNoSys,
        s.Batch.TransportType, s.Batch.PlateNo, s.Batch.ReceivePlace, s.Batch.InvOutType,
        s.Batch.QtyPlan, s.Batch.QtyInit, s.Batch.QtyVerified, status = s.Batch.Status.ToString(),
        s.QtyOK, s.QtyNG, s.QtyRemain,
        items = s.Batch.Items.Select(i => new { i.IdNo, i.ProductName, i.BoxNo, i.CustomerName, i.FlagNG, i.ErrorReason, i.ScannedAt })
    });
});

app.MapPost("/api/verify-batches", async (VerifyBatchReq r, IVerifyBatchService svc) =>
{
    var (ok, msg, id) = await svc.CreateAsync(r.ProductName ?? "", r.RefNo, r.RefNoSys, r.TransportType, r.PlateNo, r.ReceivePlace, r.InvOutType, r.QtyPlan);
    return ok ? Results.Ok(new { id }) : Results.BadRequest(new { error = msg });
});

// Quét 1 tem vào lần ghép (tem lỗi vẫn ghi nhận nhưng không tính vào số ghép được).
app.MapPost("/api/verify-batches/{id:int}/scan", async (int id, ScanReq r, IVerifyBatchService svc) =>
{
    var (ok, msg, isNG) = await svc.ScanAsync(id, r.IdNo ?? "", r.Pin, r.BoxNo, r.CustomerName);
    return ok ? Results.Ok(new { ok, isNG, message = msg }) : Results.BadRequest(new { error = msg });
});

app.MapPost("/api/verify-batches/{id:int}/merge", async (int id, IVerifyBatchService svc) =>
{
    var (ok, msg) = await svc.MergeAsync(id);
    return ok ? Results.Ok(new { ok, message = msg }) : Results.BadRequest(new { error = msg });
});

app.MapPost("/api/verify-batches/{id:int}/cancel", async (int id, IVerifyBatchService svc) =>
{
    var (ok, msg) = await svc.CancelAsync(id);
    return ok ? Results.Ok(new { ok, message = msg }) : Results.BadRequest(new { error = msg });
});

// Đóng hộp / Đóng thùng (packing) — port từ module Box/Can của InBrand.
// Tra cứu công khai theo mã hộp: trả về danh sách serial bên trong.
app.MapGet("/api/boxes/lookup/{code}", async (string code, IPackingService svc, ISearchLogService logs) =>
{
    var b = await svc.LookupBoxAsync(code);
    await logs.RecordAsync(code, SearchType.Box, b != null);
    return b == null ? Results.NotFound(new { error = "Không tìm thấy mã hộp." }) : Results.Ok(b);
});

app.MapGet("/api/boxes", async (string? q, IPackingService svc) =>
    Results.Ok((await svc.ListBoxesAsync(q)).Select(s => new
    {
        s.Box.Id, s.Box.Code, s.Box.SecretNo, s.Box.Remark, canCode = s.CanCode, s.ItemCount, s.Box.CreatedAt
    })));

app.MapGet("/api/boxes/{id:int}", async (int id, IPackingService svc) =>
{
    var s = await svc.GetBoxAsync(id);
    if (s == null) return Results.NotFound(new { error = "Không tìm thấy hộp." });
    return Results.Ok(new
    {
        s.Box.Id, s.Box.Code, s.Box.SecretNo, s.Box.Remark, canCode = s.CanCode, s.ItemCount,
        items = s.Box.Items.Select(i => new { i.SerialNo, i.ProductName, i.PackedAt })
    });
});

app.MapPost("/api/boxes", async (BoxReq r, IPackingService svc) =>
{
    var (ok, msg, id) = await svc.CreateBoxAsync(r.Code ?? "", r.SecretNo, r.Remark);
    return ok ? Results.Ok(new { id }) : Results.BadRequest(new { error = msg });
});

app.MapPost("/api/boxes/{id:int}/pack", async (int id, PackSerialReq r, IPackingService svc) =>
{
    var (ok, msg) = await svc.PackSerialAsync(id, r.SerialNo ?? "");
    return ok ? Results.Ok(new { ok, message = msg }) : Results.BadRequest(new { error = msg });
});

app.MapPost("/api/boxes/{id:int}/unpack", async (int id, PackSerialReq r, IPackingService svc) =>
{
    var (ok, msg) = await svc.UnpackSerialAsync(id, r.SerialNo ?? "");
    return ok ? Results.Ok(new { ok, message = msg }) : Results.BadRequest(new { error = msg });
});

app.MapGet("/api/cans", async (string? q, IPackingService svc) =>
    Results.Ok((await svc.ListCansAsync(q)).Select(s => new
    {
        s.Can.Id, s.Can.Code, s.Can.SecretNo, s.Can.Remark, s.BoxCount, s.ItemCount, s.Can.CreatedAt
    })));

app.MapGet("/api/cans/{id:int}", async (int id, IPackingService svc) =>
{
    var s = await svc.GetCanAsync(id);
    if (s == null) return Results.NotFound(new { error = "Không tìm thấy thùng." });
    return Results.Ok(new
    {
        s.Can.Id, s.Can.Code, s.Can.SecretNo, s.Can.Remark, s.BoxCount, s.ItemCount,
        boxes = s.Can.Boxes.Select(b => new { b.Id, b.Code, itemCount = b.Items.Count })
    });
});

app.MapPost("/api/cans", async (CanReq r, IPackingService svc) =>
{
    var (ok, msg, id) = await svc.CreateCanAsync(r.Code ?? "", r.SecretNo, r.Remark);
    return ok ? Results.Ok(new { id }) : Results.BadRequest(new { error = msg });
});

app.MapPost("/api/cans/{id:int}/pack", async (int id, PackBoxReq r, IPackingService svc) =>
{
    var (ok, msg) = await svc.PackBoxAsync(id, r.BoxCode ?? "");
    return ok ? Results.Ok(new { ok, message = msg }) : Results.BadRequest(new { error = msg });
});

app.MapPost("/api/cans/{id:int}/unpack", async (int id, PackBoxReq r, IPackingService svc) =>
{
    var (ok, msg) = await svc.UnpackBoxAsync(id, r.BoxCode ?? "");
    return ok ? Results.Ok(new { ok, message = msg }) : Results.BadRequest(new { error = msg });
});

// Lịch sử tra cứu — port từ Rpt_SearchHis của InBrand.
app.MapGet("/api/search-logs", async (string? q, SearchType? type, ISearchLogService svc) =>
    Results.Ok((await svc.ListAsync(q, type)).Select(x => new
    {
        x.Id, x.SearchCode, x.UserCode, type = x.Type.ToString(), x.Found, x.VisitId, x.SearchDTime
    })));

app.MapGet("/api/search-logs/stats", async (ISearchLogService svc) => Results.Ok(await svc.StatsAsync()));

app.MapPost("/api/search-logs", async (SearchLogReq r, ISearchLogService svc) =>
{
    var log = await svc.RecordAsync(r.SearchCode ?? "", r.Type, r.Found, r.UserCode, r.VisitId);
    return Results.Ok(new { log.Id });
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
record BrandReq(string? Code, string? Name, bool Active = true);
record ColorReq(string? Code, string? Name, string? NameVn, bool Active = true);
record ColorMapReq(int ProductId, int ColorId, bool IsDefault = false);
record ColorMapDefaultReq(bool IsDefault);
record VerifyReq(string? Serial, string? Pin);
record ActivateReq(string? Serial, string? Pin, string? CustomerName, string? Phone, string? Address);
record UnitReq(string? Serial, string? Pin, int? ProductId, int? BrandId, string? LotCode, string? Origin, int WarrantyMonths = 12);
record VerifyBatchReq(string? ProductName, string? RefNo, string? RefNoSys, string? TransportType, string? PlateNo, string? ReceivePlace, string? InvOutType, int QtyPlan);
record ScanReq(string? IdNo, string? Pin, string? BoxNo, string? CustomerName);
record BoxReq(string? Code, string? SecretNo, string? Remark);
record PackSerialReq(string? SerialNo);
record CanReq(string? Code, string? SecretNo, string? Remark);
record PackBoxReq(string? BoxCode);
record ImportGlnDto(string? Code, string? Name, string? Address);
record ImportOriginProdDto(string? Code, string? Name, string? Unit);
record ImportLotDto(string? Code, string? ProductCode, string? ProductName, string? GlnCode);
record SearchLogReq(string? SearchCode, SearchType Type, bool Found, string? UserCode, string? VisitId);
