using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record DealerView(Dealer Dealer, string? ParentName, string? DealerTypeName, int ChildCount);
public record DealerTypeView(DealerType Type, int DealerCount);

public interface IDealerService
{
    // Loại đại lý (Mst_DealerType)
    Task<List<DealerTypeView>> ListTypesAsync(string? q, bool? activeOnly);
    Task<(bool ok, string msg, int id)> CreateTypeAsync(string code, string name, bool active);
    Task<(bool ok, string msg)> UpdateTypeAsync(int id, string name, bool active);
    Task<(bool ok, string msg)> DeleteTypeAsync(int id);

    // Đại lý (Mst_Dealer)
    Task<List<DealerView>> ListAsync(string? q, bool? activeOnly);
    Task<DealerView?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, int? parentId, int? dealerTypeId,
        string? invCode, string? materialTypeCode, string? skycicSiteId, string? remark, bool active);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, int? parentId, int? dealerTypeId,
        string? invCode, string? materialTypeCode, string? skycicSiteId, string? remark, bool active);
    Task<(bool ok, string msg)> SetActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteAsync(int id);
}

/// <summary>
/// Danh mục Đại lý (master) — port từ Mst_Dealer / Mst_DealerType của InBrand.
/// Luật (theo MstDealerManager.MstDealerAddX/Update/Remove + MstDealerCheckDB):
///  - DLCode bắt buộc + duy nhất theo tenant (Add: CheckDB Flag.No → mã phải CHƯA tồn tại;
///    Update/Remove: CheckDB Flag.Yes → mã phải TỒN TẠI);
///  - DLName bắt buộc (RequireField trên entity);
///  - DLCodeParent (nếu có) phải TỒN TẠI & ĐANG HOẠT ĐỘNG (CheckDB Flag.Yes + Flag.Active);
///  - khi tạo: FlagRoot=No, DLBUCode/DLBUPattern="X", DLLevel=1, FlagActive=Active;
///  - chặn xoá đại lý còn đại lý con tham chiếu (giữ toàn vẹn cây phân cấp).
/// </summary>
public class DealerService(AppDbContext db) : IDealerService
{
    // ---------- Loại đại lý ----------

    public async Task<List<DealerTypeView>> ListTypesAsync(string? q, bool? activeOnly)
    {
        var query = db.DealerTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term) || t.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(t => t.Active);

        var types = await query.OrderBy(t => t.Code).ToListAsync();
        var counts = await db.Dealers.GroupBy(d => d.DealerTypeId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return types.Select(t => new DealerTypeView(t, counts.FirstOrDefault(c => c.Key == t.Id)?.Count ?? 0)).ToList();
    }

    public async Task<(bool ok, string msg, int id)> CreateTypeAsync(string code, string name, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã loại đại lý.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên loại đại lý.", 0);
        if (await db.DealerTypes.AnyAsync(t => t.Code == code)) return (false, "Mã loại đại lý đã tồn tại.", 0);

        var type = new DealerType { Code = code, Name = name, Active = active };
        db.DealerTypes.Add(type); await db.SaveChangesAsync();
        return (true, "Đã tạo loại đại lý.", type.Id);
    }

    public async Task<(bool ok, string msg)> UpdateTypeAsync(int id, string name, bool active)
    {
        var type = await db.DealerTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại đại lý.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên loại đại lý.");
        type.Name = name; type.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật loại đại lý.");
    }

    public async Task<(bool ok, string msg)> DeleteTypeAsync(int id)
    {
        var type = await db.DealerTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại đại lý.");
        if (await db.Dealers.AnyAsync(d => d.DealerTypeId == id)) return (false, "Không thể xoá: còn đại lý thuộc loại này.");
        db.DealerTypes.Remove(type); await db.SaveChangesAsync();
        return (true, "Đã xoá loại đại lý.");
    }

    // ---------- Đại lý ----------

