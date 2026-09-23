using Microsoft.EntityFrameworkCore;
using MiniOrigin.Data;
using MiniOrigin.Models;

namespace MiniOrigin.Services;

public record InventoryView(Inventory Inventory, string? ParentName, string? TypeName, string? LevelTypeName, int ChildCount);
public record InventoryTypeView(InventoryType Type, int InventoryCount);
public record InventoryLevelTypeView(InventoryLevelType LevelType, int InventoryCount);

public interface IInventoryService
{
    // Loại kho (Mst_InventoryType)
    Task<List<InventoryTypeView>> ListTypesAsync(string? q, bool? activeOnly);
    Task<(bool ok, string msg, int id)> CreateTypeAsync(string code, string name, bool active);
    Task<(bool ok, string msg)> UpdateTypeAsync(int id, string name, bool active);
    Task<(bool ok, string msg)> DeleteTypeAsync(int id);

    // Cấp kho (Mst_InventoryLevelType)
    Task<List<InventoryLevelTypeView>> ListLevelTypesAsync(string? q, bool? activeOnly);
    Task<(bool ok, string msg, int id)> CreateLevelTypeAsync(string code, string name, bool active);
    Task<(bool ok, string msg)> UpdateLevelTypeAsync(int id, string name, bool active);
    Task<(bool ok, string msg)> DeleteLevelTypeAsync(int id);

    // Kho hàng (Mst_Inventory)
    Task<List<InventoryView>> ListAsync(string? q, bool? activeOnly);
    Task<InventoryView?> GetAsync(int id);
    Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, int? parentId, int? typeId, int? levelTypeId,
        string? address, string? contactName, string? contactPhone, string? contactEmail, string? remark, bool active);
    Task<(bool ok, string msg)> UpdateAsync(int id, string name, int? parentId, int? typeId, int? levelTypeId,
        string? address, string? contactName, string? contactPhone, string? contactEmail, string? remark, bool active);
    Task<(bool ok, string msg)> SetActiveAsync(int id, bool active);
    Task<(bool ok, string msg)> DeleteAsync(int id);
}

/// <summary>
/// Danh mục Kho hàng (master) — port từ Mst_Inventory / Mst_InventoryType / Mst_InventoryLevelType của InBrand.
/// Luật (theo MstInventoryManager.MstInventoryCheckDB + Add/Update/Remove):
///  - InvCode bắt buộc + duy nhất theo tenant (Add: CheckDB Flag.No → mã phải CHƯA tồn tại;
///    Update/Remove: CheckDB Flag.Yes → mã phải TỒN TẠI);
///  - InvCodeParent bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG (CheckDB Flag.Yes + Flag.Active);
///  - InvLevelType bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG;
///  - InvType bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG;
///  - InvName bắt buộc (RequireField trên entity);
///  - khi tạo: InvBUCode/InvBUPattern="X", InvLevel=1, FlagActive=Active;
///  - chặn xoá kho còn kho con tham chiếu (giữ toàn vẹn cây phân cấp).
/// </summary>
public class InventoryService(AppDbContext db) : IInventoryService
{
    // ---------- Loại kho ----------

    public async Task<List<InventoryTypeView>> ListTypesAsync(string? q, bool? activeOnly)
    {
        var query = db.InventoryTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term) || t.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(t => t.Active);

        var types = await query.OrderBy(t => t.Code).ToListAsync();
        var counts = await db.Inventories.GroupBy(i => i.TypeId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return types.Select(t => new InventoryTypeView(t, counts.FirstOrDefault(c => c.Key == t.Id)?.Count ?? 0)).ToList();
    }

    public async Task<(bool ok, string msg, int id)> CreateTypeAsync(string code, string name, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã loại kho.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên loại kho.", 0);
        // CheckDB Flag.No: mã phải CHƯA tồn tại.
        if (await db.InventoryTypes.AnyAsync(t => t.Code == code)) return (false, "Mã loại kho đã tồn tại.", 0);

        var type = new InventoryType { Code = code, Name = name, Active = active };
        db.InventoryTypes.Add(type); await db.SaveChangesAsync();
        return (true, "Đã tạo loại kho.", type.Id);
    }

    public async Task<(bool ok, string msg)> UpdateTypeAsync(int id, string name, bool active)
    {
        var type = await db.InventoryTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại kho.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên loại kho.");
        type.Name = name; type.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật loại kho.");
    }

