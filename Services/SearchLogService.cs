using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

// Thống kê lịch sử tra cứu theo loại.
public record SearchLogStats(int Total, int Authenticity, int Trace, int Box, int Found, int NotFound);

public interface ISearchLogService
{
    // Ghi 1 lượt tra cứu (chuẩn hoá mã, gán thời điểm). Trả về bản ghi đã lưu.
    Task<SearchLog> RecordAsync(string searchCode, SearchType type, bool found, string? userCode = null, string? visitId = null);
    Task<List<SearchLog>> ListAsync(string? q, SearchType? type, int take = 200);
    Task<SearchLogStats> StatsAsync();
}

/// <summary>
/// Lịch sử tra cứu — port từ Rpt_SearchHis của InBrand (RptSearchHisManager: Rpt_SearchHis_Add/_Get).
/// Luật: mỗi lượt tra cứu ghi lại mã (SearchCode), người tra cứu (UserCode), loại (SearchType),
/// thời điểm (SearchDTime) và mã phiên (SkycicVisitID); mã được chuẩn hoá khoảng trắng trước khi lưu.
/// </summary>
public class SearchLogService(AppDbContext db) : ISearchLogService
{
    public async Task<SearchLog> RecordAsync(string searchCode, SearchType type, bool found, string? userCode = null, string? visitId = null)
    {
        var log = new SearchLog
        {
            SearchCode = (searchCode ?? "").Trim(),
            UserCode = string.IsNullOrWhiteSpace(userCode) ? null : userCode.Trim(),
            Type = type,
            Found = found,
            VisitId = string.IsNullOrWhiteSpace(visitId) ? null : visitId.Trim(),
            SearchDTime = DateTime.UtcNow
        };
        db.SearchLogs.Add(log);
        await db.SaveChangesAsync();
        return log;
    }

    public Task<List<SearchLog>> ListAsync(string? q, SearchType? type, int take = 200)
    {
        var query = db.SearchLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.SearchCode.ToLower().Contains(term)
                || (x.UserCode != null && x.UserCode.ToLower().Contains(term)));
        }
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        return query.OrderByDescending(x => x.SearchDTime).Take(take).ToListAsync();
    }

    public async Task<SearchLogStats> StatsAsync()
    {
        var all = await db.SearchLogs.ToListAsync();
        return new SearchLogStats(
            all.Count,
            all.Count(x => x.Type == SearchType.Authenticity),
            all.Count(x => x.Type == SearchType.Trace),
            all.Count(x => x.Type == SearchType.Box),
            all.Count(x => x.Found),
            all.Count(x => !x.Found));
    }
}