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
