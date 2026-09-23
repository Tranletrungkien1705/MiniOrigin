using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test lần xuất ghép (port từ Inv_VerifiedIDInOut): tạo batch, quét tem OK/NG, chốt, huỷ.</summary>
public class VerifyBatchServiceTests
{
    private static (AppDbContext db, IVerifyBatchService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new VerifyBatchService(db), conn);
    }

    private static async Task SeedUnit(AppDbContext db, string serial)
        => db.ProductUnits.Add(new ProductUnit { SerialNo = serial, SecretNo = "123", ProductName = "SP" });

    [Fact]
    public async Task Create_RequiresProductName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", null, null, null, null, null, null, 5)).ok);
            Assert.True((await svc.CreateAsync("Gạch", null, null, null, null, null, null, 5)).ok);
        }
    }

    [Fact]
    public async Task Scan_ValidTem_CountsOK()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "T1"); await db.SaveChangesAsync();
            var (_, _, id) = await svc.CreateAsync("Gạch", "PXK1", null, null, null, null, null, 2);
            var (ok, _, isNG) = await svc.ScanAsync(id, "T1", "123", "BOX1", "KH");
            Assert.True(ok); Assert.False(isNG);
            var s = await svc.GetAsync(id);
            Assert.Equal(1, s!.QtyOK);
            Assert.Equal(1, s.Batch.QtyInit);
        }
    }

    [Fact]
    public async Task Scan_UnknownTem_MarksNG()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("Gạch", null, null, null, null, null, null, 2);
            var (ok, _, isNG) = await svc.ScanAsync(id, "NOPE", null, null, null);
            Assert.True(ok); Assert.True(isNG);
            var s = await svc.GetAsync(id);
            Assert.Equal(0, s!.QtyOK);
            Assert.Equal(1, s.QtyNG);
        }
    }

    [Fact]
    public async Task Scan_DuplicateInSameBatch_Rejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "T1"); await db.SaveChangesAsync();
            var (_, _, id) = await svc.CreateAsync("Gạch", null, null, null, null, null, null, 2);
            await svc.ScanAsync(id, "T1", null, null, null);
            var (ok, msg, _) = await svc.ScanAsync(id, "T1", null, null, null);
            Assert.False(ok);
            Assert.Contains("đã được quét", msg);
        }
    }

    [Fact]
    public async Task Scan_TemUsedInOtherBatch_MarksNG()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "T1"); await db.SaveChangesAsync();
            var (_, _, b1) = await svc.CreateAsync("Gạch", null, null, null, null, null, null, 2);
            var (_, _, b2) = await svc.CreateAsync("Gạch", null, null, null, null, null, null, 2);
            await svc.ScanAsync(b1, "T1", null, null, null);
            var (ok, _, isNG) = await svc.ScanAsync(b2, "T1", null, null, null);
            Assert.True(ok); Assert.True(isNG);
        }
    }

    [Fact]
    public async Task Merge_SetsStatusAndVerifiedCount()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "T1"); await db.SaveChangesAsync();
            var (_, _, id) = await svc.CreateAsync("Gạch", null, null, null, null, null, null, 2);
            await svc.ScanAsync(id, "T1", null, null, null);
            await svc.ScanAsync(id, "NOPE", null, null, null);
            var (ok, _) = await svc.MergeAsync(id);
            Assert.True(ok);
            var s = await svc.GetAsync(id);
            Assert.Equal(VerifyBatchStatus.Merged, s!.Batch.Status);
            Assert.Equal(1, s.Batch.QtyVerified);
        }
    }

    [Fact]
    public async Task Merge_EmptyBatch_Rejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("Gạch", null, null, null, null, null, null, 2);
            var (ok, msg) = await svc.MergeAsync(id);
            Assert.False(ok);
            Assert.Contains("Chưa có tem", msg);
        }
    }

    [Fact]
    public async Task Scan_AfterMerge_Rejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "T1"); await db.SaveChangesAsync();
            var (_, _, id) = await svc.CreateAsync("Gạch", null, null, null, null, null, null, 2);
            await svc.ScanAsync(id, "T1", null, null, null);
            await svc.MergeAsync(id);
            var (ok, msg, _) = await svc.ScanAsync(id, "T1", null, null, null);
            Assert.False(ok);
            Assert.Contains("đã chốt", msg);
        }
    }

    [Fact]
    public async Task Cancel_SetsCancelled()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("Gạch", null, null, null, null, null, null, 2);
            var (ok, _) = await svc.CancelAsync(id);
            Assert.True(ok);
            Assert.Equal(VerifyBatchStatus.Cancelled, (await svc.GetAsync(id))!.Batch.Status);
        }
    }
}
