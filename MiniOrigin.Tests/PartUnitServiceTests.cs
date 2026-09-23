using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test danh mục Đơn vị tính (port từ Mst_PartUnit): mã bắt buộc + duy nhất, tên bắt buộc, cờ hoạt động, chỉ 1 đơn vị chuẩn, chặn xoá khi còn sản phẩm.</summary>
public class PartUnitServiceTests
{
    private static (AppDbContext db, IPartUnitService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new PartUnitService(db), conn);
    }

    [Fact]
    public async Task Create_RequiresCodeAndName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", "Viên", false, true)).ok);
            Assert.False((await svc.CreateAsync("VIEN", "", false, true)).ok);
            var (ok, _, id) = await svc.CreateAsync("vien", "Viên", false, true);
            Assert.True(ok);
            var u = await svc.GetAsync(id);
            Assert.Equal("VIEN", u!.Code);   // mã được chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("VIEN", "Viên", false, true);
            var (ok, msg, _) = await svc.CreateAsync("VIEN", "Viên (2)", false, true);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task List_FiltersByActiveAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("VIEN", "Viên", false, true);
            await svc.CreateAsync("KG", "Kilôgam", false, false);
            Assert.Equal(2, (await svc.ListAsync(null, null)).Count);
            Assert.Single(await svc.ListAsync(null, true));
            Assert.Single(await svc.ListAsync("kg", null));
        }
    }

    [Fact]
    public async Task OnlyOneStandardUnit()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id1) = await svc.CreateAsync("VIEN", "Viên", true, true);
            var (_, _, id2) = await svc.CreateAsync("KG", "Kilôgam", true, true);
            Assert.False((await svc.GetAsync(id1))!.IsStandard);   // đơn vị chuẩn cũ bị bỏ
            Assert.True((await svc.GetAsync(id2))!.IsStandard);
        }
    }

    [Fact]
    public async Task Delete_BlockedWhenProductsReference()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("VIEN", "Viên", false, true);
            var prod = new Product { Code = "P1", Name = "Gạch", Unit = "VIEN" };
            db.Products.Add(prod); await db.SaveChangesAsync();

            var (ok, msg) = await svc.DeleteAsync(id);
            Assert.False(ok);
            Assert.Contains("còn sản phẩm", msg);

            prod.Unit = null; await db.SaveChangesAsync();
            Assert.True((await svc.DeleteAsync(id)).ok);
        }
    }

    [Fact]
    public async Task SetActive_TogglesFlag()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("THUNG", "Thùng", false, true);
            await svc.SetActiveAsync(id, false);
            Assert.False((await svc.GetAsync(id))!.Active);
        }
    }
}
