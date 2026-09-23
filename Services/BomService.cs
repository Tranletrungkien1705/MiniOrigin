using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record BomTypeView(BomType Type, int BomCount);
public record BomView(Bom Bom, string ParentProductName, string BomTypeCode, int LineCount);
public record BomDetail(Bom Bom, string ParentProductName, string BomTypeCode, List<BomLineView> Lines);
public record BomLineView(BomLine Line, string ComponentName, string ComponentCode);

public interface IBomService
{
    // Loại BOM (Mst_BOMType)
    Task<List<BomTypeView>> ListTypesAsync(string? q, bool? activeOnly);
    Task<(bool ok, string msg, int id)> CreateTypeAsync(string code, string? description, bool active);
    Task<(bool ok, string msg)> UpdateTypeAsync(int id, string? description, bool active);
    Task<(bool ok, string msg)> DeleteTypeAsync(int id);

    // BOM (Mst_BOM + Mst_BOMDtl)
    Task<List<BomView>> ListAsync(string? q, BomStatus? status);
    Task<BomDetail?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, int parentProductId, int bomTypeId, bool isDefault, string? remark, List<BomLineInput> lines);
    Task<(bool ok, string msg)> UpdateAsync(int id, bool isDefault, string? remark);
    Task<(bool ok, string msg)> ApproveAsync(int id);
    Task<(bool ok, string msg)> FinishAsync(int id);
    Task<(bool ok, string msg)> DeleteAsync(int id);
}

public record BomLineInput(int ComponentProductId, decimal Qty, string? Unit);

/// <summary>
/// Định mức nguyên vật liệu (Bill of Materials) — port từ Mst_BOM / Mst_BOMDtl / Mst_BOMType của InBrand.
/// Luật (theo MstBOMManager + MstBOMProvider + MstBOMDtlProvider):
///  - mã BOM bắt buộc + duy nhất theo tenant; sản phẩm cha phải TỒN TẠI; loại BOM phải TỒN TẠI & ĐANG HOẠT ĐỘNG;
///  - BOM phải có ÍT NHẤT 1 dòng thành phần; mỗi dòng Qty >= 0; thành phần không trùng trong cùng BOM;
///  - chặn vòng lặp: thành phần không được là chính sản phẩm cha (A→A) và không tạo chu trình trực tiếp (A→B, B→A);
///  - MỖI SẢN PHẨM CHA CHỈ CÓ TỐI ĐA 1 BOM MẶC ĐỊNH đang ở trạng thái APPROVE;
///  - vòng đời: PENDING (tạo/sửa/xoá) → APPROVE (duyệt) → FINISH (hoàn tất); chỉ sửa/xoá/duyệt khi đang PENDING.
/// </summary>
public class BomService(AppDbContext db) : IBomService
{
    // ---------- Loại BOM (Mst_BOMType) ----------

    public async Task<List<BomTypeView>> ListTypesAsync(string? q, bool? activeOnly)
    {
        var query = db.BomTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term) || (t.Description ?? "").ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(t => t.Active);

