using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test truy xuất GS1: tạo lô, ghi sự kiện CTE + KDE, liên kết phả hệ lô, tra cứu đệ quy theo mã.</summary>
public class OriginServiceTests
{
    private static (AppDbContext db, IOriginService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new OriginService(db), conn);
    }

    [Fact]
    public async Task CreateLot_And_GetByCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, id) = await svc.CreateLotAsync("LOT-A", "Cà phê", "kg", 100, null);
            Assert.True(ok);
            var l = await svc.GetLotAsync(id);
            Assert.Equal("Cà phê", l!.ProductName);
            var t = await svc.TraceByCodeAsync("LOT-A");
            Assert.NotNull(t);
        }
    }

    [Fact]
    public async Task AddEvent_WithCteAndKde()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, lotId) = await svc.CreateLotAsync("LOT-B", "Trà", "kg", 50, null);
            var (_, _, cteId) = await svc.CreateCteAsync(new Cte { Code = "HARVEST", Name = "Thu hoạch" });
            await svc.AddKdeAsync(new KdeDef { CteId = cteId, Key = "temp", Label = "Nhiệt độ", Unit = "°C" });
            var (ok, _) = await svc.AddEventAsync(lotId, cteId, null, DateTime.Now, "NV A", null, new() { ["temp"] = "25" });
            Assert.True(ok);
            var l = await svc.GetLotAsync(lotId);
            Assert.Contains(l!.Events, e => e.CteName == "Thu hoạch");
        }
    }

    [Fact]
    public async Task LinkLot_BuildsGenealogy()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, parent) = await svc.CreateLotAsync("RAW-1", "Sữa tươi", "lít", 1000, null);
            var (_, _, child) = await svc.CreateLotAsync("FIN-1", "Sữa chua", "hộp", 500, null);
            var (ok, _) = await svc.LinkLotAsync(child, "RAW-1", 800);
            Assert.True(ok);
            var parents = await svc.ParentLotsAsync(child);
            Assert.Contains(parents, p => p.Code == "RAW-1");
        }
    }

    [Fact]
    public async Task Trace_IncludesParents_Recursively()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, parent) = await svc.CreateLotAsync("RAW-2", "Bột mì", "kg", 100, null);
            var (_, _, child) = await svc.CreateLotAsync("FIN-2", "Bánh mì", "cái", 200, null);
            await svc.LinkLotAsync(child, "RAW-2", 50);
            var t = await svc.TraceByCodeAsync("FIN-2");
            Assert.NotNull(t);
            Assert.Contains(t!.Parents, p => p.Lot.Code == "RAW-2");
        }
    }

    [Fact]
    public async Task Dashboard_CountsLotsAndCtes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateLotAsync("L1", "SP1", "kg", 10, null);
            await svc.CreateCteAsync(new Cte { Code = "C1", Name = "SX" });
            var d = await svc.DashboardAsync();
            Assert.Equal(1, d.Lots);
            Assert.True(d.Ctes >= 1);
        }
    }
}
