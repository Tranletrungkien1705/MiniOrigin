using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record MaterialTypeView(MaterialType Type, int ProductCount);

public interface IMaterialTypeService
{
    Task<List<MaterialTypeView>> ListAsync(string? q, bool? activeOnly);
    Task<MaterialType?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool active);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool active);
    Task<(bool ok, string msg)> SetActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteAsync(int id);
    Task<(bool ok, string msg)> AssignProductAsync(int productId, int? materialTypeId);
}

/// <summary>
/// Danh mục Nhóm vật liệu (loại vật liệu) — port từ Mst_PartMaterialType của InBrand.
/// Luật (theo MstPartMaterialTypeManager.MstPartMaterialTypeCheckDB + MstPartManager):
///  - mã nhóm (PMType) bắt buộc + duy nhất theo tenant; tên (PMTypeName) bắt buộc; cờ hoạt động (FlagActive);
///  - khi gán cho sản phẩm: nhóm phải TỒN TẠI và ĐANG HOẠT ĐỘNG;
///  - không xoá nhóm còn sản phẩm tham chiếu (giữ toàn vẹn khoá ngoại MstPart.PMType).
/// </summary>
public class MaterialTypeService(AppDbContext db) : IMaterialTypeService
{
    public async Task<List<MaterialTypeView>> ListAsync(string? q, bool? activeOnly)
    {
        var query = db.MaterialTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term) || t.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(t => t.Active);

        var types = await query.OrderBy(t => t.Code).ToListAsync();
        var counts = await db.Products.Where(p => p.MaterialTypeId != null)
            .GroupBy(p => p.MaterialTypeId!.Value)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TypeId, x => x.Count);
        return types.Select(t => new MaterialTypeView(t, counts.TryGetValue(t.Id, out var c) ? c : 0)).ToList();
    }

    public Task<MaterialType?> GetAsync(int id) => db.MaterialTypes.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã nhóm vật liệu.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên nhóm vật liệu.", 0);
        if (await db.MaterialTypes.AnyAsync(t => t.Code == code)) return (false, "Mã nhóm vật liệu đã tồn tại.", 0);
        var type = new MaterialType { Code = code, Name = name, Active = active };
        db.MaterialTypes.Add(type); await db.SaveChangesAsync();
        return (true, "Đã tạo nhóm vật liệu.", type.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool active)
    {
        var type = await db.MaterialTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy nhóm vật liệu.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên nhóm vật liệu.");
        type.Name = name; type.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật nhóm vật liệu.");
    }

    public async Task<(bool ok, string msg)> SetActiveAsync(int id, bool active)
    {
        var type = await db.MaterialTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy nhóm vật liệu.");
        type.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt nhóm vật liệu." : "Đã ngừng nhóm vật liệu.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var type = await db.MaterialTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy nhóm vật liệu.");
        if (await db.Products.AnyAsync(p => p.MaterialTypeId == id))
            return (false, "Không thể xoá: còn sản phẩm dùng nhóm vật liệu này.");
        db.MaterialTypes.Remove(type); await db.SaveChangesAsync();
        return (true, "Đã xoá nhóm vật liệu.");
    }

    public async Task<(bool ok, string msg)> AssignProductAsync(int productId, int? materialTypeId)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return (false, "Không tìm thấy sản phẩm.");
        if (materialTypeId.HasValue)
        {
            var type = await db.MaterialTypes.FirstOrDefaultAsync(t => t.Id == materialTypeId.Value);
            if (type == null) return (false, "Không tìm thấy nhóm vật liệu.");
            if (!type.Active) return (false, "Nhóm vật liệu đang ngừng hoạt động.");
        }
        product.MaterialTypeId = materialTypeId;
        await db.SaveChangesAsync();
        return (true, "Đã gán nhóm vật liệu cho sản phẩm.");
    }
}
