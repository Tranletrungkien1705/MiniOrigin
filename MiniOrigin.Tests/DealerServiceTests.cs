using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test danh mục Đại lý (port từ Mst_Dealer / Mst_DealerType): mã bắt buộc + duy nhất, tên bắt buộc,
/// đại lý cấp trên phải tồn tại &amp; đang hoạt động, mặc định khi tạo (FlagRoot=No, BU="X", Level=1),
/// chặn xoá khi còn cấp dưới, loại đại lý CRUD.</summary>
public class DealerServiceTests
{
    private static (AppDbContext db, IDealerService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new DealerService(db), conn);
    }

    [Fact]
    public async Task Create_RequiresCodeAndName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            Assert.False((await svc.CreateAsync("", "Đại lý A", null, null, null, null, null, null, true)).ok);
            Assert.False((await svc.CreateAsync("DL001", "", null, null, null, null, null, null, true)).ok);
            var (ok, _, id) = await svc.CreateAsync("dl001", "Đại lý A", null, null, null, null, null, null, true);
            Assert.True(ok);
            Assert.Equal("DL001", (await svc.GetAsync(id))!.Dealer.Code);   // mã chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_AppliesDefaults()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("DL001", "Đại lý A", null, null, null, null, null, null, true);
            var d = (await svc.GetAsync(id))!.Dealer;
            Assert.False(d.IsRoot);          // FlagRoot = No
            Assert.Equal("X", d.BuCode);     // DLBUCode = "X"
            Assert.Equal("X", d.BuPattern);  // DLBUPattern = "X"
            Assert.Equal(1, d.Level);        // DLLevel = 1
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("DL001", "Đại lý A", null, null, null, null, null, null, true);
            var (ok, msg, _) = await svc.CreateAsync("DL001", "Đại lý B", null, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task Create_RequiresActiveParent()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, parentId) = await svc.CreateAsync("DL-ROOT", "Đại lý gốc", null, null, null, null, null, null, true);
            // cấp trên đang hoạt động → OK
            Assert.True((await svc.CreateAsync("DL-CON", "Đại lý con", parentId, null, null, null, null, null, true)).ok);
            // cấp trên ngừng hoạt động → chặn
            await svc.SetActiveAsync(parentId, false);
            var (ok, msg, _) = await svc.CreateAsync("DL-CON2", "Đại lý con 2", parentId, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("ngừng hoạt động", msg);
        }
    }

    [Fact]
    public async Task Create_RejectsUnknownParent()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, msg, _) = await svc.CreateAsync("DL001", "Đại lý A", 999, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("cấp trên", msg);
        }
    }

    [Fact]
    public async Task Update_RejectsSelfAsParent()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, id) = await svc.CreateAsync("DL001", "Đại lý A", null, null, null, null, null, null, true);
            var (ok, msg) = await svc.UpdateAsync(id, "Đại lý A", id, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("chính nó", msg);
        }
    }

    [Fact]
    public async Task Delete_BlockedWhenHasChildren()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, _, parentId) = await svc.CreateAsync("DL-ROOT", "Đại lý gốc", null, null, null, null, null, null, true);
            await svc.CreateAsync("DL-CON", "Đại lý con", parentId, null, null, null, null, null, true);
            var (ok, msg) = await svc.DeleteAsync(parentId);
            Assert.False(ok);
            Assert.Contains("cấp dưới", msg);
        }
    }

    [Fact]
    public async Task List_FiltersByActiveAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await svc.CreateAsync("DL-HCM", "Đại lý HCM", null, null, null, null, null, null, true);
            await svc.CreateAsync("DL-HN", "Đại lý Hà Nội", null, null, null, null, null, null, false);
            Assert.Equal(2, (await svc.ListAsync(null, null)).Count);
            Assert.Single(await svc.ListAsync(null, true));
            Assert.Single(await svc.ListAsync("hà nội", null));
        }
    }

    [Fact]
    public async Task DealerType_CrudAndDeleteGuard()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, typeId) = await svc.CreateTypeAsync("cap1", "Đại lý cấp 1", true);
            Assert.True(ok);
            Assert.Single(await svc.ListTypesAsync(null, null));

            await svc.CreateAsync("DL001", "Đại lý A", null, typeId, null, null, null, null, true);
            // còn đại lý thuộc loại → chặn xoá
            Assert.False((await svc.DeleteTypeAsync(typeId)).ok);

            await svc.DeleteAsync((await svc.ListAsync(null, null))[0].Dealer.Id);
            Assert.True((await svc.DeleteTypeAsync(typeId)).ok);
        }
    }
}
