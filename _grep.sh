#!/bin/bash
DB="D:/idocNet/2017.A.iNOS.InBrand/Dev/.svn/wc.db"
bash D:/idocNet/_labs/MiniOrigin/_read.sh "$DB" "$1" | grep -iE "$2"
