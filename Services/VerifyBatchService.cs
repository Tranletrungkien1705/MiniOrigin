using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

// Tổng hợp 1 lần xuất ghép: số kế hoạch / thực nhập / ghép được / lỗi / còn lại.
public record BatchSummary(VerifyBatch Batch, int QtyOK, int QtyNG, int QtyRemain);

public interface IVerifyBatchService
{
    Task<List<BatchSummary>> ListAsync(string? q);
    Task<BatchSummary?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string productName, string? refNo, string? refNoSys,
        string? transportType, string? plateNo, string? receivePlace, string? invOutType, int qtyPlan);
    // Quét 1 tem (IDNo) vào lần ghép: hợp lệ nếu tem tồn tại trong kho xác thực và chưa dùng ở lần ghép khác.
    Task<(bool ok, string msg, bool isNG)> ScanAsync(int batchId, string idNo, string? pin, string? boxNo, string? customerName);
    Task<(bool ok, string msg)> MergeAsync(int batchId);
    Task<(bool ok, string msg)> CancelAsync(int batchId);
}

/// <summary>
/// Lần xuất ghép (batch xác thực) — port từ module Inv_VerifiedIDInOut của InBrand
/// (controller InvVerifiedIDInOutController: WA_Inv_VerifiedIDInOut_Merge / _Cancel; bảng Inv_VerifiedIDInOut + Inv_InventoryVerifiedID).
/// Luật: 1 lần ghép gom nhiều tem (IDNo) cho 1 sản phẩm gắn với 1 đơn hàng/phiếu xuất;
/// mỗi tem phải tồn tại trong kho xác thực (ProductUnit) và không được ghép trùng ở lần khác;
/// tem không hợp lệ bị đánh dấu lỗi (FlagNG) và không tính vào số ghép được;
/// khi chốt (Merge) số ghép được = số tem OK, trạng thái chuyển Merged; huỷ (Cancel) trả về Cancelled.
/// </summary>
public class VerifyBatchService(AppDbContext db) : IVerifyBatchService
{
    public async Task<List<BatchSummary>> ListAsync(string? q)
    {
        var query = db.VerifyBatches.Include(b => b.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(b => b.Code.ToLower().Contains(term)
                || b.ProductName.ToLower().Contains(term)
                || (b.RefNo != null && b.RefNo.ToLower().Contains(term)));
        }
        var batches = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        return batches.Select(Summarize).ToList();
    }

    public async Task<BatchSummary?> GetAsync(int id)
    {
        var b = await db.VerifyBatches.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        return b == null ? null : Summarize(b);
    }

    public async Task<(bool ok, string msg, int id)> CreateAsync(string productName, string? refNo, string? refNoSys,
        string? transportType, string? plateNo, string? receivePlace, string? invOutType, int qtyPlan)
    {
        productName = (productName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(productName)) return (false, "Cần tên sản phẩm.", 0);
        if (qtyPlan < 0) return (false, "Số lượng kế hoạch không hợp lệ.", 0);

        var batch = new VerifyBatch
        {
            Code = await NextCodeAsync(),
            ProductName = productName,
            RefNo = Trim(refNo), RefNoSys = Trim(refNoSys),
            TransportType = Trim(transportType), PlateNo = Trim(plateNo),
            ReceivePlace = Trim(receivePlace), InvOutType = Trim(invOutType),
            QtyPlan = qtyPlan, Status = VerifyBatchStatus.Open
        };
        db.VerifyBatches.Add(batch); await db.SaveChangesAsync();
        return (true, "Đã tạo lần xuất ghép.", batch.Id);
    }

    public async Task<(bool ok, string msg, bool isNG)> ScanAsync(int batchId, string idNo, string? pin, string? boxNo, string? customerName)
    {
        idNo = (idNo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(idNo)) return (false, "Cần mã tem (IDNo).", false);

        var batch = await db.VerifyBatches.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch == null) return (false, "Không tìm thấy lần xuất ghép.", false);
        if (batch.Status != VerifyBatchStatus.Open) return (false, "Lần xuất ghép đã chốt hoặc đã huỷ.", false);
        if (batch.Items.Any(i => i.IdNo == idNo)) return (false, "Tem này đã được quét trong lần ghép.", false);

        // Tem phải tồn tại trong kho xác thực (ProductUnit) — nếu không, ghi nhận tem lỗi.
        var unit = await db.ProductUnits.FirstOrDefaultAsync(u => u.SerialNo == idNo);
        bool isNG = unit == null;
        string? reason = isNG ? "Tem không có trong kho xác thực." : null;

        // Tem đã ghép ở lần xuất ghép khác (chưa huỷ) → lỗi.
        if (!isNG)
        {
            var usedElsewhere = await db.VerifyBatchItems
                .AnyAsync(i => i.IdNo == idNo && i.BatchId != batchId && !i.FlagNG
                    && i.Batch!.Status != VerifyBatchStatus.Cancelled);
            if (usedElsewhere) { isNG = true; reason = "Tem đã được ghép ở lần xuất khác."; }
        }

        db.VerifyBatchItems.Add(new VerifyBatchItem
        {
            BatchId = batchId, IdNo = idNo, Pin = Trim(pin),
            ProductName = unit?.ProductName ?? batch.ProductName,
            BoxNo = Trim(boxNo), CustomerName = Trim(customerName),
            FlagNG = isNG, ErrorReason = reason
        });
        batch.QtyInit++;
        if (!isNG) batch.QtyVerified++;
        await db.SaveChangesAsync();

        return isNG
            ? (true, $"Tem lỗi: {reason}", true)
            : (true, "Đã ghép tem.", false);
    }

    public async Task<(bool ok, string msg)> MergeAsync(int batchId)
    {
        var batch = await db.VerifyBatches.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch == null) return (false, "Không tìm thấy lần xuất ghép.");
        if (batch.Status != VerifyBatchStatus.Open) return (false, "Lần xuất ghép đã chốt hoặc đã huỷ.");
        if (batch.Items.Count == 0) return (false, "Chưa có tem nào để chốt.");

        batch.QtyVerified = batch.Items.Count(i => !i.FlagNG);
        batch.Status = VerifyBatchStatus.Merged;
        batch.MergedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, $"Đã chốt lần xuất ghép: {batch.QtyVerified}/{batch.QtyInit} tem hợp lệ.");
    }

    public async Task<(bool ok, string msg)> CancelAsync(int batchId)
    {
        var batch = await db.VerifyBatches.FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch == null) return (false, "Không tìm thấy lần xuất ghép.");
        if (batch.Status == VerifyBatchStatus.Cancelled) return (false, "Lần xuất ghép đã được huỷ trước đó.");
        batch.Status = VerifyBatchStatus.Cancelled;
        await db.SaveChangesAsync();
        return (true, "Đã huỷ lần xuất ghép.");
    }

    private async Task<string> NextCodeAsync()
    {
        var count = await db.VerifyBatches.IgnoreQueryFilters().CountAsync();
        return $"IVIDINOUTNO.{DateTime.UtcNow:yyMMdd}.{count + 1:D4}";
    }

    private static BatchSummary Summarize(VerifyBatch b)
    {
        var ok = b.Items.Count(i => !i.FlagNG);
        var ng = b.Items.Count(i => i.FlagNG);
        var remain = b.QtyPlan - ok;
        return new BatchSummary(b, ok, ng, remain < 0 ? 0 : remain);
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