    public async Task<(bool ok, string msg)> DeleteTypeAsync(int id)
    {
        var type = await db.InventoryTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return (false, "Không tìm thấy loại kho.");
        if (await db.Inventories.AnyAsync(i => i.TypeId == id)) return (false, "Không thể xoá: còn kho thuộc loại này.");
        db.InventoryTypes.Remove(type); await db.SaveChangesAsync();
        return (true, "Đã xoá loại kho.");
    }

    // ---------- Cấp kho ----------

    public async Task<List<InventoryLevelTypeView>> ListLevelTypesAsync(string? q, bool? activeOnly)
    {
        var query = db.InventoryLevelTypes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term) || t.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(t => t.Active);

        var types = await query.OrderBy(t => t.Code).ToListAsync();
        var counts = await db.Inventories.GroupBy(i => i.LevelTypeId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return types.Select(t => new InventoryLevelTypeView(t, counts.FirstOrDefault(c => c.Key == t.Id)?.Count ?? 0)).ToList();
    }

    public async Task<(bool ok, string msg, int id)> CreateLevelTypeAsync(string code, string name, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã cấp kho.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên cấp kho.", 0);
        if (await db.InventoryLevelTypes.AnyAsync(t => t.Code == code)) return (false, "Mã cấp kho đã tồn tại.", 0);

        var levelType = new InventoryLevelType { Code = code, Name = name, Active = active };
        db.InventoryLevelTypes.Add(levelType); await db.SaveChangesAsync();
        return (true, "Đã tạo cấp kho.", levelType.Id);
    }

    public async Task<(bool ok, string msg)> UpdateLevelTypeAsync(int id, string name, bool active)
    {
        var levelType = await db.InventoryLevelTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (levelType == null) return (false, "Không tìm thấy cấp kho.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên cấp kho.");
        levelType.Name = name; levelType.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật cấp kho.");
    }

    public async Task<(bool ok, string msg)> DeleteLevelTypeAsync(int id)
    {
        var levelType = await db.InventoryLevelTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (levelType == null) return (false, "Không tìm thấy cấp kho.");
        if (await db.Inventories.AnyAsync(i => i.LevelTypeId == id)) return (false, "Không thể xoá: còn kho thuộc cấp này.");
        db.InventoryLevelTypes.Remove(levelType); await db.SaveChangesAsync();
        return (true, "Đã xoá cấp kho.");
    }

    // ---------- Kho hàng ----------

    public async Task<List<InventoryView>> ListAsync(string? q, bool? activeOnly)
    {
        var query = db.Inventories.Include(i => i.Parent).Include(i => i.Type).Include(i => i.LevelType).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(i => i.Code.ToLower().Contains(term) || i.Name.ToLower().Contains(term));
        }
        if (activeOnly == true) query = query.Where(i => i.Active);

        var inventories = await query.OrderBy(i => i.Code).ToListAsync();
        var childCounts = await db.Inventories.Where(i => i.ParentId != null && i.ParentId != i.Id)
            .GroupBy(i => i.ParentId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return inventories.Select(i => new InventoryView(i, i.Parent?.Name, i.Type?.Name, i.LevelType?.Name,
            childCounts.FirstOrDefault(c => c.Key == i.Id)?.Count ?? 0)).ToList();
    }

