# DEEPEN-LOG — MiniOrigin

- 2026-07-28 · feat: Xác thực sản phẩm chính hãng (nguồn gốc thương hiệu) — port từ module BrandPositioning của InBrand (piController: getDetailByQR/PINChecked/ActivePIN; bảng Inv_InventoryBalanceSerial + Inv_InventorySecret). Thêm entity ProductUnit (SerialNo + SecretNo/PIN, bảo hành, khách hàng), AuthenticityService (lookup/verify/activate), minimal-API /api/authenticity/*, /api/units, trang công khai /Verify + quản trị /Authenticity/Units, seed 2 đơn vị demo, 8 test. Build Release 0 error, 18/18 test pass.
