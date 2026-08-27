using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record KdeValue(string Label, string Value, string? Unit);
public record EventView(TraceEvent Ev, List<KdeValue> Kdes);
public record LotTrace(Lot Lot, List<EventView> Events, List<LotTrace> Parents, int Depth);
public record OriginDash(int Lots, int Events, int Ctes, int Glns, List<Lot> Recent);

public interface IOriginService
{
    Task<List<Cte>> CtesAsync();
    Task<Cte?> GetCteAsync(int id);
    Task<(bool ok, string msg, int id)> CreateCteAsync(Cte c);
    Task<(bool ok, string msg)> AddKdeAsync(KdeDef k);
    Task<List<Gln>> GlnsAsync();
    Task<(bool ok, string msg)> CreateGlnAsync(Gln g);
    Task<List<Product>> ProductsAsync();
    Task<Product> EnsureProductAsync(string name, string? unit);
    Task<List<Lot>> LotsAsync(string? q);
    Task<Lot?> GetLotAsync(int id);
    Task<(bool ok, string msg, int id)> CreateLotAsync(string code, string productName, string? unit, decimal qty, int? originGlnId);
    Task<(bool ok, string msg)> AddEventAsync(int lotId, int cteId, int? glnId, DateTime when, string? op, string? note, Dictionary<string, string> kde);
    Task<(bool ok, string msg)> LinkLotAsync(int childLotId, string parentCode, decimal? qty);
    Task<List<Lot>> ParentLotsAsync(int childLotId);
    Task<LotTrace?> TraceByCodeAsync(string code);
    Task<OriginDash> DashboardAsync();
}

public class OriginService(AppDbContext db) : IOriginService
{
    public Task<List<Cte>> CtesAsync() => db.Ctes.Include(c => c.Kdes).OrderBy(c => c.Ordinal).ThenBy(c => c.Id).ToListAsync();
    public Task<Cte?> GetCteAsync(int id) => db.Ctes.Include(c => c.Kdes).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateCteAsync(Cte c)
    {
        if (string.IsNullOrWhiteSpace(c.Name)) return (false, "Cần tên sự kiện.", 0);
        if (string.IsNullOrWhiteSpace(c.Code)) c.Code = "CTE" + (await db.Ctes.CountAsync() + 1).ToString("D2");
        if (await db.Ctes.AnyAsync(x => x.Code == c.Code)) return (false, "Mã CTE đã tồn tại.", 0);
        db.Ctes.Add(c); await db.SaveChangesAsync();
        return (true, "Đã tạo loại sự kiện.", c.Id);
    }

    public async Task<(bool ok, string msg)> AddKdeAsync(KdeDef k)
    {
        if (string.IsNullOrWhiteSpace(k.Label)) return (false, "Cần nhãn trường.");
        if (string.IsNullOrWhiteSpace(k.Key)) k.Key = Slug(k.Label);
        if (!await db.Ctes.AnyAsync(c => c.Id == k.CteId)) return (false, "Không tìm thấy CTE.");
        if (await db.Kdes.AnyAsync(x => x.CteId == k.CteId && x.Key == k.Key)) return (false, "Trường đã tồn tại.");
        db.Kdes.Add(k); await db.SaveChangesAsync();
        return (true, "Đã thêm trường KDE.");
    }

    public Task<List<Gln>> GlnsAsync() => db.Glns.OrderBy(g => g.Name).ToListAsync();
    public async Task<(bool ok, string msg)> CreateGlnAsync(Gln g)
    {
        if (string.IsNullOrWhiteSpace(g.Name)) return (false, "Cần tên địa điểm.");
        if (string.IsNullOrWhiteSpace(g.Code)) g.Code = "893" + (await db.Glns.CountAsync() + 1).ToString("D10");
        if (await db.Glns.AnyAsync(x => x.Code == g.Code)) return (false, "GLN đã tồn tại.");
        db.Glns.Add(g); await db.SaveChangesAsync();
        return (true, "Đã thêm địa điểm.");
    }

