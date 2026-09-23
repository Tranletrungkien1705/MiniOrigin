using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test xác thực sản phẩm chính hãng (port từ BrandPositioning/piController): serial+PIN, kích hoạt bảo hành lần đầu.</summary>
public class AuthenticityServiceTests
{
    private static (AppDbContext db, IAuthenticityService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new AuthenticityService(db), conn);
    }

    [Fact]
    public async Task Create_RequiresSerialAndPin()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", "123", null, null, null, null, 12)).ok);
            Assert.False((await svc.CreateAsync("S1", "", null, null, null, null, 12)).ok);
            Assert.True((await svc.CreateAsync("S1", "123", null, null, null, null, 12)).ok);
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateSerial()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("S1", "123", null, null, null, null, 12);
            var (ok, msg, _) = await svc.CreateAsync("S1", "999", null, null, null, null, 12);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task Verify_WrongPin_Fails()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("S1", "123", null, null, null, null, 12);
            var res = await svc.VerifyAsync("S1", "000");
            Assert.False(res.Ok);
            Assert.Contains("không đúng", res.Message);
        }
    }

    [Fact]
    public async Task Verify_CorrectPin_SucceedsAndCounts()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("S1", "123", null, null, null, null, 12);
            var res = await svc.VerifyAsync("S1", "123");
            Assert.True(res.Ok);
            Assert.False(res.Activated);
            var u = await svc.LookupAsync("S1");
            Assert.Equal(1, u!.VerifyCount);
        }
    }

    [Fact]
    public async Task Verify_UnknownSerial_Fails()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var res = await svc.VerifyAsync("NOPE", "123");
            Assert.False(res.Ok);
            Assert.Contains("Không tìm thấy", res.Message);
        }
    }

    [Fact]
    public async Task Activate_SetsWarrantyAndCustomer()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("S1", "123", null, null, null, null, 24);
            var res = await svc.ActivateAsync("S1", "123", "Nguyễn Văn A", "0900000", "Hà Nội");
            Assert.True(res.Ok);
            Assert.True(res.Activated);
            Assert.NotNull(res.WarrantyDateStart);
            Assert.NotNull(res.WarrantyDateEnd);
            Assert.Equal(res.WarrantyDateStart!.Value.AddMonths(24), res.WarrantyDateEnd!.Value);

            var u = await svc.LookupAsync("S1");
            Assert.True(u!.Activated);
        }
    }

    [Fact]
    public async Task Activate_WrongPin_DoesNotActivate()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("S1", "123", null, null, null, null, 12);
            var res = await svc.ActivateAsync("S1", "999", null, null, null);
            Assert.False(res.Ok);
            Assert.False((await svc.LookupAsync("S1"))!.Activated);
        }
    }

    [Fact]
    public async Task Activate_Twice_KeepsFirstDate()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("S1", "123", null, null, null, null, 12);
            var first = await svc.ActivateAsync("S1", "123", null, null, null);
            var second = await svc.ActivateAsync("S1", "123", null, null, null);
            Assert.True(second.Ok);
            Assert.Equal(first.WarrantyDateStart, second.WarrantyDateStart);
        }
    }
}
