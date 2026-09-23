#!/bin/bash
for f in "$@"; do
  echo "===== $f ====="
  head -12 "$f"
done
