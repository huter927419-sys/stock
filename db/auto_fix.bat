@echo off
chcp 65001 >nul
echo ========================================
echo 正在自动检查并修复 stock_info 表
echo ========================================
echo.

echo [步骤1] 快速检查当前状态...
echo.
"C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -p 8532 -U postgres -d stockdb -f "%~dp0quick_check.sql"

echo.
echo ========================================
echo [步骤2] 执行修复...
echo.
"C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -p 8532 -U postgres -d stockdb -f "%~dp0fix_stock_info_common_issues.sql"

echo.
echo ========================================
echo [步骤3] 验证修复结果...
echo.
"C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -p 8532 -U postgres -d stockdb -f "%~dp0quick_check.sql"

echo.
echo ========================================
echo 修复完成！
echo 建议：重启应用程序以重新加载缓存
echo ========================================
pause
