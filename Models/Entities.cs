namespace MiniOrigin.Models;

public interface IOrgOwned { Guid OrgId { get; set; } }

public enum GlnType { Farm = 0, Factory = 1, Warehouse = 2, Store = 3, Transport = 4 }
public enum LotStatus { Open = 0, Shipped = 1, Sold = 2, Recalled = 3 }

public class Org
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Global Location Number — địa điểm chuẩn GS1
public class Gln : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // 13 chữ số GLN
    public string Name { get; set; } = "";
    public GlnType Type { get; set; }
    public string? Address { get; set; }
}

// Critical Tracking Event — loại sự kiện (Trồng, Thu hoạch, Đóng gói, Vận chuyển…)
public class Cte : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "bi-record-circle";
    public int Ordinal { get; set; }
    public List<KdeDef> Kdes { get; set; } = new();
}

// Key Data Element — định nghĩa 1 trường dữ liệu thuộc 1 CTE
public class KdeDef : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int CteId { get; set; }
    public Cte? Cte { get; set; }
    public string Key { get; set; } = "";                // định danh (temperature)
    public string Label { get; set; } = "";              // hiển thị (Nhiệt độ)
    public string? Unit { get; set; }
    public bool Required { get; set; }
    public int Ordinal { get; set; }
}

// Thương hiệu (master) — nguồn gốc thương hiệu của sản phẩm (VD VIGLACERA, SANFI)
public class Brand : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // mã thương hiệu (duy nhất theo tenant)
    public string Name { get; set; } = "";               // tên hiển thị
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Unit { get; set; }
    public int? BrandId { get; set; }                    // thương hiệu của sản phẩm
    public Brand? Brand { get; set; }
    public int? WarrantyTypeId { get; set; }             // loại thời hạn bảo hành của sản phẩm
    public WarrantyType? WarrantyType { get; set; }
    public int? MaterialTypeId { get; set; }             // nhóm vật liệu của sản phẩm
    public MaterialType? MaterialType { get; set; }
    public int? PartTypeId { get; set; }                 // loại sản phẩm của sản phẩm
    public PartType? PartType { get; set; }
}

