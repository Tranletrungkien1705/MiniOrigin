using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record BrandView(Brand Brand, int ProductCount);

public interface IBrandService
{
    Task<List<BrandView>> ListAsync(string? q, bool? activeOnly);
    Task<Brand?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool active);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool active);
    Task<(bool ok, string msg)> SetActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteAsync(int id);
    Task<(bool ok, string msg)> AssignProductAsync(int productId, int? brandId);
}

/// <summary>
/// Danh mục Thương hiệu (nguồn gốc thương hiệu) — port từ Mst_Brand của InBrand.
/// Luật: mã thương hiệu bắt buộc + duy nhất theo tenant; tên bắt buộc; cờ hoạt động;
/// không xoá thương hiệu còn sản phẩm tham chiếu (giữ toàn vẹn khoá ngoại như FK_MstBrand_MstPart).
/// </summary>
public class BrandService(AppDbContext db) : IBrandService
{
    public async Task<List<BrandView>> ListAsync(string? q, bool? activeOnly)
    {
        var query = db.Brands.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(b => b.Code.ToLower().Contains(term) || b.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(b => b.Active);

        var brands = await query.OrderBy(b => b.Name).ToListAsync();
        var counts = await db.Products.Where(p => p.BrandId != null)
            .GroupBy(p => p.BrandId!.Value)
            .Select(g => new { BrandId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BrandId, x => x.Count);
        return brands.Select(b => new BrandView(b, counts.TryGetValue(b.Id, out var c) ? c : 0)).ToList();
    }

    public Task<Brand?> GetAsync(int id) => db.Brands.FirstOrDefaultAsync(b => b.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã thương hiệu.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên thương hiệu.", 0);
        if (await db.Brands.AnyAsync(b => b.Code == code)) return (false, "Mã thương hiệu đã tồn tại.", 0);
        var brand = new Brand { Code = code, Name = name, Active = active };
        db.Brands.Add(brand); await db.SaveChangesAsync();
        return (true, "Đã tạo thương hiệu.", brand.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool active)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id);
        if (brand == null) return (false, "Không tìm thấy thương hiệu.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên thương hiệu.");
        brand.Name = name; brand.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật thương hiệu.");
    }

    public async Task<(bool ok, string msg)> SetActiveAsync(int id, bool active)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id);
        if (brand == null) return (false, "Không tìm thấy thương hiệu.");
        brand.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt thương hiệu." : "Đã ngừng thương hiệu.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id);
        if (brand == null) return (false, "Không tìm thấy thương hiệu.");
        if (await db.Products.AnyAsync(p => p.BrandId == id))
            return (false, "Không thể xoá: còn sản phẩm thuộc thương hiệu này.");
        db.Brands.Remove(brand); await db.SaveChangesAsync();
        return (true, "Đã xoá thương hiệu.");
    }

    public async Task<(bool ok, string msg)> AssignProductAsync(int productId, int? brandId)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return (false, "Không tìm thấy sản phẩm.");
        if (brandId.HasValue && !await db.Brands.AnyAsync(b => b.Id == brandId.Value))
            return (false, "Không tìm thấy thương hiệu.");
        product.BrandId = brandId;
        await db.SaveChangesAsync();
        return (true, "Đã gán thương hiệu cho sản phẩm.");
    }
}