    public Task<List<Product>> ProductsAsync() => db.Products.OrderBy(p => p.Name).ToListAsync();
    public async Task<Product> EnsureProductAsync(string name, string? unit)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.Name == name);
        if (p != null) return p;
        p = new Product { Name = name, Unit = unit, Code = "SP" + (await db.Products.CountAsync() + 1).ToString("D4") };
        db.Products.Add(p); await db.SaveChangesAsync();
        return p;
    }

    public Task<List<Lot>> LotsAsync(string? q)
    {
        var query = db.Lots.Include(l => l.OriginGln).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(l => l.Code.Contains(q) || l.ProductName.Contains(q));
        return query.OrderByDescending(l => l.Id).ToListAsync();
    }

    public Task<Lot?> GetLotAsync(int id) =>
        db.Lots.Include(l => l.OriginGln).Include(l => l.Events).FirstOrDefaultAsync(l => l.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateLotAsync(string code, string productName, string? unit, decimal qty, int? originGlnId)
    {
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã lô.", 0);
        if (string.IsNullOrWhiteSpace(productName)) return (false, "Cần tên sản phẩm.", 0);
        code = code.Trim();
        if (await db.Lots.IgnoreQueryFilters().AnyAsync(l => l.Code == code)) return (false, "Mã lô đã tồn tại.", 0);
        var prod = await EnsureProductAsync(productName.Trim(), unit);
        var lot = new Lot { Code = code, ProductId = prod.Id, ProductName = prod.Name, Unit = unit, Quantity = qty, OriginGlnId = originGlnId };
        db.Lots.Add(lot); await db.SaveChangesAsync();
        return (true, "Đã tạo lô.", lot.Id);
    }

    public async Task<(bool ok, string msg)> AddEventAsync(int lotId, int cteId, int? glnId, DateTime when, string? op, string? note, Dictionary<string, string> kde)
    {
        var lot = await db.Lots.FirstOrDefaultAsync(l => l.Id == lotId);
        if (lot == null) return (false, "Không tìm thấy lô.");
        var cte = await db.Ctes.Include(c => c.Kdes).FirstOrDefaultAsync(c => c.Id == cteId);
        if (cte == null) return (false, "Không tìm thấy loại sự kiện.");
        foreach (var d in cte.Kdes.Where(x => x.Required))
            if (!kde.TryGetValue(d.Key, out var v) || string.IsNullOrWhiteSpace(v))
                return (false, $"Thiếu trường bắt buộc: {d.Label}.");
        var gln = glnId.HasValue ? await db.Glns.FirstOrDefaultAsync(g => g.Id == glnId) : null;
        var seq = (await db.Events.Where(x => x.LotId == lotId).MaxAsync(x => (int?)x.Sequence) ?? 0) + 1;
        var clean = kde.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value);
        db.Events.Add(new TraceEvent
        {
            LotId = lotId, CteId = cte.Id, CteName = cte.Name, CteIcon = cte.Icon,
            GlnId = gln?.Id, GlnName = gln?.Name, EventTime = when == default ? DateTime.Now : when,
            Operator = op, Note = note, KdeJson = JsonSerializer.Serialize(clean), Sequence = seq
        });
        await db.SaveChangesAsync();
        return (true, "Đã ghi sự kiện.");
    }

    public async Task<(bool ok, string msg)> LinkLotAsync(int childLotId, string parentCode, decimal? qty)
    {
        parentCode = (parentCode ?? "").Trim();
        var parent = await db.Lots.FirstOrDefaultAsync(l => l.Code == parentCode);
        if (parent == null) return (false, "Không tìm thấy lô nguyên liệu.");
        if (parent.Id == childLotId) return (false, "Lô không thể là nguyên liệu của chính nó.");
        if (await db.LotLinks.AnyAsync(x => x.ChildLotId == childLotId && x.ParentLotId == parent.Id)) return (false, "Đã liên kết.");
        db.LotLinks.Add(new LotLink { ChildLotId = childLotId, ParentLotId = parent.Id, Quantity = qty });
        await db.SaveChangesAsync();
        return (true, $"Đã liên kết nguyên liệu {parent.Code}.");
    }

    public async Task<List<Lot>> ParentLotsAsync(int childLotId)
    {
        var pids = await db.LotLinks.Where(x => x.ChildLotId == childLotId).Select(x => x.ParentLotId).ToListAsync();
        return await db.Lots.Where(l => pids.Contains(l.Id)).ToListAsync();
    }

    public Task<LotTrace?> TraceByCodeAsync(string code)
    {
        code = (code ?? "").Trim();
        return BuildTraceAsync(code, 0, new HashSet<int>());
    }

    // Truy xuất công khai xuyên tenant qua mã lô toàn cục; đệ quy phả hệ nguyên liệu (giới hạn độ sâu).
    private async Task<LotTrace?> BuildTraceAsync(string code, int depth, HashSet<int> seen)
    {
        var lot = await db.Lots.IgnoreQueryFilters().Include(l => l.OriginGln)
            .FirstOrDefaultAsync(l => l.Code == code);
        if (lot == null || !seen.Add(lot.Id) || depth > 5) return lot == null ? null : new LotTrace(lot!, new(), new(), depth);

        var events = await db.Events.IgnoreQueryFilters().Where(e => e.LotId == lot.Id)
            .OrderBy(e => e.Sequence).ThenBy(e => e.EventTime).ToListAsync();
        var cteKdes = await db.Kdes.IgnoreQueryFilters().Where(k => k.OrgId == lot.OrgId).ToListAsync();
        var evViews = events.Select(e => new EventView(e, ParseKde(e, cteKdes))).ToList();

        var parentIds = await db.LotLinks.IgnoreQueryFilters().Where(x => x.ChildLotId == lot.Id).Select(x => x.ParentLotId).ToListAsync();
        var parentCodes = await db.Lots.IgnoreQueryFilters().Where(l => parentIds.Contains(l.Id)).Select(l => l.Code).ToListAsync();
        var parents = new List<LotTrace>();
        foreach (var pc in parentCodes)
        {
            var pt = await BuildTraceAsync(pc, depth + 1, seen);
            if (pt != null) parents.Add(pt);
        }
        return new LotTrace(lot, evViews, parents, depth);
    }

    private static List<KdeValue> ParseKde(TraceEvent e, List<KdeDef> allKdes)
    {
        var result = new List<KdeValue>();
        Dictionary<string, string>? dict;
        try { dict = JsonSerializer.Deserialize<Dictionary<string, string>>(e.KdeJson); } catch { dict = null; }
        if (dict == null) return result;
        var defs = allKdes.Where(k => k.CteId == e.CteId).OrderBy(k => k.Ordinal).ToList();
        foreach (var kv in dict)
        {
            var def = defs.FirstOrDefault(d => d.Key == kv.Key);
            result.Add(new KdeValue(def?.Label ?? kv.Key, kv.Value, def?.Unit));
        }
        return result;
    }

    public async Task<OriginDash> DashboardAsync() => new(
        await db.Lots.CountAsync(), await db.Events.CountAsync(), await db.Ctes.CountAsync(), await db.Glns.CountAsync(),
        await db.Lots.Include(l => l.OriginGln).OrderByDescending(l => l.Id).Take(8).ToListAsync());

    private static string Slug(string s) => new string(s.ToLower().Replace(" ", "_").Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
}
