using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

// Màu sắc kèm số sản phẩm đang gán màu này.
public record ProductColorView(ProductColor Color, int ProductCount);

// 1 dòng gán màu cho sản phẩm (kèm tên sản phẩm + tên màu để hiển thị).
public record ProductColorMapView(ProductColorMap Map, string ProductName, string ColorName, string ColorNameVn);

public interface IProductColorService
{
    Task<List<ProductColorView>> ListColorsAsync(string? q, bool? activeOnly);
    Task<ProductColor?> GetColorAsync(int id);
    Task<(bool ok, string msg, int id)> CreateColorAsync(string code, string name, string nameVn, bool active);
    Task<(bool ok, string msg)> UpdateColorAsync(int id, string name, string nameVn, bool active);
    Task<(bool ok, string msg)> SetColorActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteColorAsync(int id);

    Task<List<ProductColorMapView>> ListMapsAsync(int? productId, string? q);
    Task<(bool ok, string msg, int id)> AssignAsync(int productId, int colorId, bool isDefault);
    Task<(bool ok, string msg)> SetMapDefaultAsync(int mapId, bool isDefault);
    Task<(bool ok, string msg)> SetMapActiveAsync(int mapId, bool active);
    Task<(bool ok, string msg)> RemoveMapAsync(int mapId);
}

/// <summary>
/// Danh mục Màu sắc sản phẩm + gán màu cho sản phẩm — port từ Mst_PartColor / Mst_MapPartColor của InBrand.
/// Luật (theo MstPartColorManager / MstMapPartColorManager):
///  - Màu: mã bắt buộc + duy nhất theo tenant; tên (EN) và tên tiếng Việt bắt buộc; cờ hoạt động.
///  - Gán màu: sản phẩm và màu phải tồn tại & đang hoạt động; 1 cặp sản phẩm-màu chỉ gán 1 lần;
///    MỖI SẢN PHẨM CHỈ CÓ TỐI ĐA 1 MÀU MẶC ĐỊNH (Mst_MapPartColor_CheckFlagDefault).
///  - Không xoá màu còn được gán cho sản phẩm (giữ toàn vẹn khoá ngoại).
/// </summary>
public class ProductColorService(AppDbContext db) : IProductColorService
{
    // ---------- Danh mục màu ----------

