@echo off
chcp 65001 >nul
echo ========================================
echo 股票代码表（stock_info）诊断工具
echo ========================================
echo.

"C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -p 8532 -U postgres -d stockdb -f "%~dp0diagnose_stock_info.sql"

echo.
echo ========================================
echo 诊断完成！
echo ========================================
echo.
pause