// Loại thời hạn bảo hành (master) — port từ Mst_PartWarrantyType của InBrand.
// Mã loại (A10, A20, …) quyết định cách hiển thị thời hạn bảo hành khi tra cứu sản phẩm.
public class WarrantyType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // WarrantyType — mã loại (duy nhất theo tenant)
    public string Name { get; set; } = "";               // WarrantyName — tên hiển thị
    public bool Active { get; set; } = true;             // FlagActive
    public string? Remark { get; set; }                   // Remark — ghi chú
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhóm vật liệu (loại vật liệu) — port từ Mst_PartMaterialType của InBrand.
// Phân nhóm sản phẩm theo vật liệu chế tác (VD Sứ vệ sinh, Sen vòi, Gạch lát nền).
public class MaterialType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // PMType — mã nhóm vật liệu (duy nhất theo tenant)
    public string Name { get; set; } = "";               // PMTypeName — tên hiển thị
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Loại sản phẩm (master) — port từ Mst_PartType của InBrand.
// Phân loại sản phẩm theo loại (VD: Sứ vệ sinh, Sen vòi, Gạch lát nền).
// Luật (theo MstPartTypeManager.MstPartTypeCheckDB + Add/Update/Remove):
//  - PartType bắt buộc + duy nhất theo tenant (Add: Flag.No → mã phải CHƯA tồn tại;
//    Update/Remove: Flag.Yes → mã phải TỒN TẠI);
//  - PartTypeName bắt buộc; cờ hoạt động FlagActive.
public class PartType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // PartType — mã loại sản phẩm (duy nhất theo tenant)
    public string Name { get; set; } = "";               // PartTypeName — tên loại sản phẩm
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Đơn vị tính (master) — port từ Mst_PartUnit của InBrand.
// Đơn vị đo lường của sản phẩm (VD: viên, kg, thùng). FlagUnitStd đánh dấu đơn vị CHUẨN.
// Luật (theo MstPartUnitManager.MstPartUnitCheckDB + Add/Update/Remove):
//  - PartUnitCode bắt buộc + duy nhất theo tenant (Add: Flag.No → mã phải CHƯA tồn tại;
//    Update/Remove: Flag.Yes → mã phải TỒN TẠI);
//  - PartUnitName bắt buộc; cờ hoạt động FlagActive; FlagUnitStd = đơn vị chuẩn.
public class PartUnit : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // PartUnitCode — mã đơn vị tính (duy nhất theo tenant)
    public string Name { get; set; } = "";               // PartUnitName — tên đơn vị tính
    public bool IsStandard { get; set; }                  // FlagUnitStd — đơn vị chuẩn
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Nhà cung cấp (master) — port từ Mst_Supplier của InBrand.
// Đối tác cung cấp nguyên vật liệu/hàng hoá; SupType mặc định NORMAL khi tạo.
public class Supplier : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // SupCode — mã nhà cung cấp (duy nhất theo tenant)
    public string Name { get; set; } = "";               // SupName — tên nhà cung cấp
    public string Type { get; set; } = "NORMAL";         // SupType — loại nhà cung cấp (mặc định NORMAL)
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Danh mục Màu sắc sản phẩm — port từ Mst_PartColor của InBrand.
// Màu sắc là thuộc tính nguồn gốc thương hiệu của sản phẩm (VD Trắng bóng, Xám mờ).
public class ProductColor : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // PartColorCode — mã màu (duy nhất theo tenant)
    public string Name { get; set; } = "";               // PartColorName — tên (EN)
    public string NameVn { get; set; } = "";             // PartColorNameVN — tên tiếng Việt
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Gán màu cho sản phẩm — port từ Mst_MapPartColor của InBrand.
// Luật: mỗi sản phẩm chỉ có TỐI ĐA 1 màu mặc định (FlagDefault).
public class ProductColorMap : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int ProductId { get; set; }                    // PartCode — sản phẩm
    public Product? Product { get; set; }
    public int ColorId { get; set; }                      // PartColorCode — màu
    public ProductColor? Color { get; set; }
    public bool IsDefault { get; set; }                   // FlagDefault — màu mặc định của sản phẩm
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Lô sản xuất — đơn vị truy xuất
public class Lot : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // Mã lô (GLOBAL unique — tra cứu công khai)
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public int? OriginGlnId { get; set; }
    public Gln? OriginGln { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public LotStatus Status { get; set; } = LotStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TraceEvent> Events { get; set; } = new();
}

// Phả hệ lô: lô con (thành phẩm) ← lô cha (nguyên liệu / bán thành phẩm)
public class LotLink : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int ChildLotId { get; set; }                  // lô kết quả
    public int ParentLotId { get; set; }                 // lô đầu vào
    public decimal? Quantity { get; set; }
}

// Đơn vị sản phẩm (serial) — đơn vị xác thực chính hãng theo Serial + mã bí mật (PIN)
// Port từ Inv_InventoryBalanceSerial / Inv_InventorySecret của InBrand (module BrandPositioning).
public class ProductUnit : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string SerialNo { get; set; } = "";            // SerialNo_Actual — số serial thực tế in trên tem/QR
    public string SecretNo { get; set; } = "";            // SecretNo — mã bí mật (PIN) để xác thực
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public int? BrandId { get; set; }                     // thương hiệu của đơn vị (nguồn gốc thương hiệu)
    public Brand? Brand { get; set; }
    public string? LotCode { get; set; }                  // FGLotNo — mã lô thành phẩm
    public string? Origin { get; set; }                   // xuất xứ
    public DateTime? WarrantyDateStart { get; set; }      // ngày kích hoạt bảo hành (lần xác thực đầu)
    public int WarrantyMonths { get; set; }               // thời hạn bảo hành (tháng)
    public bool Activated { get; set; }                   // đã kích hoạt bảo hành chưa
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public int VerifyCount { get; set; }                  // số lần đã xác thực
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Lần xuất ghép (batch xác thực) — port từ Inv_VerifiedIDInOut của InBrand.
// Một "lần xuất ghép" gom 1 lô tem (IDNo) đã xác thực để ghép với 1 đơn hàng/phiếu xuất.
public enum VerifyBatchStatus { Open = 0, Merged = 1, Cancelled = 2 }