        var types = await query.OrderBy(t => t.Code).ToListAsync();
        var counts = await db.Boms.GroupBy(b => b.BomTypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TypeId, x => x.Count);
        return types.Select(t => new BomTypeView(t, counts.TryGetValue(t.Id, out var c) ? c : 0)).ToList();
    }

    public async Task<(bool ok, string msg, int id)> CreateTypeAsync(string code, string? description, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã loại BOM.", 0);
        if (await db.BomTypes.AnyAsync(t => t.Code == code)) return (false, "Mã loại BOM đã tồn tại.", 0);
        var type = new BomType { Code = code, Description = description?.Trim(), Active = active };
        db.BomTypes.Add(type); await db.SaveChangesAsync();
        return (true, "Đã tạo loại BOM.", type.Id);
    }

    public async Task<(bool ok, string msg)> UpdateTypeAsync(int id, string? description, bool active)
    {
        var type = await db.BomTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại BOM.");
        type.Description = description?.Trim(); type.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật loại BOM.");
    }

    public async Task<(bool ok, string msg)> DeleteTypeAsync(int id)
    {
        var type = await db.BomTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại BOM.");
        if (await db.Boms.AnyAsync(b => b.BomTypeId == id))
            return (false, "Không thể xoá: còn BOM dùng loại này.");
        db.BomTypes.Remove(type); await db.SaveChangesAsync();
        return (true, "Đã xoá loại BOM.");
    }

    // ---------- BOM (Mst_BOM + Mst_BOMDtl) ----------

    public async Task<List<BomView>> ListAsync(string? q, BomStatus? status)
    {
        var query = db.Boms.Include(b => b.ParentProduct).Include(b => b.BomType).Include(b => b.Lines).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(b => b.Code.ToLower().Contains(term)
                || (b.ParentProduct != null && b.ParentProduct.Name.ToLower().Contains(term)));
        }
        if (status.HasValue) query = query.Where(b => b.Status == status.Value);

        var boms = await query.OrderBy(b => b.Code).ToListAsync();
        return boms.Select(b => new BomView(b, b.ParentProduct?.Name ?? "", b.BomType?.Code ?? "", b.Lines.Count)).ToList();
    }

    public async Task<BomDetail?> GetAsync(int id)
    {
        var bom = await db.Boms.Include(b => b.ParentProduct).Include(b => b.BomType)
            .Include(b => b.Lines).ThenInclude(l => l.ComponentProduct)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null) return null;
        var lines = bom.Lines.OrderBy(l => l.ComponentProduct?.Code)
            .Select(l => new BomLineView(l, l.ComponentProduct?.Name ?? "", l.ComponentProduct?.Code ?? "")).ToList();
        return new BomDetail(bom, bom.ParentProduct?.Name ?? "", bom.BomType?.Code ?? "", lines);
    }

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, int parentProductId, int bomTypeId, bool isDefault, string? remark, List<BomLineInput> lines)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã BOM.", 0);
        if (await db.Boms.AnyAsync(b => b.Code == code)) return (false, "Mã BOM đã tồn tại.", 0);

        var parent = await db.Products.FirstOrDefaultAsync(p => p.Id == parentProductId);
        if (parent == null) return (false, "Không tìm thấy sản phẩm cha.", 0);

        var type = await db.BomTypes.FirstOrDefaultAsync(t => t.Id == bomTypeId);
        if (type == null) return (false, "Không tìm thấy loại BOM.", 0);
        if (!type.Active) return (false, "Loại BOM đang ngừng hoạt động.", 0);

        if (lines == null || lines.Count < 1) return (false, "BOM phải có ít nhất 1 dòng thành phần.", 0);

        // Kiểm tra từng dòng: thành phần tồn tại, Qty >= 0, không trùng, không phải chính sản phẩm cha.
        var seen = new HashSet<int>();
        foreach (var l in lines)
        {
            if (l.Qty < 0) return (false, "Số lượng thành phần không được âm.", 0);
            if (l.ComponentProductId == parentProductId)
                return (false, "Thành phần không được là chính sản phẩm cha (vòng lặp A→A).", 0);
            if (!seen.Add(l.ComponentProductId))
                return (false, "Thành phần bị lặp trong cùng một BOM.", 0);
            if (!await db.Products.AnyAsync(p => p.Id == l.ComponentProductId))
                return (false, "Không tìm thấy sản phẩm thành phần.", 0);
        }

        // Chặn chu trình trực tiếp: nếu 1 thành phần đang là cha của sản phẩm cha này (A→B, B→A).
        var componentIds = lines.Select(l => l.ComponentProductId).ToList();
        var cycle = await db.Boms.Where(b => componentIds.Contains(b.ParentProductId))
            .SelectMany(b => b.Lines)
            .AnyAsync(dl => dl.ComponentProductId == parentProductId);
        if (cycle) return (false, "Tạo chu trình BOM (A→B, B→A) — không hợp lệ.", 0);

        var bom = new Bom
        {
            Code = code, ParentProductId = parentProductId, BomTypeId = bomTypeId,
            IsDefault = isDefault, Remark = remark?.Trim(), Status = BomStatus.Pending
        };
        db.Boms.Add(bom); await db.SaveChangesAsync();

        foreach (var l in lines)
            db.BomLines.Add(new BomLine
            {
                BomId = bom.Id, ComponentProductId = l.ComponentProductId,
                Qty = l.Qty, Unit = l.Unit?.Trim(), ValCost = 0, Status = BomStatus.Pending
            });
        await db.SaveChangesAsync();

        // Mỗi sản phẩm cha chỉ có tối đa 1 BOM mặc định đang APPROVE.
        var (ok, msg) = await CheckDefaultFlagAsync();
        if (!ok) { db.Boms.Remove(bom); await db.SaveChangesAsync(); return (false, msg, 0); }

        return (true, "Đã tạo BOM.", bom.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, bool isDefault, string? remark)
    {
        var bom = await db.Boms.FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null) return (false, "Không tìm thấy BOM.");
        if (bom.Status != BomStatus.Pending) return (false, "Chỉ sửa được BOM đang ở trạng thái Chờ duyệt.");
        bom.IsDefault = isDefault; bom.Remark = remark?.Trim();
        await db.SaveChangesAsync();
        var (ok, msg) = await CheckDefaultFlagAsync();
        return ok ? (true, "Đã cập nhật BOM.") : (false, msg);
    }

    public async Task<(bool ok, string msg)> ApproveAsync(int id)
    {
        var bom = await db.Boms.Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null) return (false, "Không tìm thấy BOM.");
        if (bom.Status != BomStatus.Pending) return (false, "Chỉ duyệt được BOM đang ở trạng thái Chờ duyệt.");
        if (bom.Lines.Count < 1) return (false, "BOM phải có ít nhất 1 dòng thành phần.");

        bom.Status = BomStatus.Approve;
        bom.ApproveDTime = DateTime.UtcNow;
        foreach (var l in bom.Lines) l.Status = BomStatus.Approve;
        await db.SaveChangesAsync();

        var (ok, msg) = await CheckDefaultFlagAsync();
        return ok ? (true, "Đã duyệt BOM.") : (false, msg);
    }

    public async Task<(bool ok, string msg)> FinishAsync(int id)
    {
        var bom = await db.Boms.Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null) return (false, "Không tìm thấy BOM.");
        if (bom.Status != BomStatus.Approve) return (false, "Chỉ hoàn tất được BOM đã duyệt.");
        bom.Status = BomStatus.Finish;
        bom.FinishDTime = DateTime.UtcNow;
        foreach (var l in bom.Lines) l.Status = BomStatus.Finish;
        await db.SaveChangesAsync();
        return (true, "Đã hoàn tất BOM.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var bom = await db.Boms.Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null) return (false, "Không tìm thấy BOM.");
        if (bom.Status != BomStatus.Pending) return (false, "Chỉ xoá được BOM đang ở trạng thái Chờ duyệt.");
        db.BomLines.RemoveRange(bom.Lines);
        db.Boms.Remove(bom);
        await db.SaveChangesAsync();
        return (true, "Đã xoá BOM.");
    }

    // Mỗi sản phẩm cha chỉ có tối đa 1 BOM mặc định đang APPROVE (Mst_BOM_CheckDBFlagDefault).
    private async Task<(bool ok, string msg)> CheckDefaultFlagAsync()
    {
        var dup = await db.Boms
            .Where(b => b.IsDefault && b.Status == BomStatus.Approve)
            .GroupBy(b => b.ParentProductId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .FirstOrDefaultAsync();
        if (dup != 0)
        {
            var name = await db.Products.Where(p => p.Id == dup).Select(p => p.Name).FirstOrDefaultAsync();
            return (false, $"Sản phẩm \"{name ?? dup.ToString()}\" đã có BOM mặc định được duyệt — chỉ cho phép 1.");
        }
        return (true, "");
    }
}
