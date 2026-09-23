using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

// Tổng hợp 1 hộp: số serial đã đóng + thông tin thùng chứa.
public record BoxSummary(Box Box, int ItemCount, string? CanCode);

// Tổng hợp 1 thùng: số hộp + tổng số serial bên trong.
public record CanSummary(Can Can, int BoxCount, int ItemCount);

// Kết quả tra cứu công khai theo mã hộp.
public record BoxLookup(string Code, string? CanCode, int ItemCount,
    List<BoxLookupItem> Items);
public record BoxLookupItem(string SerialNo, string? ProductName, DateTime PackedAt);

public interface IPackingService
{
    Task<List<BoxSummary>> ListBoxesAsync(string? q);
    Task<BoxSummary?> GetBoxAsync(int id);
    Task<BoxLookup?> LookupBoxAsync(string code);
    Task<(bool ok, string msg, int id)> CreateBoxAsync(string code, string? secretNo, string? remark);
    // Đóng 1 serial vào hộp: serial phải có trong kho xác thực và chưa nằm ở hộp khác.
    Task<(bool ok, string msg)> PackSerialAsync(int boxId, string serialNo);
    Task<(bool ok, string msg)> UnpackSerialAsync(int boxId, string serialNo);

    Task<List<CanSummary>> ListCansAsync(string? q);
    Task<CanSummary?> GetCanAsync(int id);
    Task<(bool ok, string msg, int id)> CreateCanAsync(string code, string? secretNo, string? remark);
    // Đóng 1 hộp vào thùng: hộp phải tồn tại và chưa nằm ở thùng khác.
    Task<(bool ok, string msg)> PackBoxAsync(int canId, string boxCode);
    Task<(bool ok, string msg)> UnpackBoxAsync(int canId, string boxCode);
}

/// <summary>
/// Đóng hộp / Đóng thùng (packing) — port từ module Box/Can của InBrand
/// (BoxController: tra cứu mã hộp; bảng Inv_InventoryBox + Inv_InventoryCan; cờ FlagBox/FlagCan trên Inv_InventoryBalanceSerial).
/// Luật: 1 hộp gom nhiều đơn vị sản phẩm (serial); 1 thùng gom nhiều hộp;
/// serial phải có trong kho xác thực (ProductUnit) và chỉ nằm trong 1 hộp;
/// hộp chỉ nằm trong 1 thùng; tra cứu công khai theo mã hộp trả về danh sách serial bên trong.
/// </summary>
public class PackingService(AppDbContext db) : IPackingService
{
    // ---------- Hộp (Box) ----------