    public async Task<List<ProductColorView>> ListColorsAsync(string? q, bool? activeOnly)
    {
        var query = db.ProductColors.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.Code.ToLower().Contains(term)
                || c.Name.ToLower().Contains(term) || c.NameVn.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(c => c.Active);

        var colors = await query.OrderBy(c => c.Name).ToListAsync();
        var counts = await db.ProductColorMaps
            .GroupBy(m => m.ColorId)
            .Select(g => new { ColorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ColorId, x => x.Count);
        return colors.Select(c => new ProductColorView(c, counts.TryGetValue(c.Id, out var n) ? n : 0)).ToList();
    }

    public Task<ProductColor?> GetColorAsync(int id) => db.ProductColors.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateColorAsync(string code, string name, string nameVn, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        nameVn = (nameVn ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã màu.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên màu (EN).", 0);
        if (string.IsNullOrWhiteSpace(nameVn)) return (false, "Cần tên màu tiếng Việt.", 0);
        if (await db.ProductColors.AnyAsync(c => c.Code == code)) return (false, "Mã màu đã tồn tại.", 0);
        var color = new ProductColor { Code = code, Name = name, NameVn = nameVn, Active = active };
        db.ProductColors.Add(color); await db.SaveChangesAsync();
        return (true, "Đã tạo màu.", color.Id);
    }

    public async Task<(bool ok, string msg)> UpdateColorAsync(int id, string name, string nameVn, bool active)
    {
        var color = await db.ProductColors.FirstOrDefaultAsync(c => c.Id == id);
        if (color == null) return (false, "Không tìm thấy màu.");
        name = (name ?? "").Trim();
        nameVn = (nameVn ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên màu (EN).");
        if (string.IsNullOrWhiteSpace(nameVn)) return (false, "Cần tên màu tiếng Việt.");
        color.Name = name; color.NameVn = nameVn; color.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật màu.");
    }

    public async Task<(bool ok, string msg)> SetColorActiveAsync(int id, bool active)
    {
        var color = await db.ProductColors.FirstOrDefaultAsync(c => c.Id == id);
        if (color == null) return (false, "Không tìm thấy màu.");
        color.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt màu." : "Đã ngừng màu.");
    }

    public async Task<(bool ok, string msg)> DeleteColorAsync(int id)
    {
        var color = await db.ProductColors.FirstOrDefaultAsync(c => c.Id == id);
        if (color == null) return (false, "Không tìm thấy màu.");
        if (await db.ProductColorMaps.AnyAsync(m => m.ColorId == id))
            return (false, "Không thể xoá: màu còn được gán cho sản phẩm.");
        db.ProductColors.Remove(color); await db.SaveChangesAsync();
        return (true, "Đã xoá màu.");
    }

    // ---------- Gán màu cho sản phẩm ----------

    public async Task<List<ProductColorMapView>> ListMapsAsync(int? productId, string? q)
    {
        var query = db.ProductColorMaps.Include(m => m.Product).Include(m => m.Color).AsQueryable();
        if (productId.HasValue) query = query.Where(m => m.ProductId == productId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(m => (m.Product != null && m.Product.Name.ToLower().Contains(term))
                || (m.Color != null && (m.Color.Name.ToLower().Contains(term) || m.Color.NameVn.ToLower().Contains(term))));
        }
        var maps = await query.OrderBy(m => m.ProductId).ThenByDescending(m => m.IsDefault).ToListAsync();
        return maps.Select(m => new ProductColorMapView(
            m, m.Product?.Name ?? "", m.Color?.Name ?? "", m.Color?.NameVn ?? "")).ToList();
    }

    public async Task<(bool ok, string msg, int id)> AssignAsync(int productId, int colorId, bool isDefault)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return (false, "Không tìm thấy sản phẩm.", 0);
        var color = await db.ProductColors.FirstOrDefaultAsync(c => c.Id == colorId);
        if (color == null) return (false, "Không tìm thấy màu.", 0);
        if (!color.Active) return (false, "Màu đang ngừng hoạt động.", 0);
        if (await db.ProductColorMaps.AnyAsync(m => m.ProductId == productId && m.ColorId == colorId))
            return (false, "Sản phẩm đã được gán màu này.", 0);

        // Luật: mỗi sản phẩm chỉ có tối đa 1 màu mặc định.
        if (isDefault)
        {
            var current = await db.ProductColorMaps
                .Where(m => m.ProductId == productId && m.IsDefault).ToListAsync();
            foreach (var m in current) m.IsDefault = false;
        }

        var map = new ProductColorMap { ProductId = productId, ColorId = colorId, IsDefault = isDefault, Active = true };
        db.ProductColorMaps.Add(map); await db.SaveChangesAsync();
        return (true, "Đã gán màu cho sản phẩm.", map.Id);
    }

    public async Task<(bool ok, string msg)> SetMapDefaultAsync(int mapId, bool isDefault)
    {
        var map = await db.ProductColorMaps.FirstOrDefaultAsync(m => m.Id == mapId);
        if (map == null) return (false, "Không tìm thấy liên kết màu.");
        if (isDefault)
        {
            var current = await db.ProductColorMaps
                .Where(m => m.ProductId == map.ProductId && m.IsDefault && m.Id != mapId).ToListAsync();
            foreach (var m in current) m.IsDefault = false;
        }
        map.IsDefault = isDefault;
        await db.SaveChangesAsync();
        return (true, isDefault ? "Đã đặt làm màu mặc định." : "Đã bỏ màu mặc định.");
    }

    public async Task<(bool ok, string msg)> SetMapActiveAsync(int mapId, bool active)
    {
        var map = await db.ProductColorMaps.FirstOrDefaultAsync(m => m.Id == mapId);
        if (map == null) return (false, "Không tìm thấy liên kết màu.");
        map.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt liên kết." : "Đã ngừng liên kết.");
    }

    public async Task<(bool ok, string msg)> RemoveMapAsync(int mapId)
    {
        var map = await db.ProductColorMaps.FirstOrDefaultAsync(m => m.Id == mapId);
        if (map == null) return (false, "Không tìm thấy liên kết màu.");
        db.ProductColorMaps.Remove(map); await db.SaveChangesAsync();
        return (true, "Đã bỏ gán màu.");
    }
}