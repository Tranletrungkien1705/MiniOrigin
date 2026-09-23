#!/bin/bash
# Usage: _read.sh <wc.db> <relpath-substring>
DB="$1"
SUB="$2"
PRIST="$(dirname "$DB")/pristine"
line=$("C:/Program Files/dotnet/dotnet.exe" run --project D:/idocNet/_labs/_svntool -c Release -- "$DB" 2>/dev/null | grep -F "$SUB" | head -1)
if [ -z "$line" ]; then echo "NOT FOUND: $SUB"; exit 1; fi
sum=$(echo "$line" | cut -f1 | sed 's/\$sha1\$//')
path=$(echo "$line" | cut -f2)
f="$PRIST/${sum:0:2}/${sum}.svn-base"
echo "=== $path ==="
cat "$f"