public class VerifyBatch : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // IVerifiedIDInOutNo — mã lần xuất ghép
    public string ProductName { get; set; } = "";         // sản phẩm ghép
    public string? RefNo { get; set; }                    // mã đơn hàng / phiếu xuất
    public string? RefNoSys { get; set; }                 // mã phiếu xuất kho (hệ thống)
    public string? TransportType { get; set; }            // loại phương tiện
    public string? PlateNo { get; set; }                  // biển số xe
    public string? ReceivePlace { get; set; }             // địa điểm nhận hàng
    public string? InvOutType { get; set; }               // loại xuất kho
    public int QtyPlan { get; set; }                      // số lượng kế hoạch
    public int QtyInit { get; set; }                      // số lượng thực tế nhập vào
    public int QtyVerified { get; set; }                  // số lượng ghép được (OK)
    public VerifyBatchStatus Status { get; set; } = VerifyBatchStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? MergedAt { get; set; }

    public List<VerifyBatchItem> Items { get; set; } = new();
}

// Tem (IDNo) trong 1 lần xuất ghép — port từ Inv_InventoryVerifiedID.
public class VerifyBatchItem : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int BatchId { get; set; }
    public VerifyBatch? Batch { get; set; }
    public string IdNo { get; set; } = "";               // mã tem (IDNo)
    public string? Pin { get; set; }                      // mã bí mật đi kèm tem (nếu có)
    public string? ProductName { get; set; }
    public string? BoxNo { get; set; }                    // hộp chứa tem
    public string? CustomerName { get; set; }             // khách hàng nhận
    public bool FlagNG { get; set; }                      // tem lỗi (không ghép được)
    public string? ErrorReason { get; set; }              // lý do lỗi
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}

// Hộp (Box) — đóng gói nhiều đơn vị sản phẩm (serial) vào 1 hộp.
// Port từ Inv_InventoryBox của InBrand (BoxNo + SecretNo; cờ FlagBox trên Inv_InventoryBalanceSerial).
public class Box : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // BoxNo — mã hộp (duy nhất theo tenant)
    public string? SecretNo { get; set; }                 // mã bí mật của hộp (nếu có)
    public string? Remark { get; set; }
    public int? CanId { get; set; }                       // thùng chứa hộp này (nếu đã đóng thùng)
    public Can? Can { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BoxItem> Items { get; set; } = new();
}

// Thùng (Can) — đóng gói nhiều hộp vào 1 thùng.
// Port từ Inv_InventoryCan của InBrand (CanNo; cờ FlagCan trên Inv_InventoryBalanceSerial).
public class Can : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // CanNo — mã thùng (duy nhất theo tenant)
    public string? SecretNo { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Box> Boxes { get; set; } = new();
}

// Đơn vị sản phẩm (serial) nằm trong 1 hộp — port từ quan hệ BoxNo trên Inv_InventoryBalanceSerial.
public class BoxItem : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int BoxId { get; set; }
    public Box? Box { get; set; }
    public string SerialNo { get; set; } = "";            // SerialNo_Actual của đơn vị được đóng vào hộp
    public string? ProductName { get; set; }
    public DateTime PackedAt { get; set; } = DateTime.UtcNow;
}

// Lịch sử tra cứu — port từ Rpt_SearchHis của InBrand.
// Ghi lại mỗi lần người dùng tra cứu 1 mã (serial xác thực / mã lô / mã hộp) để thống kê & truy vết.
public enum SearchType { Authenticity = 0, Trace = 1, Box = 2 }

