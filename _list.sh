#!/bin/bash
DB="D:/idocNet/2017.A.iNOS.InBrand/Dev/.svn/wc.db"
"C:/Program Files/dotnet/dotnet.exe" run --project D:/idocNet/_labs/_svntool -c Release -- "$DB" "$1" 2>/dev/null
