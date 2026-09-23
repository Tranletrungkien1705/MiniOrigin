using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test Lịch sử tra cứu (port từ Rpt_SearchHis): ghi lượt tra cứu, chuẩn hoá mã, lọc theo loại/từ khoá, thống kê.</summary>
public class SearchLogServiceTests
{
    private static (AppDbContext db, ISearchLogService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new SearchLogService(db), conn);
    }

    [Fact]
    public async Task Record_TrimsCodeAndStampsTime()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var log = await svc.RecordAsync("  VGC-2026-0001  ", SearchType.Authenticity, true, "  khach-hcm ", " VISIT-1 ");
            Assert.Equal("VGC-2026-0001", log.SearchCode);
            Assert.Equal("khach-hcm", log.UserCode);
            Assert.Equal("VISIT-1", log.VisitId);
            Assert.True(log.Found);
            Assert.True(log.SearchDTime > DateTime.UtcNow.AddMinutes(-1));
        }
    }

    [Fact]
    public async Task Record_BlankUserAndVisitBecomeNull()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var log = await svc.RecordAsync("X", SearchType.Trace, false, "   ", "");
            Assert.Null(log.UserCode);
            Assert.Null(log.VisitId);
            Assert.False(log.Found);
        }
    }

    [Fact]
    public async Task List_FiltersByTypeAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.RecordAsync("VGC-2026-0001", SearchType.Authenticity, true, "khach-hcm");
            await svc.RecordAsync("GAO-ST25-2026-A", SearchType.Trace, true, "khach-hn");
            await svc.RecordAsync("BOX-VGC-0001", SearchType.Box, true, "khach-hcm");

            Assert.Equal(3, (await svc.ListAsync(null, null)).Count);
            Assert.Single(await svc.ListAsync(null, SearchType.Trace));
            Assert.Equal(2, (await svc.ListAsync("khach-hcm", null)).Count);
            Assert.Single(await svc.ListAsync("GAO", null));
        }
    }

    [Fact]
    public async Task List_OrdersByMostRecentFirst()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var first = await svc.RecordAsync("A", SearchType.Trace, true);
            var second = await svc.RecordAsync("B", SearchType.Trace, true);
            var list = await svc.ListAsync(null, null);
            Assert.Equal(second.Id, list[0].Id);
            Assert.Equal(first.Id, list[1].Id);
        }
    }

    [Fact]
    public async Task Stats_CountsByTypeAndFound()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.RecordAsync("A", SearchType.Authenticity, true);
            await svc.RecordAsync("B", SearchType.Authenticity, false);
            await svc.RecordAsync("C", SearchType.Trace, true);
            await svc.RecordAsync("D", SearchType.Box, true);

            var s = await svc.StatsAsync();
            Assert.Equal(4, s.Total);
            Assert.Equal(2, s.Authenticity);
            Assert.Equal(1, s.Trace);
            Assert.Equal(1, s.Box);
            Assert.Equal(3, s.Found);
            Assert.Equal(1, s.NotFound);
        }
    }
}