public class SearchLog : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string SearchCode { get; set; } = "";          // mã đã tra cứu (serial / mã lô / mã hộp)
    public string? UserCode { get; set; }                  // người tra cứu (nếu đã đăng nhập)
    public SearchType Type { get; set; }                   // loại tra cứu (xác thực / nguồn gốc / hộp)
    public bool Found { get; set; }                        // có tìm thấy kết quả không
    public string? VisitId { get; set; }                   // SkycicVisitID — mã phiên truy cập
    public DateTime SearchDTime { get; set; } = DateTime.UtcNow;   // thời điểm tra cứu
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Loại BOM (Bill of Materials) — port từ Mst_BOMType của InBrand.
// Phân loại cấu trúc định mức nguyên vật liệu (VD: BOM sản xuất, BOM đóng gói).
public class BomType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // BOMType — mã loại (duy nhất theo tenant)
    public string? Description { get; set; }              // BOMTypeDesc — mô tả
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Trạng thái BOM — port từ TConst.BOMStatus của InBrand (PENDING → APPROVE → FINISH).
public enum BomStatus { Pending = 0, Approve = 1, Finish = 2 }

// Định mức nguyên vật liệu (Bill of Materials) — port từ Mst_BOM của InBrand.
// Một BOM gắn 1 sản phẩm cha (PartCodeParent) với 1 loại BOM, gồm nhiều dòng thành phần.
// Vòng đời: PENDING (tạo/sửa/xoá) → APPROVE (duyệt) → FINISH (hoàn tất).
public class Bom : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // BOMCode — mã BOM (duy nhất theo tenant)
    public int ParentProductId { get; set; }              // PartCodeParent — sản phẩm cha
    public Product? ParentProduct { get; set; }
    public int BomTypeId { get; set; }                    // BOMType — loại BOM
    public BomType? BomType { get; set; }
    public bool IsDefault { get; set; }                   // FlagDefault — BOM mặc định của sản phẩm cha
    public BomStatus Status { get; set; } = BomStatus.Pending;
    public string? Remark { get; set; }
    public DateTime? ApproveDTime { get; set; }           // ApprDTime
    public string? ApproveBy { get; set; }                // ApprBy
    public DateTime? FinishDTime { get; set; }            // FinishDTime
    public string? FinishBy { get; set; }                 // FinishBy
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BomLine> Lines { get; set; } = new();
}

// Dòng thành phần của BOM — port từ Mst_BOMDtl của InBrand.
// Mỗi dòng: 1 sản phẩm thành phần (PartCode) + số lượng (Qty) + đơn vị tính (PartUnitCode).
public class BomLine : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int BomId { get; set; }
    public Bom? Bom { get; set; }
    public int ComponentProductId { get; set; }           // PartCode — sản phẩm thành phần
    public Product? ComponentProduct { get; set; }
    public decimal Qty { get; set; }                      // Qty — số lượng (>= 0)
    public string? Unit { get; set; }                     // PartUnitCode — đơn vị tính
    public decimal ValCost { get; set; }                  // ValCost — giá trị chi phí (mặc định 0)
    public BomStatus Status { get; set; } = BomStatus.Pending;   // BOMStatusDtl
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Loại đại lý (master) — port từ Mst_DealerType của InBrand.
// Phân loại đại lý (VD: Đại lý cấp 1, Nhà phân phối, Cửa hàng bán lẻ).
public class DealerType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // DLType — mã loại đại lý (duy nhất theo tenant)
    public string Name { get; set; } = "";               // DLTypeName — tên loại đại lý
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Đại lý (master) — port từ Mst_Dealer của InBrand.
// Đại lý là điểm bán/xác thực sản phẩm chính hãng; có phân cấp cha–con (DLCodeParent).
// Luật (theo MstDealerManager.MstDealerAddX/Update/Remove + MstDealerCheckDB):
//  - DLCode bắt buộc + duy nhất theo tenant (Add: Flag.No → mã phải CHƯA tồn tại;
//    Update/Remove: Flag.Yes → mã phải TỒN TẠI);
//  - DLName bắt buộc;
//  - DLCodeParent (nếu có) phải TỒN TẠI & ĐANG HOẠT ĐỘNG;
//  - khi tạo: FlagRoot=No, DLBUCode/DLBUPattern="X", DLLevel=1, FlagActive=Active.
public class Dealer : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // DLCode — mã đại lý (duy nhất theo tenant)
    public string Name { get; set; } = "";               // DLName — tên đại lý
    public int? ParentId { get; set; }                    // DLCodeParent — đại lý cấp trên
    public Dealer? Parent { get; set; }
    public int? DealerTypeId { get; set; }                // DLType — loại đại lý
    public DealerType? DealerType { get; set; }
    public string? InvCode { get; set; }                  // InvCode — mã kho gắn với đại lý
    public string? MaterialTypeCode { get; set; }         // PMType — nhóm vật liệu đại lý phụ trách
    public string? SkycicSiteID { get; set; }             // SkycicSiteID — mã site trên hệ thống Skycic
    public bool IsRoot { get; set; }                      // FlagRoot — đại lý gốc
    public string BuCode { get; set; } = "X";            // DLBUCode — mã đơn vị kinh doanh
    public string BuPattern { get; set; } = "X";         // DLBUPattern — mẫu mã đơn vị kinh doanh
    public double Level { get; set; } = 1;                // DLLevel — cấp trong cây đại lý
    public bool Active { get; set; } = true;             // FlagActive
    public string? Remark { get; set; }                   // Remark — ghi chú
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Trạng thái Mẫu truy xuất — port từ TConst.eTemNN.TplNWTStatus của InBrand (PENDING → APPROVE → CANCEL).
public enum TraceTemplateStatus { Pending = 0, Approve = 1, Cancel = 2 }

