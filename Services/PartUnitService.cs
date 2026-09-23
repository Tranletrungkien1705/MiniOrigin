using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record PartUnitView(PartUnit Unit, int ProductCount);

public interface IPartUnitService
{
    Task<List<PartUnitView>> ListAsync(string? q, bool? activeOnly);
    Task<PartUnit?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool isStandard, bool active);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool isStandard, bool active);
    Task<(bool ok, string msg)> SetActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteAsync(int id);
}

/// <summary>
/// Danh mục Đơn vị tính — port từ Mst_PartUnit của InBrand.
/// Luật (theo MstPartUnitManager.MstPartUnitCheckDB + Add/Update/Remove):
///  - mã đơn vị (PartUnitCode) bắt buộc + duy nhất theo tenant; tên (PartUnitName) bắt buộc;
///  - cờ hoạt động (FlagActive); FlagUnitStd = đơn vị chuẩn;
///  - chỉ có TỐI ĐA 1 đơn vị chuẩn (đặt chuẩn cho đơn vị này sẽ bỏ chuẩn ở đơn vị khác);
///  - không xoá đơn vị còn sản phẩm tham chiếu (giữ toàn vẹn dữ liệu).
/// </summary>
public class PartUnitService(AppDbContext db) : IPartUnitService
{
    public async Task<List<PartUnitView>> ListAsync(string? q, bool? activeOnly)
    {
        var query = db.PartUnits.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(u => u.Code.ToLower().Contains(term) || u.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(u => u.Active);

        var units = await query.OrderBy(u => u.Code).ToListAsync();
        var counts = await db.Products.Where(p => p.Unit != null)
            .GroupBy(p => p.Unit!)
            .Select(g => new { Unit = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Unit, x => x.Count);
        return units.Select(u => new PartUnitView(u, counts.TryGetValue(u.Code, out var c) ? c : 0)).ToList();
    }

    public Task<PartUnit?> GetAsync(int id) => db.PartUnits.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, bool isStandard, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã đơn vị tính.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên đơn vị tính.", 0);
        if (await db.PartUnits.AnyAsync(u => u.Code == code)) return (false, "Mã đơn vị tính đã tồn tại.", 0);
        if (isStandard) await ClearStandardAsync();
        var unit = new PartUnit { Code = code, Name = name, IsStandard = isStandard, Active = active };
        db.PartUnits.Add(unit); await db.SaveChangesAsync();
        return (true, "Đã tạo đơn vị tính.", unit.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, bool isStandard, bool active)
    {
        var unit = await db.PartUnits.FirstOrDefaultAsync(u => u.Id == id);
        if (unit == null) return (false, "Không tìm thấy đơn vị tính.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên đơn vị tính.");
        if (isStandard && !unit.IsStandard) await ClearStandardAsync();
        unit.Name = name; unit.IsStandard = isStandard; unit.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật đơn vị tính.");
    }

    public async Task<(bool ok, string msg)> SetActiveAsync(int id, bool active)
    {
        var unit = await db.PartUnits.FirstOrDefaultAsync(u => u.Id == id);
        if (unit == null) return (false, "Không tìm thấy đơn vị tính.");
        unit.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt đơn vị tính." : "Đã ngừng đơn vị tính.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var unit = await db.PartUnits.FirstOrDefaultAsync(u => u.Id == id);
        if (unit == null) return (false, "Không tìm thấy đơn vị tính.");
        if (await db.Products.AnyAsync(p => p.Unit == unit.Code))
            return (false, "Không thể xoá: còn sản phẩm dùng đơn vị tính này.");
        db.PartUnits.Remove(unit); await db.SaveChangesAsync();
        return (true, "Đã xoá đơn vị tính.");
    }

    // Chỉ cho phép 1 đơn vị chuẩn: bỏ cờ chuẩn ở mọi đơn vị khác trước khi đặt đơn vị mới.
    private async Task ClearStandardAsync()
    {
        var stds = await db.PartUnits.Where(u => u.IsStandard).ToListAsync();
        foreach (var s in stds) s.IsStandard = false;
    }
}
