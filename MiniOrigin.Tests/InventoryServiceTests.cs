using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;
using MiniOrigin.Services;
using Xunit;

namespace MiniOrigin.Tests;

/// <summary>Test danh mục Kho hàng (port từ Mst_Inventory / Mst_InventoryType / Mst_InventoryLevelType):
/// mã bắt buộc + duy nhất, tên bắt buộc, kho cấp trên + loại kho + cấp kho phải tồn tại &amp; đang hoạt động,
/// mặc định khi tạo (BU="X", Level=1), chặn xoá khi còn cấp dưới, loại kho &amp; cấp kho CRUD.</summary>
public class InventoryServiceTests
{
    private static (AppDbContext db, IInventoryService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new InventoryService(db), conn);
    }

    // Tạo sẵn 1 loại kho + 1 cấp kho + 1 kho gốc để dùng làm cấp trên.
    private static async Task<(int typeId, int levelTypeId, int rootId)> SeedRefs(IInventoryService svc)
    {
        var (_, _, typeId) = await svc.CreateTypeAsync("TP", "Kho thành phẩm", true);
        var (_, _, levelTypeId) = await svc.CreateLevelTypeAsync("TONG", "Kho tổng", true);
        var (_, _, rootId) = await svc.CreateAsync("KHO-ROOT", "Kho gốc", null, typeId, levelTypeId, null, null, null, null, null, true);
        return (typeId, levelTypeId, rootId);
    }

    [Fact]
    public async Task Create_RequiresCodeAndName()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, rootId) = await SeedRefs(svc);
            Assert.False((await svc.CreateAsync("", "Kho A", rootId, typeId, levelTypeId, null, null, null, null, null, true)).ok);
            Assert.False((await svc.CreateAsync("KHO001", "", rootId, typeId, levelTypeId, null, null, null, null, null, true)).ok);
            var (ok, _, id) = await svc.CreateAsync("kho001", "Kho A", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            Assert.True(ok);
            Assert.Equal("KHO001", (await svc.GetAsync(id))!.Inventory.Code);   // mã chuẩn hoá hoa
        }
    }

    [Fact]
    public async Task Create_AppliesDefaults()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, rootId) = await SeedRefs(svc);
            var (_, _, id) = await svc.CreateAsync("KHO001", "Kho A", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            var i = (await svc.GetAsync(id))!.Inventory;
            Assert.Equal("X", i.BuCode);     // InvBUCode = "X"
            Assert.Equal("X", i.BuPattern);  // InvBUPattern = "X"
            Assert.Equal(1, i.Level);        // InvLevel = 1
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, rootId) = await SeedRefs(svc);
            await svc.CreateAsync("KHO001", "Kho A", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            var (ok, msg, _) = await svc.CreateAsync("KHO001", "Kho B", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("đã tồn tại", msg);
        }
    }

    [Fact]
    public async Task Create_RootSelfReferences()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, _) = await SeedRefs(svc);
            // Kho gốc (không chọn cấp trên) tự tham chiếu chính nó.
            var (ok, _, id) = await svc.CreateAsync("VGLA", "Kho gốc", null, typeId, levelTypeId, null, null, null, null, null, true);
            Assert.True(ok);
            var i = (await svc.GetAsync(id))!.Inventory;
            Assert.Equal(i.Id, i.ParentId);   // InvCodeParent = InvCode
        }
    }

    [Fact]
    public async Task Create_RequiresActiveParent()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, rootId) = await SeedRefs(svc);
            // cấp trên đang hoạt động → OK
            Assert.True((await svc.CreateAsync("KHO-CON", "Kho con", rootId, typeId, levelTypeId, null, null, null, null, null, true)).ok);
            // cấp trên ngừng hoạt động → chặn
            await svc.SetActiveAsync(rootId, false);
            var (ok, msg, _) = await svc.CreateAsync("KHO-CON2", "Kho con 2", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("ngừng hoạt động", msg);
        }
    }

    [Fact]
    public async Task Create_RequiresActiveTypeAndLevelType()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, rootId) = await SeedRefs(svc);
            // loại kho ngừng hoạt động → chặn
            await svc.UpdateTypeAsync(typeId, "Kho thành phẩm", false);
            var (ok1, msg1, _) = await svc.CreateAsync("KHO001", "Kho A", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            Assert.False(ok1);
            Assert.Contains("Loại kho", msg1);
            // cấp kho ngừng hoạt động → chặn
            await svc.UpdateTypeAsync(typeId, "Kho thành phẩm", true);
            await svc.UpdateLevelTypeAsync(levelTypeId, "Kho tổng", false);
            var (ok2, msg2, _) = await svc.CreateAsync("KHO001", "Kho A", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            Assert.False(ok2);
            Assert.Contains("Cấp kho", msg2);
        }
    }

    [Fact]
    public async Task Create_RejectsUnknownType()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (_, levelTypeId, rootId) = await SeedRefs(svc);
            var (ok, msg, _) = await svc.CreateAsync("KHO001", "Kho A", rootId, 999, levelTypeId, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("loại kho", msg);
        }
    }

    [Fact]
    public async Task Create_RejectsUnknownParent()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, _) = await SeedRefs(svc);
            var (ok, msg, _) = await svc.CreateAsync("KHO001", "Kho A", 999, typeId, levelTypeId, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("cấp trên", msg);
        }
    }

    [Fact]
    public async Task Update_RejectsSelfAsParent()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, rootId) = await SeedRefs(svc);
            var (_, _, id) = await svc.CreateAsync("KHO001", "Kho A", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            var (ok, msg) = await svc.UpdateAsync(id, "Kho A", id, typeId, levelTypeId, null, null, null, null, null, true);
            Assert.False(ok);
            Assert.Contains("chính nó", msg);
        }
    }

    [Fact]
    public async Task Delete_BlockedWhenHasChildren()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, rootId) = await SeedRefs(svc);
            await svc.CreateAsync("KHO-CON", "Kho con", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            var (ok, msg) = await svc.DeleteAsync(rootId);
            Assert.False(ok);
            Assert.Contains("cấp dưới", msg);
        }
    }

    [Fact]
    public async Task List_FiltersByActiveAndQuery()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (typeId, levelTypeId, rootId) = await SeedRefs(svc);
            await svc.CreateAsync("KHO-HCM", "Kho HCM", rootId, typeId, levelTypeId, null, null, null, null, null, true);
            await svc.CreateAsync("KHO-HN", "Kho Hà Nội", rootId, typeId, levelTypeId, null, null, null, null, null, false);
            Assert.Equal(3, (await svc.ListAsync(null, null)).Count);   // gồm cả kho gốc
            Assert.Equal(2, (await svc.ListAsync(null, true)).Count);
            Assert.Single(await svc.ListAsync("hà nội", null));
        }
    }

    [Fact]
    public async Task TypeAndLevelType_CrudAndDeleteGuard()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var (ok, _, typeId) = await svc.CreateTypeAsync("tp", "Kho thành phẩm", true);
            Assert.True(ok);
            Assert.Single(await svc.ListTypesAsync(null, null));

            var (ok2, _, levelTypeId) = await svc.CreateLevelTypeAsync("tong", "Kho tổng", true);
            Assert.True(ok2);
            Assert.Single(await svc.ListLevelTypesAsync(null, null));

            var (_, _, rootId) = await svc.CreateAsync("KHO-ROOT", "Kho gốc", null, typeId, levelTypeId, null, null, null, null, null, true);
            // còn kho thuộc loại/cấp → chặn xoá
            Assert.False((await svc.DeleteTypeAsync(typeId)).ok);
            Assert.False((await svc.DeleteLevelTypeAsync(levelTypeId)).ok);

            await svc.DeleteAsync(rootId);
            Assert.True((await svc.DeleteTypeAsync(typeId)).ok);
            Assert.True((await svc.DeleteLevelTypeAsync(levelTypeId)).ok);
        }
    }
}