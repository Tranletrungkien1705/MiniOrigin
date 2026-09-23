using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record TraceTemplateView(TraceTemplate Template, int CteCount, int KdeCount, int CteKdeCount);
public record TraceTemplateDetail(TraceTemplate Template, List<TraceTemplateCte> Ctes,
    List<TraceTemplateKde> Kdes, List<TraceTemplateCteKde> CteKdes);

// Đầu vào 1 sự kiện (CTE) khi tạo/sửa mẫu.
public record TraceCteInput(string Code, string Name, string? ApiLink);
// Đầu vào 1 thành phần dữ liệu (KDE) khi tạo/sửa mẫu.
public record TraceKdeInput(string Code, string Name, string? DataType, string? RefNoList, bool FlagList, bool FlagQuery);
// Đầu vào 1 gán CTE-KDE khi tạo/sửa mẫu.
public record TraceCteKdeInput(string CteCode, string KdeCode, string? ApiLink, bool FlagOsOrgView, bool FlagKey);

public interface ITraceTemplateService
{
    Task<List<TraceTemplateView>> ListAsync(string? q, TraceTemplateStatus? status);
    Task<TraceTemplateDetail?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, string? remark,
        List<TraceCteInput> ctes, List<TraceKdeInput> kdes, List<TraceCteKdeInput> cteKdes);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, string? remark,
        List<TraceCteInput> ctes, List<TraceKdeInput> kdes, List<TraceCteKdeInput> cteKdes);
    Task<(bool ok, string msg)> ApproveAsync(int id);
    Task<(bool ok, string msg)> CancelAsync(int id);
    Task<(bool ok, string msg)> DeleteAsync(int id);
}