    public async Task<List<DealerView>> ListAsync(string? q, bool? activeOnly)
    {
        var query = db.Dealers.Include(d => d.Parent).Include(d => d.DealerType).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(d => d.Code.ToLower().Contains(term) || d.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(d => d.Active);

        var dealers = await query.OrderBy(d => d.Code).ToListAsync();
        var childCounts = await db.Dealers.Where(d => d.ParentId != null)
            .GroupBy(d => d.ParentId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return dealers.Select(d => new DealerView(d, d.Parent?.Name, d.DealerType?.Name,
            childCounts.FirstOrDefault(c => c.Key == d.Id)?.Count ?? 0)).ToList();
    }

    public async Task<DealerView?> GetAsync(int id)
    {
        var d = await db.Dealers.Include(x => x.Parent).Include(x => x.DealerType).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return null;
        var childCount = await db.Dealers.CountAsync(x => x.ParentId == id);
        return new DealerView(d, d.Parent?.Name, d.DealerType?.Name, childCount);
    }

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, int? parentId, int? dealerTypeId,
        string? invCode, string? materialTypeCode, string? skycicSiteId, string? remark, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã đại lý.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên đại lý.", 0);
        // CheckDB Flag.No: mã phải CHƯA tồn tại.
        if (await db.Dealers.AnyAsync(d => d.Code == code)) return (false, "Mã đại lý đã tồn tại.", 0);

        // DLCodeParent (nếu có) phải TỒN TẠI & ĐANG HOẠT ĐỘNG.
        if (parentId is > 0)
        {
            var parent = await db.Dealers.FirstOrDefaultAsync(d => d.Id == parentId);
            if (parent == null) return (false, "Không tìm thấy đại lý cấp trên.", 0);
            if (!parent.Active) return (false, "Đại lý cấp trên đang ngừng hoạt động.", 0);
        }
        else parentId = null;

        if (dealerTypeId is > 0)
        {
            var type = await db.DealerTypes.FirstOrDefaultAsync(t => t.Id == dealerTypeId);
            if (type == null) return (false, "Không tìm thấy loại đại lý.", 0);
            if (!type.Active) return (false, "Loại đại lý đang ngừng hoạt động.", 0);
        }
        else dealerTypeId = null;

        var dealer = new Dealer
        {
            Code = code, Name = name, ParentId = parentId, DealerTypeId = dealerTypeId,
            InvCode = string.IsNullOrWhiteSpace(invCode) ? null : invCode.Trim(),
            MaterialTypeCode = string.IsNullOrWhiteSpace(materialTypeCode) ? null : materialTypeCode.Trim().ToUpperInvariant(),
            SkycicSiteID = string.IsNullOrWhiteSpace(skycicSiteId) ? null : skycicSiteId.Trim(),
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim(),
            // Mặc định khi tạo (theo MstDealerAddX).
            IsRoot = false, BuCode = "X", BuPattern = "X", Level = 1, Active = active
        };
        db.Dealers.Add(dealer); await db.SaveChangesAsync();
        return (true, "Đã tạo đại lý.", dealer.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, int? parentId, int? dealerTypeId,
        string? invCode, string? materialTypeCode, string? skycicSiteId, string? remark, bool active)
    {
        var dealer = await db.Dealers.FirstOrDefaultAsync(d => d.Id == id);
        if (dealer == null) return (false, "Không tìm thấy đại lý.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên đại lý.");

        if (parentId is > 0)
        {
            if (parentId == id) return (false, "Đại lý không thể là cấp trên của chính nó.");
            var parent = await db.Dealers.FirstOrDefaultAsync(d => d.Id == parentId);
            if (parent == null) return (false, "Không tìm thấy đại lý cấp trên.");
            if (!parent.Active) return (false, "Đại lý cấp trên đang ngừng hoạt động.");
        }
        else parentId = null;

        if (dealerTypeId is > 0)
        {
            var type = await db.DealerTypes.FirstOrDefaultAsync(t => t.Id == dealerTypeId);
            if (type == null) return (false, "Không tìm thấy loại đại lý.");
            if (!type.Active) return (false, "Loại đại lý đang ngừng hoạt động.");
        }
        else dealerTypeId = null;

        dealer.Name = name; dealer.ParentId = parentId; dealer.DealerTypeId = dealerTypeId;
        dealer.InvCode = string.IsNullOrWhiteSpace(invCode) ? null : invCode.Trim();
        dealer.MaterialTypeCode = string.IsNullOrWhiteSpace(materialTypeCode) ? null : materialTypeCode.Trim().ToUpperInvariant();
        dealer.SkycicSiteID = string.IsNullOrWhiteSpace(skycicSiteId) ? null : skycicSiteId.Trim();
        dealer.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
        dealer.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật đại lý.");
    }

    public async Task<(bool ok, string msg)> SetActiveAsync(int id, bool active)
    {
        var dealer = await db.Dealers.FirstOrDefaultAsync(d => d.Id == id);
        if (dealer == null) return (false, "Không tìm thấy đại lý.");
        dealer.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt đại lý." : "Đã ngừng đại lý.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var dealer = await db.Dealers.FirstOrDefaultAsync(d => d.Id == id);
        if (dealer == null) return (false, "Không tìm thấy đại lý.");
        if (await db.Dealers.AnyAsync(d => d.ParentId == id)) return (false, "Không thể xoá: còn đại lý cấp dưới.");
        db.Dealers.Remove(dealer); await db.SaveChangesAsync();
        return (true, "Đã xoá đại lý.");
    }
}
