@echo off
chcp 65001 >nul
cls
echo ========================================
echo   数据库代码表一键检查工具
echo ========================================
echo.
echo 正在检查 stock_info 表的数据质量...
echo.
echo ========================================
echo.

"C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -p 8532 -U postgres -d stockdb -f "db\diagnose_stock_info.sql"

echo.
echo ========================================
echo.
echo 检查完成！
echo.
echo 如果发现问题，可以：
echo 1. 在pgAdmin中执行 db\fix_stock_info_common_issues.sql 修复
echo 2. 查看 数据库代码表检查和修复指南.md 获取详细说明
echo.
echo ========================================
pause
