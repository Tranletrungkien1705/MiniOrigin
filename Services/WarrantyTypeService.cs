using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record WarrantyTypeView(WarrantyType Type, int ProductCount);

public interface IWarrantyTypeService
{
    Task<List<WarrantyTypeView>> ListAsync(string? q, bool? activeOnly);
    Task<WarrantyType?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, string? remark, bool active);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, string? remark, bool active);
    Task<(bool ok, string msg)> SetActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteAsync(int id);
    Task<(bool ok, string msg)> AssignProductAsync(int productId, int? warrantyTypeId);
}

/// <summary>
/// Danh mục Loại thời hạn bảo hành — port từ Mst_PartWarrantyType của InBrand.
/// Luật (theo MstPartWarrantyTypeCheckDB + MstPartManager):
///  - mã loại bắt buộc + duy nhất theo tenant; tên bắt buộc; cờ hoạt động;
///  - khi gán cho sản phẩm: loại phải TỒN TẠI và ĐANG HOẠT ĐỘNG (FlagActive);
///  - không xoá loại còn sản phẩm tham chiếu (giữ toàn vẹn khoá ngoại như FK_MstPartWarrantyType_MstPart).
/// </summary>
public class WarrantyTypeService(AppDbContext db) : IWarrantyTypeService
{
    public async Task<List<WarrantyTypeView>> ListAsync(string? q, bool? activeOnly)
    {
        var query = db.WarrantyTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term) || t.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(t => t.Active);

        var types = await query.OrderBy(t => t.Code).ToListAsync();
        var counts = await db.Products.Where(p => p.WarrantyTypeId != null)
            .GroupBy(p => p.WarrantyTypeId!.Value)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TypeId, x => x.Count);
        return types.Select(t => new WarrantyTypeView(t, counts.TryGetValue(t.Id, out var c) ? c : 0)).ToList();
    }

    public Task<WarrantyType?> GetAsync(int id) => db.WarrantyTypes.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, string? remark, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã loại bảo hành.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên loại bảo hành.", 0);
        if (await db.WarrantyTypes.AnyAsync(t => t.Code == code)) return (false, "Mã loại bảo hành đã tồn tại.", 0);
        var type = new WarrantyType { Code = code, Name = name, Remark = remark?.Trim(), Active = active };
        db.WarrantyTypes.Add(type); await db.SaveChangesAsync();
        return (true, "Đã tạo loại bảo hành.", type.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, string? remark, bool active)
    {
        var type = await db.WarrantyTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại bảo hành.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên loại bảo hành.");
        type.Name = name; type.Remark = remark?.Trim(); type.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật loại bảo hành.");
    }

    public async Task<(bool ok, string msg)> SetActiveAsync(int id, bool active)
    {
        var type = await db.WarrantyTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại bảo hành.");
        type.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt loại bảo hành." : "Đã ngừng loại bảo hành.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var type = await db.WarrantyTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại bảo hành.");
        if (await db.Products.AnyAsync(p => p.WarrantyTypeId == id))
            return (false, "Không thể xoá: còn sản phẩm dùng loại bảo hành này.");
        db.WarrantyTypes.Remove(type); await db.SaveChangesAsync();
        return (true, "Đã xoá loại bảo hành.");
    }

    public async Task<(bool ok, string msg)> AssignProductAsync(int productId, int? warrantyTypeId)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return (false, "Không tìm thấy sản phẩm.");
        if (warrantyTypeId.HasValue)
        {
            var type = await db.WarrantyTypes.FirstOrDefaultAsync(t => t.Id == warrantyTypeId.Value);
            if (type == null) return (false, "Không tìm thấy loại bảo hành.");
            if (!type.Active) return (false, "Loại bảo hành đang ngừng hoạt động.");
        }
        product.WarrantyTypeId = warrantyTypeId;
        await db.SaveChangesAsync();
        return (true, "Đã gán loại bảo hành cho sản phẩm.");
    }
}