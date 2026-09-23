#!/bin/bash
while read f; do
  ns=$(grep -m1 'namespace idn.iNOS.InBrand' "$f" | tr -d '\r')
  cls=$(grep -m1 -oE 'public (class|interface|enum) [A-Za-z0-9_]+' "$f" | tr -d '\r')
  echo "$ns :: $cls"
done < /tmp/inbrand.txt | sort | uniq -c | sort -rn | head -100
