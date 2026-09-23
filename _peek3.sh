#!/bin/bash
P=D:/idocNet/2017.A.iNOS.InBrand/Dev/.svn/pristine
echo "=== files mentioning Brand/Origin/NhanHieu/ThuongHieu (class defs) ==="
grep -rln 'class MstBrand\|class MstOrigin\|class Brand\|class Origin\|NhanHieu\|ThuongHieu\|XacThucNhanHieu\|DangKyNhanHieu' "$P" 2>/dev/null | while read f; do
  echo "--- $f"
  grep -m3 -n 'class \|namespace ' "$f"
done
