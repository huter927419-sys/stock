@echo off
chcp 65001 >nul
setlocal

set PGPASSWORD=123456
"F:\dsfr\mqq\tools\bin\psql.exe" -h localhost -p 8532 -U postgres -d stockdb -f update_filter_stocks.sql

endlocal
pause