    public async Task<List<BoxSummary>> ListBoxesAsync(string? q)
    {
        var query = db.Boxes.Include(b => b.Items).Include(b => b.Can).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(b => b.Code.ToLower().Contains(term));
        }
        var boxes = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        return boxes.Select(Summarize).ToList();
    }

    public async Task<BoxSummary?> GetBoxAsync(int id)
    {
        var b = await db.Boxes.Include(x => x.Items).Include(x => x.Can).FirstOrDefaultAsync(x => x.Id == id);
        return b == null ? null : Summarize(b);
    }

    public async Task<BoxLookup?> LookupBoxAsync(string code)
    {
        code = (code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return null;
        var b = await db.Boxes.Include(x => x.Items).Include(x => x.Can)
            .FirstOrDefaultAsync(x => x.Code == code);
        if (b == null) return null;
        return new BoxLookup(b.Code, b.Can?.Code, b.Items.Count,
            b.Items.OrderBy(i => i.PackedAt)
                .Select(i => new BoxLookupItem(i.SerialNo, i.ProductName, i.PackedAt)).ToList());
    }

    public async Task<(bool ok, string msg, int id)> CreateBoxAsync(string code, string? secretNo, string? remark)
    {
        code = (code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã hộp.", 0);
        if (await db.Boxes.AnyAsync(b => b.Code == code)) return (false, "Mã hộp đã tồn tại.", 0);
        var box = new Box { Code = code, SecretNo = Trim(secretNo), Remark = Trim(remark) };
        db.Boxes.Add(box); await db.SaveChangesAsync();
        return (true, "Đã tạo hộp.", box.Id);
    }

    public async Task<(bool ok, string msg)> PackSerialAsync(int boxId, string serialNo)
    {
        serialNo = (serialNo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(serialNo)) return (false, "Cần số serial.");

        var box = await db.Boxes.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == boxId);
        if (box == null) return (false, "Không tìm thấy hộp.");
        if (box.Items.Any(i => i.SerialNo == serialNo)) return (false, "Serial này đã có trong hộp.");

        // Serial phải tồn tại trong kho xác thực (ProductUnit).
        var unit = await db.ProductUnits.FirstOrDefaultAsync(u => u.SerialNo == serialNo);
        if (unit == null) return (false, "Serial không có trong kho xác thực.");

        // Serial không được nằm ở hộp khác.
        var inOtherBox = await db.BoxItems.AnyAsync(i => i.SerialNo == serialNo && i.BoxId != boxId);
        if (inOtherBox) return (false, "Serial đã được đóng ở hộp khác.");

        db.BoxItems.Add(new BoxItem { BoxId = boxId, SerialNo = serialNo, ProductName = unit.ProductName });
        await db.SaveChangesAsync();
        return (true, "Đã đóng serial vào hộp.");
    }

    public async Task<(bool ok, string msg)> UnpackSerialAsync(int boxId, string serialNo)
    {
        serialNo = (serialNo ?? "").Trim();
        var item = await db.BoxItems.FirstOrDefaultAsync(i => i.BoxId == boxId && i.SerialNo == serialNo);
        if (item == null) return (false, "Không tìm thấy serial trong hộp.");
        db.BoxItems.Remove(item); await db.SaveChangesAsync();
        return (true, "Đã lấy serial ra khỏi hộp.");
    }

    // ---------- Thùng (Can) ----------

    public async Task<List<CanSummary>> ListCansAsync(string? q)
    {
        var query = db.Cans.Include(c => c.Boxes).ThenInclude(b => b.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.Code.ToLower().Contains(term));
        }
        var cans = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return cans.Select(Summarize).ToList();
    }

    public async Task<CanSummary?> GetCanAsync(int id)
    {
        var c = await db.Cans.Include(x => x.Boxes).ThenInclude(b => b.Items).FirstOrDefaultAsync(x => x.Id == id);
        return c == null ? null : Summarize(c);
    }

    public async Task<(bool ok, string msg, int id)> CreateCanAsync(string code, string? secretNo, string? remark)
    {
        code = (code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã thùng.", 0);
        if (await db.Cans.AnyAsync(c => c.Code == code)) return (false, "Mã thùng đã tồn tại.", 0);
        var can = new Can { Code = code, SecretNo = Trim(secretNo), Remark = Trim(remark) };
        db.Cans.Add(can); await db.SaveChangesAsync();
        return (true, "Đã tạo thùng.", can.Id);
    }

    public async Task<(bool ok, string msg)> PackBoxAsync(int canId, string boxCode)
    {
        boxCode = (boxCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(boxCode)) return (false, "Cần mã hộp.");

        var can = await db.Cans.FirstOrDefaultAsync(c => c.Id == canId);
        if (can == null) return (false, "Không tìm thấy thùng.");

        var box = await db.Boxes.FirstOrDefaultAsync(b => b.Code == boxCode);
        if (box == null) return (false, "Không tìm thấy hộp.");
        if (box.CanId == canId) return (false, "Hộp đã nằm trong thùng này.");
        if (box.CanId != null) return (false, "Hộp đã nằm trong thùng khác.");

        box.CanId = canId;
        await db.SaveChangesAsync();
        return (true, "Đã đóng hộp vào thùng.");
    }

    public async Task<(bool ok, string msg)> UnpackBoxAsync(int canId, string boxCode)
    {
        boxCode = (boxCode ?? "").Trim();
        var box = await db.Boxes.FirstOrDefaultAsync(b => b.Code == boxCode && b.CanId == canId);
        if (box == null) return (false, "Không tìm thấy hộp trong thùng.");
        box.CanId = null;
        await db.SaveChangesAsync();
        return (true, "Đã lấy hộp ra khỏi thùng.");
    }

    private static BoxSummary Summarize(Box b) => new(b, b.Items.Count, b.Can?.Code);
    private static CanSummary Summarize(Can c) => new(c, c.Boxes.Count, c.Boxes.Sum(b => b.Items.Count));
    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