    public async Task<InventoryView?> GetAsync(int id)
    {
        var i = await db.Inventories.Include(x => x.Parent).Include(x => x.Type).Include(x => x.LevelType)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i == null) return null;
        var childCount = await db.Inventories.CountAsync(x => x.ParentId == id && x.Id != id);
        return new InventoryView(i, i.Parent?.Name, i.Type?.Name, i.LevelType?.Name, childCount);
    }

    public async Task<(bool ok, string msg, int id)> CreateAsync(string code, string name, int? parentId, int? typeId, int? levelTypeId,
        string? address, string? contactName, string? contactPhone, string? contactEmail, string? remark, bool active)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) return (false, "Cần mã kho.", 0);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên kho.", 0);
        // CheckDB Flag.No: mã phải CHƯA tồn tại.
        if (await db.Inventories.AnyAsync(i => i.Code == code)) return (false, "Mã kho đã tồn tại.", 0);

        // InvCodeParent bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG.
        // Ngoại lệ: kho GỐC (không chọn cấp trên) tự tham chiếu chính nó (theo TConst.BizMix.InvCodeRoot = "VGLA").
        Inventory? parent = null;
        if (parentId is > 0)
        {
            parent = await db.Inventories.FirstOrDefaultAsync(i => i.Id == parentId);
            if (parent == null) return (false, "Không tìm thấy kho cấp trên.", 0);
            if (!parent.Active) return (false, "Kho cấp trên đang ngừng hoạt động.", 0);
        }
        else parentId = null;

        // InvType bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG.
        if (typeId is not > 0) return (false, "Cần chọn loại kho.", 0);
        var type = await db.InventoryTypes.FirstOrDefaultAsync(t => t.Id == typeId);
        if (type == null) return (false, "Không tìm thấy loại kho.", 0);
        if (!type.Active) return (false, "Loại kho đang ngừng hoạt động.", 0);

        // InvLevelType bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG.
        if (levelTypeId is not > 0) return (false, "Cần chọn cấp kho.", 0);
        var levelType = await db.InventoryLevelTypes.FirstOrDefaultAsync(t => t.Id == levelTypeId);
        if (levelType == null) return (false, "Không tìm thấy cấp kho.", 0);
        if (!levelType.Active) return (false, "Cấp kho đang ngừng hoạt động.", 0);

        var inventory = new Inventory
        {
            Code = code, Name = name, ParentId = parentId, TypeId = typeId, LevelTypeId = levelTypeId,
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim(),
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim(),
            // Mặc định khi tạo (theo MstInventoryManager.Add).
            BuCode = "X", BuPattern = "X", Level = 1, Active = active
        };
        db.Inventories.Add(inventory); await db.SaveChangesAsync();
        // Kho gốc tự tham chiếu chính nó (InvCodeParent = InvCode) — theo TConst.BizMix.InvCodeRoot.
        if (inventory.ParentId == null) { inventory.ParentId = inventory.Id; await db.SaveChangesAsync(); }
        return (true, "Đã tạo kho.", inventory.Id);
    }

    public async Task<(bool ok, string msg)> UpdateAsync(int id, string name, int? parentId, int? typeId, int? levelTypeId,
        string? address, string? contactName, string? contactPhone, string? contactEmail, string? remark, bool active)
    {
        var inventory = await db.Inventories.FirstOrDefaultAsync(i => i.Id == id);
        if (inventory == null) return (false, "Không tìm thấy kho.");
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return (false, "Cần tên kho.");

        if (parentId is not > 0) return (false, "Cần chọn kho cấp trên.");
        if (parentId == id) return (false, "Kho không thể là cấp trên của chính nó.");
        var parent = await db.Inventories.FirstOrDefaultAsync(i => i.Id == parentId);
        if (parent == null) return (false, "Không tìm thấy kho cấp trên.");
        if (!parent.Active) return (false, "Kho cấp trên đang ngừng hoạt động.");

        if (typeId is not > 0) return (false, "Cần chọn loại kho.");
        var type = await db.InventoryTypes.FirstOrDefaultAsync(t => t.Id == typeId);
        if (type == null) return (false, "Không tìm thấy loại kho.");
        if (!type.Active) return (false, "Loại kho đang ngừng hoạt động.");

        if (levelTypeId is not > 0) return (false, "Cần chọn cấp kho.");
        var levelType = await db.InventoryLevelTypes.FirstOrDefaultAsync(t => t.Id == levelTypeId);
        if (levelType == null) return (false, "Không tìm thấy cấp kho.");
        if (!levelType.Active) return (false, "Cấp kho đang ngừng hoạt động.");

        inventory.Name = name; inventory.ParentId = parentId; inventory.TypeId = typeId; inventory.LevelTypeId = levelTypeId;
        inventory.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        inventory.ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        inventory.ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
        inventory.ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        inventory.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
        inventory.Active = active;
        await db.SaveChangesAsync();
        return (true, "Đã cập nhật kho.");
    }

    public async Task<(bool ok, string msg)> SetActiveAsync(int id, bool active)
    {
        var inventory = await db.Inventories.FirstOrDefaultAsync(i => i.Id == id);
        if (inventory == null) return (false, "Không tìm thấy kho.");
        inventory.Active = active;
        await db.SaveChangesAsync();
        return (true, active ? "Đã kích hoạt kho." : "Đã ngừng kho.");
    }

    public async Task<(bool ok, string msg)> DeleteAsync(int id)
    {
        var inventory = await db.Inventories.FirstOrDefaultAsync(i => i.Id == id);
        if (inventory == null) return (false, "Không tìm thấy kho.");
        if (await db.Inventories.AnyAsync(i => i.ParentId == id && i.Id != id)) return (false, "Không thể xoá: còn kho cấp dưới.");
        db.Inventories.Remove(inventory); await db.SaveChangesAsync();
        return (true, "Đã xoá kho.");
    }
}
