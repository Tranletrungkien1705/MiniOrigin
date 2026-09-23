using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

// Kết quả tra cứu công khai theo serial (không lộ mã bí mật).
public record UnitLookup(int Id, string SerialNo, string ProductName, string? BrandName, string? LotCode,
    string? Origin, bool Activated, DateTime? WarrantyDateStart, DateTime? WarrantyDateEnd, int VerifyCount);

// Kết quả xác thực Serial + PIN.
public record VerifyResult(bool Ok, string Message, bool Activated, DateTime? WarrantyDateStart,
    DateTime? WarrantyDateEnd, string? ProductName, string? BrandName);

public interface IAuthenticityService
{
    Task<UnitLookup?> LookupAsync(string serial);
    Task<VerifyResult> VerifyAsync(string serial, string pin);
    Task<VerifyResult> ActivateAsync(string serial, string pin, string? customerName, string? phone, string? address);
    Task<List<ProductUnit>> ListAsync(string? q);
    Task<(bool ok, string msg, int id)> CreateAsync(string serial, string pin, int? productId, int? brandId, string? lotCode, string? origin, int warrantyMonths);
}

/// <summary>
/// Xác thực sản phẩm chính hãng (nguồn gốc thương hiệu) — port từ module BrandPositioning của InBrand
/// (piController: getDetailByQR / PINChecked / ActivePIN; bảng Inv_InventoryBalanceSerial + Inv_InventorySecret).
/// Luật: mỗi đơn vị có Serial (SerialNo_Actual) + mã bí mật (SecretNo/PIN); xác thực đúng cặp thì hợp lệ;
/// lần xác thực đầu tiên kích hoạt bảo hành (WarrantyDateStart) và ghi thông tin khách hàng.
/// </summary>
public class AuthenticityService(AppDbContext db) : IAuthenticityService
{
    public async Task<UnitLookup?> LookupAsync(string serial)
    {
        serial = (serial ?? "").Trim();
        if (string.IsNullOrWhiteSpace(serial)) return null;
        var u = await db.ProductUnits.Include(x => x.Brand)
            .FirstOrDefaultAsync(x => x.SerialNo == serial);
        return u == null ? null : Map(u);
    }

    public async Task<VerifyResult> VerifyAsync(string serial, string pin)
    {
        serial = (serial ?? "").Trim();
        pin = (pin ?? "").Trim();
        if (string.IsNullOrWhiteSpace(serial) || string.IsNullOrWhiteSpace(pin))
            return new VerifyResult(false, "Cần nhập số serial và mã bí mật.", false, null, null, null, null);

        var u = await db.ProductUnits.Include(x => x.Brand).FirstOrDefaultAsync(x => x.SerialNo == serial);
        if (u == null)
            return new VerifyResult(false, "Không tìm thấy sản phẩm với số serial này.", false, null, null, null, null);
        if (!string.Equals(u.SecretNo, pin, StringComparison.OrdinalIgnoreCase))
            return new VerifyResult(false, "Mã bí mật không đúng — sản phẩm có thể là hàng giả.", false, null, null, u.ProductName, u.Brand?.Name);

        u.VerifyCount++;
        await db.SaveChangesAsync();
        return new VerifyResult(true, "Sản phẩm chính hãng.", u.Activated, u.WarrantyDateStart, WarrantyEnd(u), u.ProductName, u.Brand?.Name);
    }

    public async Task<VerifyResult> ActivateAsync(string serial, string pin, string? customerName, string? phone, string? address)
    {
        serial = (serial ?? "").Trim();
        pin = (pin ?? "").Trim();
        var u = await db.ProductUnits.Include(x => x.Brand).FirstOrDefaultAsync(x => x.SerialNo == serial);
        if (u == null)
            return new VerifyResult(false, "Không tìm thấy sản phẩm với số serial này.", false, null, null, null, null);
        if (!string.Equals(u.SecretNo, pin, StringComparison.OrdinalIgnoreCase))
            return new VerifyResult(false, "Mã bí mật không đúng — không thể kích hoạt bảo hành.", false, null, null, u.ProductName, u.Brand?.Name);

        if (u.Activated)
            return new VerifyResult(true, "Sản phẩm đã được kích hoạt bảo hành trước đó.", true, u.WarrantyDateStart, WarrantyEnd(u), u.ProductName, u.Brand?.Name);

        u.Activated = true;
        u.WarrantyDateStart = DateTime.UtcNow;
        u.ActivatedAt = DateTime.UtcNow;
        u.CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        u.CustomerPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        u.CustomerAddress = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        u.VerifyCount++;
        await db.SaveChangesAsync();
        return new VerifyResult(true, "Kích hoạt bảo hành thành công.", true, u.WarrantyDateStart, WarrantyEnd(u), u.ProductName, u.Brand?.Name);
    }

    public Task<List<ProductUnit>> ListAsync(string? q)
    {
        var query = db.ProductUnits.Include(x => x.Brand).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.SerialNo.ToLower().Contains(term) || x.ProductName.ToLower().Contains(term));
        }
        return query.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    public async Task<(bool ok, string msg, int id)> CreateAsync(string serial, string pin, int? productId, int? brandId, string? lotCode, string? origin, int warrantyMonths)
    {
        serial = (serial ?? "").Trim();
        pin = (pin ?? "").Trim();
        if (string.IsNullOrWhiteSpace(serial)) return (false, "Cần số serial.", 0);
        if (string.IsNullOrWhiteSpace(pin)) return (false, "Cần mã bí mật (PIN).", 0);
        if (await db.ProductUnits.AnyAsync(x => x.SerialNo == serial)) return (false, "Số serial đã tồn tại.", 0);

        string productName = serial;
        if (productId.HasValue)
        {
            var p = await db.Products.FirstOrDefaultAsync(x => x.Id == productId.Value);
            if (p == null) return (false, "Không tìm thấy sản phẩm.", 0);
            productName = p.Name;
            brandId ??= p.BrandId;
        }
        if (brandId.HasValue && !await db.Brands.AnyAsync(b => b.Id == brandId.Value))
            return (false, "Không tìm thấy thương hiệu.", 0);

        var unit = new ProductUnit
        {
            SerialNo = serial, SecretNo = pin, ProductId = productId, ProductName = productName,
            BrandId = brandId, LotCode = lotCode?.Trim(), Origin = origin?.Trim(),
            WarrantyMonths = warrantyMonths < 0 ? 0 : warrantyMonths
        };
        db.ProductUnits.Add(unit); await db.SaveChangesAsync();
        return (true, "Đã tạo đơn vị sản phẩm.", unit.Id);
    }

    private static DateTime? WarrantyEnd(ProductUnit u) =>
        u.Activated && u.WarrantyDateStart.HasValue ? u.WarrantyDateStart.Value.AddMonths(u.WarrantyMonths) : null;

    private static UnitLookup Map(ProductUnit u) =>
        new(u.Id, u.SerialNo, u.ProductName, u.Brand?.Name, u.LotCode, u.Origin,
            u.Activated, u.WarrantyDateStart, WarrantyEnd(u), u.VerifyCount);
}
