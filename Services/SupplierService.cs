using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record SupplierView(Supplier Supplier);

public interface ISupplierService
{
    Task<List<SupplierView>> ListAsync(string? q, bool? activeOnly);
    Task<Supplier?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool active);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool active);
    Task<(bool ok, string msg)> SetActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteAsync(int id);
}

/// <summary>
/// Danh mục Nhà cung cấp (master) — port từ Mst_Supplier của InBrand.
/// Luật (theo MstSupplierManager.MstSupplierCheckDB + Add/Update/Remove):
///  - SupCode bắt buộc + duy nhất theo tenant (Add: CheckDB Flag.No → mã phải CHƯA tồn tại;
///    Update/Remove: CheckDB Flag.Yes → mã phải TỒN TẠI);
///  - SupName bắt buộc (RequireField trên entity);
///  - SupType luôn được gán NORMAL khi tạo (TConst.SupType.Normal);
///  - cờ hoạt động FlagActive (mặc định Active khi tạo).
/// </summary>
public class SupplierService(AppDbContext db) : ISupplierService
{
    public async Task<List<SupplierView>> ListAsync(string? q, bool? activeOnly)
    {
        var query = db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(s => s.Code.ToLower().Contains(term) || s.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(s => s.Active);

        var suppliers = await query.OrderBy(s => s.Code).ToListAsync();
        return suppliers.Select(s => new SupplierView(s)).ToList();
    }

    public Task<Supplier?> GetAsync(int id) => db.Suppliers.FirstOrDefaultAsync(s => s.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã nhà cung cấp.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên nhà cung cấp.", 0);
        // CheckDB Flag.No: mã phải CHƯA tồn tại.
        if (await db.Suppliers.AnyAsync(s => s.Code == code)) return (false, "Mã nhà cung cấp đã tồn tại.", 0);

        var supplier = new Supplier { Code = code, Name = name, Type = "NORMAL", Active = active };
        db.Suppliers.Add(supplier); await db.SaveChangesAsync();
        return (true, "Đã tạo nhà cung cấp.", supplier.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool active)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return (false, "Không tìm thấy nhà cung cấp.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên nhà cung cấp.");
        supplier.Name = name; supplier.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật nhà cung cấp.");
    }

    public async Task<(bool ok, string msg)> SetActiveAsync(int id, bool active)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return (false, "Không tìm thấy nhà cung cấp.");
        supplier.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt nhà cung cấp." : "Đã ngừng nhà cung cấp.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return (false, "Không tìm thấy nhà cung cấp.");
        db.Suppliers.Remove(supplier); await db.SaveChangesAsync();
        return (true, "Đã xoá nhà cung cấp.");
    }
}