// Mẫu truy xuất (TemplateNWType) — port từ Mst_TemplateNWType của InBrand (module eTemNN).
// Một mẫu định nghĩa bộ sự kiện (CTE) + thành phần dữ liệu (KDE) mà 1 loại tổ chức dùng để truy xuất.
// Vòng đời: PENDING (tạo/sửa/xoá) → APPROVE (duyệt) → CANCEL (huỷ).
// Luật (theo MstTemplate.Mst_TemplateNWType_CheckDB + Mst_TemplateNWType_Save):
//  - TplNWType bắt buộc + duy nhất theo tenant (CheckDB Flag.No → mã phải CHƯA tồn tại;
//    Flag.Yes → mã phải TỒN TẠI);
//  - TplNWTDesc bắt buộc;
//  - chỉ mẫu ở trạng thái PENDING mới được sửa/xoá (SaveX_InvalidStatus).
public class TraceTemplate : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // TplNWType — mã loại tổ chức/mẫu (duy nhất theo tenant)
    public string Name { get; set; } = "";               // TplNWTDesc — tên mẫu
    public TraceTemplateStatus Status { get; set; } = TraceTemplateStatus.Pending;   // TplNWTStatus
    public string? Remark { get; set; }                   // Remark — ghi chú
    public DateTime? ApproveDTime { get; set; }           // mốc duyệt
    public DateTime? CancelDTime { get; set; }            // mốc huỷ
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TraceTemplateCte> Ctes { get; set; } = new();
    public List<TraceTemplateKde> Kdes { get; set; } = new();
    public List<TraceTemplateCteKde> CteKdes { get; set; } = new();
}

// Sự kiện (CTE) trong 1 mẫu truy xuất — port từ TplNWT_Mst_CTE của InBrand.
public class TraceTemplateCte : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int TemplateId { get; set; }
    public TraceTemplate? Template { get; set; }
    public string Code { get; set; } = "";               // CTECode — mã sự kiện
    public string Name { get; set; } = "";               // CTEDesc — mô tả sự kiện
    public string? ApiLink { get; set; }                  // APIsLink — liên kết API (nếu có)
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Thành phần dữ liệu (KDE) trong 1 mẫu truy xuất — port từ TplNWT_Mst_KDE của InBrand.
public class TraceTemplateKde : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int TemplateId { get; set; }
    public TraceTemplate? Template { get; set; }
    public string Code { get; set; } = "";               // KDECode — mã thành phần
    public string Name { get; set; } = "";               // KDEDesc — mô tả thành phần
    public string? DataType { get; set; }                 // DataType — kiểu dữ liệu (TEXT/NUMBER/DATE…)
    public string? RefNoList { get; set; }                // RefNoList — danh sách chọn sẵn
    public bool FlagList { get; set; }                    // FlagList — là danh sách
    public bool FlagQuery { get; set; }                   // FlagQuery — dùng để truy vấn
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Gán KDE vào CTE trong 1 mẫu truy xuất — port từ TplNWT_CTE_KDE của InBrand.
// Luật: 1 cặp (CTE, KDE) chỉ gán 1 lần trong 1 mẫu.
public class TraceTemplateCteKde : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int TemplateId { get; set; }
    public TraceTemplate? Template { get; set; }
    public string CteCode { get; set; } = "";            // CTECode
    public string KdeCode { get; set; } = "";            // KDECode
    public string? ApiLink { get; set; }                  // APIsLink
    public bool FlagOsOrgView { get; set; }               // FlagOSOrgView — hiển thị trên cổng tổ chức
    public bool FlagKey { get; set; }                     // FlagKey — là khoá
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Sự kiện thực tế gắn với lô (1 CTE tại 1 GLN + giá trị KDE)
public class TraceEvent : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int LotId { get; set; }
    public Lot? Lot { get; set; }
    public int CteId { get; set; }
    public string CteName { get; set; } = "";
    public string CteIcon { get; set; } = "bi-record-circle";
    public int? GlnId { get; set; }
    public string? GlnName { get; set; }
    public DateTime EventTime { get; set; } = DateTime.Now;
    public string? Operator { get; set; }
    public string? Note { get; set; }
    public string KdeJson { get; set; } = "{}";          // {"temperature":"4","lot":"A1"}
    public int Sequence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Loại kho (master) — port từ Mst_InventoryType của InBrand.
