using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniOrigin.Models;
namespace MiniOrigin.Data;

public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await MigratePostgresAsync(db);
        if (!await db.Orgs.AnyAsync(o => o.Id == TenantContext.DefaultOrgId))
        { db.Orgs.Add(new Org { Id = TenantContext.DefaultOrgId, Name = "Demo Nguồn gốc", ApiKey = TenantContext.DefaultApiKey }); await db.SaveChangesAsync(); }

        if (!await db.Ctes.AnyAsync())
        {
            var harvest = new Cte { Code = "HARVEST", Name = "Thu hoạch", Icon = "bi-tree", Ordinal = 1 };
            var process = new Cte { Code = "PROCESS", Name = "Chế biến", Icon = "bi-gear-wide-connected", Ordinal = 2 };
            var pack = new Cte { Code = "PACK", Name = "Đóng gói", Icon = "bi-box-seam", Ordinal = 3 };
            var ship = new Cte { Code = "SHIP", Name = "Vận chuyển", Icon = "bi-truck", Ordinal = 4 };
            db.Ctes.AddRange(harvest, process, pack, ship); await db.SaveChangesAsync();
            db.Kdes.AddRange(
                new KdeDef { CteId = harvest.Id, Key = "field", Label = "Thửa ruộng", Ordinal = 1, Required = true },
                new KdeDef { CteId = harvest.Id, Key = "method", Label = "Phương thức", Ordinal = 2 },
                new KdeDef { CteId = process.Id, Key = "temperature", Label = "Nhiệt độ", Unit = "°C", Ordinal = 1 },
                new KdeDef { CteId = process.Id, Key = "line", Label = "Dây chuyền", Ordinal = 2 },
                new KdeDef { CteId = pack.Id, Key = "packsize", Label = "Quy cách", Unit = "kg", Ordinal = 1 },
                new KdeDef { CteId = ship.Id, Key = "vehicle", Label = "Biển số xe", Ordinal = 1 },
                new KdeDef { CteId = ship.Id, Key = "temp", Label = "Nhiệt độ thùng", Unit = "°C", Ordinal = 2 });
            await db.SaveChangesAsync();

            var farm = new Gln { Code = "8930000000001", Name = "Trang trại Đồng Tháp", Type = GlnType.Farm, Address = "Đồng Tháp" };
            var factory = new Gln { Code = "8930000000002", Name = "Nhà máy xay Cần Thơ", Type = GlnType.Factory, Address = "Cần Thơ" };
            var wh = new Gln { Code = "8930000000003", Name = "Kho phân phối HCM", Type = GlnType.Warehouse, Address = "TP.HCM" };
            db.Glns.AddRange(farm, factory, wh); await db.SaveChangesAsync();

            // Danh mục thương hiệu (nguồn gốc thương hiệu) — port từ Mst_Brand của InBrand.
            db.Brands.AddRange(
                new Brand { Code = "VIGLACERA", Name = "Viglacera", Active = true },
                new Brand { Code = "SANFI", Name = "Sanfi", Active = true });
            await db.SaveChangesAsync();

            // Danh mục Loại thời hạn bảo hành — port từ Mst_PartWarrantyType của InBrand.
            var wtA10 = new WarrantyType { Code = "A10", Name = "Bảo hành 10 năm", Remark = "Sứ vệ sinh — kiểu 1", Active = true };
            var wtA20 = new WarrantyType { Code = "A20", Name = "Bảo hành 20 năm", Remark = "Sứ vệ sinh — kiểu 2", Active = true };
            var wtA70 = new WarrantyType { Code = "A70", Name = "Bảo hành 7 năm", Remark = "Sen vòi", Active = true };
            db.WarrantyTypes.AddRange(wtA10, wtA20, wtA70); await db.SaveChangesAsync();

            // Danh mục Nhóm vật liệu — port từ Mst_PartMaterialType của InBrand.
            var mtSu = new MaterialType { Code = "SUVIETRI", Name = "Sứ vệ sinh", Active = true };
            var mtSen = new MaterialType { Code = "SENVOI", Name = "Sen vòi", Active = true };
            var mtGach = new MaterialType { Code = "GACHLATNEN", Name = "Gạch lát nền", Active = true };
            db.MaterialTypes.AddRange(mtSu, mtSen, mtGach); await db.SaveChangesAsync();

            // Danh mục Loại sản phẩm — port từ Mst_PartType của InBrand.
            var ptSu = new PartType { Code = "SUVIETRI", Name = "Sứ vệ sinh", Active = true };
            var ptSen = new PartType { Code = "SENVOI", Name = "Sen vòi", Active = true };
            var ptGach = new PartType { Code = "GACHLATNEN", Name = "Gạch lát nền", Active = true };
            db.PartTypes.AddRange(ptSu, ptSen, ptGach); await db.SaveChangesAsync();

            // Danh mục Đơn vị tính — port từ Mst_PartUnit của InBrand.
            db.PartUnits.AddRange(
                new PartUnit { Code = "VIEN", Name = "Viên", IsStandard = true, Active = true },
                new PartUnit { Code = "KG", Name = "Kilôgam", IsStandard = false, Active = true },
                new PartUnit { Code = "THUNG", Name = "Thùng", IsStandard = false, Active = true });
            await db.SaveChangesAsync();

            // Danh mục Nhà cung cấp — port từ Mst_Supplier của InBrand.
            db.Suppliers.AddRange(
                new Supplier { Code = "NCC001", Name = "Công ty TNHH Vật liệu Xây dựng Miền Nam", Type = "NORMAL", Active = true },
                new Supplier { Code = "NCC002", Name = "Công ty CP Men & Phụ gia Gốm sứ", Type = "NORMAL", Active = true });
            await db.SaveChangesAsync();

            // Danh mục Màu sắc sản phẩm + gán màu — port từ Mst_PartColor / Mst_MapPartColor của InBrand.
            var white = new ProductColor { Code = "TRANG-BONG", Name = "Glossy White", NameVn = "Trắng bóng", Active = true };
            var grey = new ProductColor { Code = "XAM-MO", Name = "Matte Grey", NameVn = "Xám mờ", Active = true };
            db.ProductColors.AddRange(white, grey); await db.SaveChangesAsync();

            // Sản phẩm demo để gán màu (mỗi sản phẩm chỉ 1 màu mặc định) + gán loại bảo hành.
            var tile = new Product { Code = "GACH-GRANITE-60", Name = "Gạch Granite 60x60", Unit = "viên", WarrantyTypeId = wtA10.Id, MaterialTypeId = mtGach.Id, PartTypeId = ptGach.Id };
            db.Products.Add(tile); await db.SaveChangesAsync();
            db.ProductColorMaps.AddRange(
                new ProductColorMap { ProductId = tile.Id, ColorId = white.Id, IsDefault = true, Active = true },
                new ProductColorMap { ProductId = tile.Id, ColorId = grey.Id, IsDefault = false, Active = true });
            await db.SaveChangesAsync();

            // Đơn vị sản phẩm xác thực chính hãng — port từ Inv_InventoryBalanceSerial/Inv_InventorySecret.
            var viglacera = db.Brands.Local.First(b => b.Code == "VIGLACERA");
            db.ProductUnits.AddRange(
                new ProductUnit { SerialNo = "VGC-2026-0001", SecretNo = "482913", ProductName = "Gạch Granite 60x60", BrandId = viglacera.Id, LotCode = "GAO-ST25-2026-A", Origin = "Việt Nam", WarrantyMonths = 24 },
                new ProductUnit { SerialNo = "VGC-2026-0002", SecretNo = "771204", ProductName = "Gạch Granite 60x60", BrandId = viglacera.Id, LotCode = "GAO-ST25-2026-A", Origin = "Việt Nam", WarrantyMonths = 24 });
            await db.SaveChangesAsync();

            // Lô nguyên liệu: lúa tươi từ trang trại
            var raw = new Lot { Code = "LUA-DT-2026-001", ProductName = "Lúa tươi ST25", Unit = "kg", Quantity = 5000, OriginGlnId = farm.Id, Status = LotStatus.Shipped };
            db.Lots.Add(raw); await db.SaveChangesAsync();
            db.Events.AddRange(
                Ev(raw.Id, harvest, farm, DateTime.Today.AddDays(-30), "Nguyễn Văn Nông", new() { ["field"] = "Thửa A3", ["method"] = "Hữu cơ" }, 1),
                Ev(raw.Id, ship, farm, DateTime.Today.AddDays(-28), "HTX Đồng Tháp", new() { ["vehicle"] = "66C-123.45" }, 2));
            await db.SaveChangesAsync();

            // Lô thành phẩm: gạo đóng túi — liên kết ngược về lô lúa
            var fin = new Lot { Code = "GAO-ST25-2026-A", ProductName = "Gạo ST25 túi 5kg", Unit = "túi", Quantity = 900, OriginGlnId = factory.Id, Status = LotStatus.Sold };
            db.Lots.Add(fin); await db.SaveChangesAsync();
            db.LotLinks.Add(new LotLink { ChildLotId = fin.Id, ParentLotId = raw.Id, Quantity = 5000 });
            db.Events.AddRange(
                Ev(fin.Id, process, factory, DateTime.Today.AddDays(-27), "QC Cần Thơ", new() { ["temperature"] = "25", ["line"] = "DC-02" }, 1),
                Ev(fin.Id, pack, factory, DateTime.Today.AddDays(-26), "Tổ đóng gói", new() { ["packsize"] = "5" }, 2),
                Ev(fin.Id, ship, wh, DateTime.Today.AddDays(-20), "Logistics", new() { ["vehicle"] = "51C-678.90", ["temp"] = "28" }, 3));
            await db.SaveChangesAsync();

            // Lần xuất ghép demo — port từ Inv_VerifiedIDInOut của InBrand.
            var batch = new VerifyBatch
            {
                Code = "IVIDINOUTNO.DEMO.0001", ProductName = "Gạch Granite 60x60",
                RefNo = "PXK2026-DEMO", RefNoSys = "PXK2026-DEMO", PlateNo = "51C-678.90",
                ReceivePlace = "Kho phân phối HCM", QtyPlan = 2, Status = VerifyBatchStatus.Open
            };
            db.VerifyBatches.Add(batch); await db.SaveChangesAsync();
            db.VerifyBatchItems.AddRange(
                new VerifyBatchItem { BatchId = batch.Id, IdNo = "VGC-2026-0001", ProductName = "Gạch Granite 60x60", BoxNo = "BOX-01", CustomerName = "Đại lý HCM" },
                new VerifyBatchItem { BatchId = batch.Id, IdNo = "VGC-2026-0002", ProductName = "Gạch Granite 60x60", BoxNo = "BOX-01", CustomerName = "Đại lý HCM" });
            batch.QtyInit = 2; batch.QtyVerified = 2;
            await db.SaveChangesAsync();

            // Đóng hộp / đóng thùng demo — port từ module Box/Can của InBrand.
            var box = new Box { Code = "BOX-VGC-0001", SecretNo = "BOXSEC01", Remark = "Hộp gạch Granite 60x60" };
            db.Boxes.Add(box); await db.SaveChangesAsync();
            db.BoxItems.AddRange(
                new BoxItem { BoxId = box.Id, SerialNo = "VGC-2026-0001", ProductName = "Gạch Granite 60x60" },
                new BoxItem { BoxId = box.Id, SerialNo = "VGC-2026-0002", ProductName = "Gạch Granite 60x60" });
            await db.SaveChangesAsync();

            var can = new Can { Code = "CAN-VGC-0001", SecretNo = "CANSEC01", Remark = "Thùng gạch Granite 60x60" };
            db.Cans.Add(can); await db.SaveChangesAsync();
            box.CanId = can.Id;
            await db.SaveChangesAsync();

            // Lịch sử tra cứu demo — port từ Rpt_SearchHis của InBrand.
            db.SearchLogs.AddRange(
                new SearchLog { SearchCode = "VGC-2026-0001", Type = SearchType.Authenticity, Found = true, UserCode = "khach-hcm", VisitId = "VISIT-DEMO-01", SearchDTime = DateTime.UtcNow.AddHours(-3) },
                new SearchLog { SearchCode = "GAO-ST25-2026-A", Type = SearchType.Trace, Found = true, UserCode = "khach-hn", VisitId = "VISIT-DEMO-02", SearchDTime = DateTime.UtcNow.AddHours(-2) },
                new SearchLog { SearchCode = "BOX-VGC-0001", Type = SearchType.Box, Found = true, UserCode = "khach-hcm", VisitId = "VISIT-DEMO-01", SearchDTime = DateTime.UtcNow.AddHours(-1) },
                new SearchLog { SearchCode = "VGC-9999-0000", Type = SearchType.Authenticity, Found = false, UserCode = "khach-dn", VisitId = "VISIT-DEMO-03", SearchDTime = DateTime.UtcNow.AddMinutes(-30) });
            await db.SaveChangesAsync();

            // Định mức nguyên vật liệu (BOM) demo — port từ Mst_BOM / Mst_BOMDtl / Mst_BOMType của InBrand.
            var bomType = new BomType { Code = "SANXUAT", Description = "BOM sản xuất", Active = true };
            db.BomTypes.Add(bomType); await db.SaveChangesAsync();

            var body = new Product { Code = "THAN-GACH-60", Name = "Thân gạch Granite 60x60", Unit = "viên" };
            var glaze = new Product { Code = "MEN-BONG", Name = "Men bóng", Unit = "kg" };
            db.Products.AddRange(body, glaze); await db.SaveChangesAsync();

            var bom = new Bom
            {
                Code = "BOM-GACH-60-01", ParentProductId = tile.Id, BomTypeId = bomType.Id,
                IsDefault = true, Status = BomStatus.Approve, Remark = "Định mức gạch Granite 60x60",
                ApproveDTime = DateTime.UtcNow
            };
            db.Boms.Add(bom); await db.SaveChangesAsync();
            db.BomLines.AddRange(
                new BomLine { BomId = bom.Id, ComponentProductId = body.Id, Qty = 1, Unit = "viên", ValCost = 0, Status = BomStatus.Approve },
                new BomLine { BomId = bom.Id, ComponentProductId = glaze.Id, Qty = 0.35m, Unit = "kg", ValCost = 0, Status = BomStatus.Approve });
            await db.SaveChangesAsync();

            // Danh mục Đại lý + Loại đại lý demo — port từ Mst_Dealer / Mst_DealerType của InBrand.
            var dtCap1 = new DealerType { Code = "CAP1", Name = "Đại lý cấp 1", Active = true };
            var dtBanLe = new DealerType { Code = "BANLE", Name = "Cửa hàng bán lẻ", Active = true };
            db.DealerTypes.AddRange(dtCap1, dtBanLe); await db.SaveChangesAsync();

            var dlRoot = new Dealer { Code = "DL-HCM", Name = "Đại lý HCM", DealerTypeId = dtCap1.Id, SkycicSiteID = "SITE-HCM-01", Active = true };
            db.Dealers.Add(dlRoot); await db.SaveChangesAsync();
            db.Dealers.AddRange(
                new Dealer { Code = "DL-HCM-Q1", Name = "Cửa hàng Quận 1", ParentId = dlRoot.Id, DealerTypeId = dtBanLe.Id, Active = true },
                new Dealer { Code = "DL-HN", Name = "Đại lý Hà Nội", DealerTypeId = dtCap1.Id, Active = true });
            await db.SaveChangesAsync();

            // Mẫu truy xuất demo — port từ Mst_TemplateNWType / TplNWT_Mst_CTE / TplNWT_Mst_KDE / TplNWT_CTE_KDE của InBrand.
            var tpl = new TraceTemplate { Code = "FRUIT", Name = "Mẫu truy xuất nông sản", Status = TraceTemplateStatus.Pending, Remark = "Bộ sự kiện + thành phần cho chuỗi nông sản" };
            db.TraceTemplates.Add(tpl); await db.SaveChangesAsync();
            db.TraceTemplateCtes.AddRange(
                new TraceTemplateCte { TemplateId = tpl.Id, Code = "HARVEST", Name = "Thu hoạch", Active = true },
                new TraceTemplateCte { TemplateId = tpl.Id, Code = "PACK", Name = "Đóng gói", Active = true });
            db.TraceTemplateKdes.AddRange(
                new TraceTemplateKde { TemplateId = tpl.Id, Code = "FIELD", Name = "Thửa ruộng", DataType = "TEXT", Active = true },
                new TraceTemplateKde { TemplateId = tpl.Id, Code = "PACKSIZE", Name = "Quy cách", DataType = "NUMBER", Active = true });
            db.TraceTemplateCteKdes.AddRange(
                new TraceTemplateCteKde { TemplateId = tpl.Id, CteCode = "HARVEST", KdeCode = "FIELD", FlagKey = true },
                new TraceTemplateCteKde { TemplateId = tpl.Id, CteCode = "PACK", KdeCode = "PACKSIZE" });
            await db.SaveChangesAsync();

            // Danh mục Kho hàng demo — port từ Mst_Inventory / Mst_InventoryType / Mst_InventoryLevelType của InBrand.
            var itTP = new InventoryType { Code = "TP", Name = "Kho thành phẩm", Active = true };
            var itNL = new InventoryType { Code = "NL", Name = "Kho nguyên liệu", Active = true };
            db.InventoryTypes.AddRange(itTP, itNL); await db.SaveChangesAsync();

            var ilTong = new InventoryLevelType { Code = "TONG", Name = "Kho tổng", Active = true };
            var ilVung = new InventoryLevelType { Code = "VUNG", Name = "Kho vùng", Active = true };
            db.InventoryLevelTypes.AddRange(ilTong, ilVung); await db.SaveChangesAsync();

            var invRoot = new Inventory
            {
                Code = "KHO-HCM", Name = "Kho tổng HCM", TypeId = itTP.Id, LevelTypeId = ilTong.Id,
                Address = "TP.HCM", ContactName = "Trần Văn Kho", ContactPhone = "0901234567", Active = true
            };
            db.Inventories.Add(invRoot); await db.SaveChangesAsync();
            db.Inventories.AddRange(
                new Inventory { Code = "KHO-HCM-Q1", Name = "Kho vùng Quận 1", ParentId = invRoot.Id, TypeId = itTP.Id, LevelTypeId = ilVung.Id, Active = true },
                new Inventory { Code = "KHO-HN", Name = "Kho tổng Hà Nội", TypeId = itNL.Id, LevelTypeId = ilTong.Id, Active = true });
            await db.SaveChangesAsync();
        }
    }

    private static TraceEvent Ev(int lotId, Cte cte, Gln gln, DateTime when, string op, Dictionary<string, string> kde, int seq) =>
        new()
        {
            LotId = lotId, CteId = cte.Id, CteName = cte.Name, CteIcon = cte.Icon,
            GlnId = gln.Id, GlnName = gln.Name, EventTime = when, Operator = op,
            KdeJson = JsonSerializer.Serialize(kde), Sequence = seq
        };

    private static async Task MigratePostgresAsync(AppDbContext db)
    {
        if (!db.Database.IsNpgsql()) return;
        var def = TenantContext.DefaultOrgId;
        var tables = new[] { "Glns", "Ctes", "Kdes", "Brands", "WarrantyTypes", "MaterialTypes", "PartTypes", "PartUnits", "Suppliers", "Products", "ProductColors", "ProductColorMaps", "Lots", "LotLinks", "Events", "ProductUnits", "VerifyBatches", "VerifyBatchItems", "Boxes", "Cans", "BoxItems", "SearchLogs", "BomTypes", "Boms", "BomLines", "DealerTypes", "Dealers", "TraceTemplates", "TraceTemplateCtes", "TraceTemplateKdes", "TraceTemplateCteKdes", "InventoryTypes", "InventoryLevelTypes", "Inventories" };
        var sql = new List<string> {
            "CREATE TABLE IF NOT EXISTS miniorigin.\"Orgs\" (\"Id\" uuid PRIMARY KEY, \"Name\" text NOT NULL DEFAULT '', \"ApiKey\" text NOT NULL DEFAULT '', \"CreatedAt\" timestamp NOT NULL DEFAULT now())",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Orgs_ApiKey\" ON miniorigin.\"Orgs\" (\"ApiKey\")" };
        foreach (var t in tables) sql.Add($"ALTER TABLE miniorigin.\"{t}\" ADD COLUMN IF NOT EXISTS \"OrgId\" uuid NOT NULL DEFAULT '{def}'");
        foreach (var s in sql) try { await db.Database.ExecuteSqlRawAsync(s); } catch { }
    }
}
