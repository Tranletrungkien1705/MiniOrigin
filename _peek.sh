#!/bin/bash
for f in "$@"; do
  echo "===== $f ====="
  head -8 "$f"
done
