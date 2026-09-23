using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record PartTypeView(PartType Type, int ProductCount);

public interface IPartTypeService
{
    Task<List<PartTypeView>> ListAsync(string? q, bool? activeOnly);
    Task<PartType?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool active);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool active);
    Task<(bool ok, string msg)> SetActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteAsync(int id);
    Task<(bool ok, string msg)> AssignProductAsync(int productId, int? partTypeId);
}

/// <summary>
/// Danh mục Loại sản phẩm — port từ Mst_PartType của InBrand.
/// Luật (theo MstPartTypeManager.MstPartTypeCheckDB + Add/Update/Remove; MstPartManager):
///  - mã loại (PartType) bắt buộc + duy nhất theo tenant (Add: CheckDB Flag.No → mã phải CHƯA tồn tại;
///    Update/Remove: CheckDB Flag.Yes → mã phải TỒN TẠI);
///  - tên loại (PartTypeName) bắt buộc; cờ hoạt động (FlagActive);
///  - khi gán cho sản phẩm: loại phải TỒN TẠI và ĐANG HOẠT ĐỘNG (MstPartManager kiểm tra PartType
///    với CheckDB Flag.Yes + Flag.Active);
///  - không xoá loại còn sản phẩm tham chiếu (giữ toàn vẹn khoá ngoại MstPart.PartType).
/// </summary>
public class PartTypeService(AppDbContext db) : IPartTypeService
{
    public async Task<List<PartTypeView>> ListAsync(string? q, bool? activeOnly)
    {
        var query = db.PartTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term) || t.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(t => t.Active);

        var types = await query.OrderBy(t => t.Code).ToListAsync();
        var counts = await db.Products.Where(p => p.PartTypeId != null)
            .GroupBy(p => p.PartTypeId!.Value)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TypeId, x => x.Count);
        return types.Select(t => new PartTypeView(t, counts.TryGetValue(t.Id, out var c) ? c : 0)).ToList();
    }

    public Task<PartType?> GetAsync(int id) => db.PartTypes.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã loại sản phẩm.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên loại sản phẩm.", 0);
        // CheckDB Flag.No: mã phải CHƯA tồn tại.
        if (await db.PartTypes.AnyAsync(t => t.Code == code)) return (false, "Mã loại sản phẩm đã tồn tại.", 0);
        var type = new PartType { Code = code, Name = name, Active = active };
        db.PartTypes.Add(type); await db.SaveChangesAsync();
        return (true, "Đã tạo loại sản phẩm.", type.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool active)
    {
        var type = await db.PartTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại sản phẩm.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên loại sản phẩm.");
        type.Name = name; type.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật loại sản phẩm.");
    }

    public async Task<(bool ok, string msg)> SetActiveAsync(int id, bool active)
    {
        var type = await db.PartTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại sản phẩm.");
        type.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt loại sản phẩm." : "Đã ngừng loại sản phẩm.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var type = await db.PartTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại sản phẩm.");
        if (await db.Products.AnyAsync(p => p.PartTypeId == id))
            return (false, "Không thể xoá: còn sản phẩm dùng loại sản phẩm này.");
        db.PartTypes.Remove(type); await db.SaveChangesAsync();
        return (true, "Đã xoá loại sản phẩm.");
    }

    public async Task<(bool ok, string msg)> AssignProductAsync(int productId, int? partTypeId)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return (false, "Không tìm thấy sản phẩm.");
        if (partTypeId.HasValue)
        {
            var type = await db.PartTypes.FirstOrDefaultAsync(t => t.Id == partTypeId.Value);
            if (type == null) return (false, "Không tìm thấy loại sản phẩm.");
            if (!type.Active) return (false, "Loại sản phẩm đang ngừng hoạt động.");
        }
        product.PartTypeId = partTypeId;
        await db.SaveChangesAsync();
        return (true, "Đã gán loại sản phẩm cho sản phẩm.");
    }
}
