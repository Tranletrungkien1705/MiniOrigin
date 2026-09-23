#!/bin/bash
P=D:/idocNet/2017.A.iNOS.InBrand/Dev/.svn/pristine
echo "=== search: DangKy / XacThuc / Register / Verify / Certificate / NhanHieu ==="
grep -rln 'DangKy\|XacThuc\|Register\|Verify\|Certificate\|NhanHieu\|VerifyBrand\|BrandRegist' "$P" 2>/dev/null | while read f; do
  echo "--- $f"
  grep -m4 -n 'class \|namespace \|public.*(' "$f" | head -6
done
