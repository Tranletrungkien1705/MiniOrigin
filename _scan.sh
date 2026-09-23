#!/bin/bash
DB="D:/idocNet/2017.A.iNOS.InBrand/Dev/.svn/wc.db"
grep -aob "MstSupplierManager.cs" "$DB" | head -3
echo "--- context around first hit ---"
off=$(grep -aob "MstSupplierManager.cs" "$DB" | head -1 | cut -d: -f1)
echo "offset=$off"
dd if="$DB" bs=1 skip=$((off-200)) count=600 2>/dev/null | tr -c '[:print:]' '\n' | grep -aE '.{3,}'
