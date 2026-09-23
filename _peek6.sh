#!/bin/bash
P=D:/idocNet/2017.A.iNOS.InBrand/Dev/.svn/pristine
echo "=== InBrand domain classes (namespace idn.iNOS.InBrand) ==="
grep -rln 'namespace idn.iNOS.InBrand' "$P" 2>/dev/null | while read f; do
  ns=$(grep -m1 'namespace idn.iNOS.InBrand' "$f")
  cls=$(grep -m1 -oE 'public (class|interface|enum) [A-Za-z0-9_]+' "$f")
  echo "$f | $ns | $cls"
done | sort -t'|' -k2 | head -120