/// <summary>
/// Mẫu truy xuất (TemplateNWType) — port từ Mst_TemplateNWType / TplNWT_Mst_CTE / TplNWT_Mst_KDE /
/// TplNWT_CTE_KDE của InBrand (module eTemNN).
/// Luật (theo MstTemplate.Mst_TemplateNWType_CheckDB + Mst_TemplateNWType_Save):
///  - TplNWType bắt buộc + duy nhất theo tenant (CheckDB Flag.No → mã phải CHƯA tồn tại;
///    Flag.Yes → mã phải TỒN TẠI);
///  - TplNWTDesc bắt buộc;
///  - mẫu phải có ≥1 CTE, ≥1 KDE và ≥1 gán CTE-KDE (Save_Input_*NotFound/Invalid);
///  - mã CTE/KDE bắt buộc, không trùng trong cùng 1 mẫu; gán CTE-KDE phải tham chiếu CTE/KDE có trong mẫu;
///  - vòng đời PENDING → APPROVE → CANCEL: chỉ mẫu PENDING mới sửa/xoá/duyệt/huỷ được.
/// </summary>
public class TraceTemplateService(AppDbContext db) : ITraceTemplateService
{
    public async Task<List<TraceTemplateView>> ListAsync(string? q, TraceTemplateStatus? status)
    {
        var query = db.TraceTemplates.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term) || t.Name.ToLower().Contains(term));
        }
        if (status != null) query = query.Where(t => t.Status == status);

        var templates = await query.OrderBy(t => t.Code).ToListAsync();
        var cteCounts = await db.TraceTemplateCtes.GroupBy(x => x.TemplateId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var kdeCounts = await db.TraceTemplateKdes.GroupBy(x => x.TemplateId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var mapCounts = await db.TraceTemplateCteKdes.GroupBy(x => x.TemplateId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return templates.Select(t => new TraceTemplateView(t,
            cteCounts.FirstOrDefault(c => c.Key == t.Id)?.Count ?? 0,
            kdeCounts.FirstOrDefault(c => c.Key == t.Id)?.Count ?? 0,
            mapCounts.FirstOrDefault(c => c.Key == t.Id)?.Count ?? 0)).ToList();
    }

    public async Task<TraceTemplateDetail?> GetAsync(int id)
    {
        var t = await db.TraceTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return null;
        var ctes = await db.TraceTemplateCtes.Where(x => x.TemplateId == id).OrderBy(x => x.Code).ToListAsync();
        var kdes = await db.TraceTemplateKdes.Where(x => x.TemplateId == id).OrderBy(x => x.Code).ToListAsync();
        var maps = await db.TraceTemplateCteKdes.Where(x => x.TemplateId == id).OrderBy(x => x.CteCode).ThenBy(x => x.KdeCode).ToListAsync();
        return new TraceTemplateDetail(t, ctes, kdes, maps);
    }

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, string? remark,
        List<TraceCteInput> ctes, List<TraceKdeInput> kdes, List<TraceCteKdeInput> cteKdes)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã mẫu truy xuất.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên mẫu truy xuất.", 0);
        // CheckDB Flag.No: mã phải CHƯA tồn tại.
        if (await db.TraceTemplates.AnyAsync(t => t.Code == code)) return (false, "Mã mẫu truy xuất đã tồn tại.", 0);

        var (ok, msg, cteList, kdeList, mapList) = ValidateChildren(ctes, kdes, cteKdes);
        if (!ok) return (false, msg, 0);

        var template = new TraceTemplate
        {
            Code = code, Name = name,
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim(),
            Status = TraceTemplateStatus.Pending
        };
        db.TraceTemplates.Add(template); await db.SaveChangesAsync();

        db.TraceTemplateCtes.AddRange(cteList.Select(c => new TraceTemplateCte
        { TemplateId = template.Id, Code = c.Code, Name = c.Name, ApiLink = c.ApiLink, Active = true }));
        db.TraceTemplateKdes.AddRange(kdeList.Select(k => new TraceTemplateKde
        { TemplateId = template.Id, Code = k.Code, Name = k.Name, DataType = k.DataType, RefNoList = k.RefNoList, FlagList = k.FlagList, FlagQuery = k.FlagQuery, Active = true }));
        db.TraceTemplateCteKdes.AddRange(mapList.Select(m => new TraceTemplateCteKde
        { TemplateId = template.Id, CteCode = m.CteCode, KdeCode = m.KdeCode, ApiLink = m.ApiLink, FlagOsOrgView = m.FlagOsOrgView, FlagKey = m.FlagKey }));
        await db.SaveChangesAsync();
        return (true, "Đã tạo mẫu truy xuất.", template.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, string? remark,
        List<TraceCteInput> ctes, List<TraceKdeInput> kdes, List<TraceCteKdeInput> cteKdes)
    {
        var template = await db.TraceTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return (false, "Không tìm thấy mẫu truy xuất.");
        // Chỉ mẫu PENDING mới được sửa (SaveX_InvalidStatus).
        if (template.Status != TraceTemplateStatus.Pending) return (false, "Chỉ mẫu ở trạng thái Chờ duyệt mới được sửa.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên mẫu truy xuất.");

        var (ok, msg, cteList, kdeList, mapList) = ValidateChildren(ctes, kdes, cteKdes);
        if (!ok) return (false, msg);

        template.Name = name;
        template.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();

        // Thay toàn bộ con (mẫu PENDING được phép dựng lại).
        db.TraceTemplateCtes.RemoveRange(db.TraceTemplateCtes.Where(x => x.TemplateId == id));
        db.TraceTemplateKdes.RemoveRange(db.TraceTemplateKdes.Where(x => x.TemplateId == id));
        db.TraceTemplateCteKdes.RemoveRange(db.TraceTemplateCteKdes.Where(x => x.TemplateId == id));
        await db.SaveChangesAsync();

        db.TraceTemplateCtes.AddRange(cteList.Select(c => new TraceTemplateCte
        { TemplateId = id, Code = c.Code, Name = c.Name, ApiLink = c.ApiLink, Active = true }));
        db.TraceTemplateKdes.AddRange(kdeList.Select(k => new TraceTemplateKde
        { TemplateId = id, Code = k.Code, Name = k.Name, DataType = k.DataType, RefNoList = k.RefNoList, FlagList = k.FlagList, FlagQuery = k.FlagQuery, Active = true }));
        db.TraceTemplateCteKdes.AddRange(mapList.Select(m => new TraceTemplateCteKde
        { TemplateId = id, CteCode = m.CteCode, KdeCode = m.KdeCode, ApiLink = m.ApiLink, FlagOsOrgView = m.FlagOsOrgView, FlagKey = m.FlagKey }));
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật mẫu truy xuất.");
    }

    public async Task<(bool ok, string msg)> ApproveAsync(int id)
    {
        var template = await db.TraceTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return (false, "Không tìm thấy mẫu truy xuất.");
        if (template.Status != TraceTemplateStatus.Pending) return (false, "Chỉ mẫu ở trạng thái Chờ duyệt mới được duyệt.");
        template.Status = TraceTemplateStatus.Approve;
        template.ApproveDTime = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, "Đã duyệt mẫu truy xuất.");
    }

    public async Task<(bool ok, string msg)> CancelAsync(int id)
    {
        var template = await db.TraceTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return (false, "Không tìm thấy mẫu truy xuất.");
        if (template.Status != TraceTemplateStatus.Pending) return (false, "Chỉ mẫu ở trạng thái Chờ duyệt mới được huỷ.");
        template.Status = TraceTemplateStatus.Cancel;
        template.CancelDTime = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, "Đã huỷ mẫu truy xuất.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var template = await db.TraceTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return (false, "Không tìm thấy mẫu truy xuất.");
        if (template.Status != TraceTemplateStatus.Pending) return (false, "Chỉ mẫu ở trạng thái Chờ duyệt mới được xoá.");
        db.TraceTemplateCtes.RemoveRange(db.TraceTemplateCtes.Where(x => x.TemplateId == id));
        db.TraceTemplateKdes.RemoveRange(db.TraceTemplateKdes.Where(x => x.TemplateId == id));
        db.TraceTemplateCteKdes.RemoveRange(db.TraceTemplateCteKdes.Where(x => x.TemplateId == id));
        db.TraceTemplates.Remove(template);
        await db.SaveChangesAsync();
        return (true, "Đã xoá mẫu truy xuất.");
    }

    // Kiểm tra & chuẩn hoá bộ con của mẫu (CTE/KDE/gán CTE-KDE).
    private static (bool ok, string msg, List<TraceCteInput> ctes, List<TraceKdeInput> kdes, List<TraceCteKdeInput> maps)
       ValidateChildren(List<TraceCteInput> ctes, List<TraceKdeInput> kdes, List<TraceCteKdeInput> cteKdes)
    {
        ctes ??= new(); kdes ??= new(); cteKdes ??= new();
        if (ctes.Count < 1) return (false, "Mẫu phải có ít nhất 1 sự kiện (CTE).", ctes, kdes, cteKdes);
        if (kdes.Count < 1) return (false, "Mẫu phải có ít nhất 1 thành phần dữ liệu (KDE).", ctes, kdes, cteKdes);
        if (cteKdes.Count < 1) return (false, "Mẫu phải có ít nhất 1 gán CTE-KDE.", ctes, kdes, cteKdes);

        var normCtes = new List<TraceCteInput>();
        var cteCodes = new HashSet<string>();
        foreach (var c in ctes)
        {
            var code = (c.Code ?? "").Trim().ToUpperInvariant();
            var name = (c.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã sự kiện (CTE).", ctes, kdes, cteKdes);
            if (string.IsNullOrWhiteSpace(name)) return (false, "Cần mô tả sự kiện (CTE).", ctes, kdes, cteKdes);
            if (!cteCodes.Add(code)) return (false, $"Mã sự kiện '{code}' bị trùng trong mẫu.", ctes, kdes, cteKdes);
            normCtes.Add(new TraceCteInput(code, name, string.IsNullOrWhiteSpace(c.ApiLink) ? null : c.ApiLink.Trim()));
        }

        var normKdes = new List<TraceKdeInput>();
        var kdeCodes = new HashSet<string>();
        foreach (var k in kdes)
        {
            var code = (k.Code ?? "").Trim().ToUpperInvariant();
            var name = (k.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã thành phần (KDE).", ctes, kdes, cteKdes);
            if (string.IsNullOrWhiteSpace(name)) return (false, "Cần mô tả thành phần (KDE).", ctes, kdes, cteKdes);
            if (!kdeCodes.Add(code)) return (false, $"Mã thành phần '{code}' bị trùng trong mẫu.", ctes, kdes, cteKdes);
            normKdes.Add(new TraceKdeInput(code, name,
                string.IsNullOrWhiteSpace(k.DataType) ? null : k.DataType.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(k.RefNoList) ? null : k.RefNoList.Trim(), k.FlagList, k.FlagQuery));
        }

        var normMaps = new List<TraceCteKdeInput>();
        var pairs = new HashSet<string>();
        foreach (var m in cteKdes)
        {
            var cteCode = (m.CteCode ?? "").Trim().ToUpperInvariant();
            var kdeCode = (m.KdeCode ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(cteCode) || string.IsNullOrWhiteSpace(kdeCode))
                return (false, "Gán CTE-KDE cần đủ mã CTE và mã KDE.", ctes, kdes, cteKdes);
            if (!cteCodes.Contains(cteCode)) return (false, $"Gán CTE-KDE tham chiếu sự kiện '{cteCode}' không có trong mẫu.", ctes, kdes, cteKdes);
            if (!kdeCodes.Contains(kdeCode)) return (false, $"Gán CTE-KDE tham chiếu thành phần '{kdeCode}' không có trong mẫu.", ctes, kdes, cteKdes);
            if (!pairs.Add(cteCode + "|" + kdeCode)) return (false, $"Cặp CTE-KDE '{cteCode}-{kdeCode}' bị gán trùng.", ctes, kdes, cteKdes);
            normMaps.Add(new TraceCteKdeInput(cteCode, kdeCode,
                string.IsNullOrWhiteSpace(m.ApiLink) ? null : m.ApiLink.Trim(), m.FlagOsOrgView, m.FlagKey));
        }

        return (true, "", normCtes, normKdes, normMaps);
    }
}