// Phân loại kho hàng (VD: Kho thành phẩm, Kho nguyên liệu, Kho trung chuyển).
public class InventoryType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // InvType — mã loại kho (duy nhất theo tenant)
    public string Name { get; set; } = "";               // InvTypeName — tên loại kho
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Cấp kho (master) — port từ Mst_InventoryLevelType của InBrand.
// Phân cấp kho theo tầng quản lý (VD: Kho tổng, Kho vùng, Kho chi nhánh).
public class InventoryLevelType : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // InvLevelType — mã cấp kho (duy nhất theo tenant)
    public string Name { get; set; } = "";               // InvLevelTypeName — tên cấp kho
    public bool Active { get; set; } = true;             // FlagActive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Kho hàng (master) — port từ Mst_Inventory của InBrand.
// Kho là điểm lưu trữ hàng hoá; có phân cấp cha–con (InvCodeParent) và gắn loại kho + cấp kho.
// Luật (theo MstInventoryManager.MstInventoryCheckDB + Add/Update/Remove):
//  - InvCode bắt buộc + duy nhất theo tenant (Add: Flag.No → mã phải CHƯA tồn tại;
//    Update/Remove: Flag.Yes → mã phải TỒN TẠI);
//  - InvCodeParent bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG;
//  - InvLevelType bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG;
//  - InvType bắt buộc, phải TỒN TẠI & ĐANG HOẠT ĐỘNG;
//  - InvName bắt buộc;
//  - khi tạo: InvBUCode/InvBUPattern="X", InvLevel=1, FlagActive=Active.
public class Inventory : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";               // InvCode — mã kho (duy nhất theo tenant)
    public int? ParentId { get; set; }                    // InvCodeParent — kho cấp trên
    public Inventory? Parent { get; set; }
    public string BuCode { get; set; } = "X";            // InvBUCode — mã đơn vị kinh doanh
    public string BuPattern { get; set; } = "X";         // InvBUPattern — mẫu mã đơn vị kinh doanh
    public double Level { get; set; } = 1;                // InvLevel — cấp trong cây kho
    public int? LevelTypeId { get; set; }                 // InvLevelType — cấp kho
    public InventoryLevelType? LevelType { get; set; }
    public int? TypeId { get; set; }                      // InvType — loại kho
    public InventoryType? Type { get; set; }
    public string Name { get; set; } = "";               // InvName — tên kho
    public string? Address { get; set; }                  // InvAddress — địa chỉ
    public string? ContactName { get; set; }              // InvContactName — người liên hệ
    public string? ContactPhone { get; set; }             // InvContactPhone — điện thoại liên hệ
    public string? ContactEmail { get; set; }             // InvContactEmail — email liên hệ
    public bool Active { get; set; } = true;             // FlagActive
    public string? Remark { get; set; }                   // Remark — ghi chú
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
