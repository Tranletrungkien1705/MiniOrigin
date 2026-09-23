using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test đóng hộp / đóng thùng (port từ module Box/Can): tạo hộp, đóng serial, đóng hộp vào thùng, tra cứu.</summary>
public class PackingServiceTests
{
    private static (AppDbContext db, IPackingService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new PackingService(db), conn);
    }

    private static async Task SeedUnit(AppDbContext db, string serial)
        => db.ProductUnits.Add(new ProductUnit { SerialNo = serial, SecretNo = "123", ProductName = "SP" });

    [Fact]
    public async Task CreateBox_RequiresCode_AndUnique()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateBoxAsync("", null, null)).ok);
            Assert.True((await svc.CreateBoxAsync("BOX1", null, null)).ok);
            Assert.False((await svc.CreateBoxAsync("BOX1", null, null)).ok);
        }
    }

    [Fact]
    public async Task PackSerial_ValidUnit_Counts()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "S1"); await db.SaveChangesAsync();
            var (_, _, id) = await svc.CreateBoxAsync("BOX1", null, null);
            var (ok, _) = await svc.PackSerialAsync(id, "S1");
            Assert.True(ok);
            Assert.Equal(1, (await svc.GetBoxAsync(id))!.ItemCount);
        }
    }

    [Fact]
    public async Task PackSerial_UnknownUnit_Rejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateBoxAsync("BOX1", null, null);
            var (ok, msg) = await svc.PackSerialAsync(id, "NOPE");
            Assert.False(ok);
            Assert.Contains("kho xác thực", msg);
        }
    }

    [Fact]
    public async Task PackSerial_DuplicateInSameBox_Rejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "S1"); await db.SaveChangesAsync();
            var (_, _, id) = await svc.CreateBoxAsync("BOX1", null, null);
            await svc.PackSerialAsync(id, "S1");
            var (ok, msg) = await svc.PackSerialAsync(id, "S1");
            Assert.False(ok);
            Assert.Contains("đã có trong hộp", msg);
        }
    }

    [Fact]
    public async Task PackSerial_InOtherBox_Rejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "S1"); await db.SaveChangesAsync();
            var (_, _, b1) = await svc.CreateBoxAsync("BOX1", null, null);
            var (_, _, b2) = await svc.CreateBoxAsync("BOX2", null, null);
            await svc.PackSerialAsync(b1, "S1");
            var (ok, msg) = await svc.PackSerialAsync(b2, "S1");
            Assert.False(ok);
            Assert.Contains("hộp khác", msg);
        }
    }

    [Fact]
    public async Task UnpackSerial_Removes()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "S1"); await db.SaveChangesAsync();
            var (_, _, id) = await svc.CreateBoxAsync("BOX1", null, null);
            await svc.PackSerialAsync(id, "S1");
            var (ok, _) = await svc.UnpackSerialAsync(id, "S1");
            Assert.True(ok);
            Assert.Equal(0, (await svc.GetBoxAsync(id))!.ItemCount);
        }
    }

    [Fact]
    public async Task PackBox_IntoCan_SetsCanCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, boxId) = await svc.CreateBoxAsync("BOX1", null, null);
            var (_, _, canId) = await svc.CreateCanAsync("CAN1", null, null);
            var (ok, _) = await svc.PackBoxAsync(canId, "BOX1");
            Assert.True(ok);
            Assert.Equal("CAN1", (await svc.GetBoxAsync(boxId))!.CanCode);
            Assert.Equal(1, (await svc.GetCanAsync(canId))!.BoxCount);
        }
    }

    [Fact]
    public async Task PackBox_AlreadyInOtherCan_Rejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateBoxAsync("BOX1", null, null);
            var (_, _, c1) = await svc.CreateCanAsync("CAN1", null, null);
            var (_, _, c2) = await svc.CreateCanAsync("CAN2", null, null);
            await svc.PackBoxAsync(c1, "BOX1");
            var (ok, msg) = await svc.PackBoxAsync(c2, "BOX1");
            Assert.False(ok);
            Assert.Contains("thùng khác", msg);
        }
    }

    [Fact]
    public async Task LookupBox_ReturnsItems()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await SeedUnit(db, "S1"); await SeedUnit(db, "S2"); await db.SaveChangesAsync();
            var (_, _, id) = await svc.CreateBoxAsync("BOX1", null, null);
            await svc.PackSerialAsync(id, "S1");
            await svc.PackSerialAsync(id, "S2");
            var l = await svc.LookupBoxAsync("BOX1");
            Assert.NotNull(l);
            Assert.Equal(2, l!.ItemCount);
            Assert.Null(await svc.LookupBoxAsync("NOPE"));
        }
    }
}
