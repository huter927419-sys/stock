@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
cls

echo.
echo ╔════════════════════════════════════════╗
echo ║   股票代码表自动检查和修复工具         ║
echo ╚════════════════════════════════════════╝
echo.

set PSQL="C:\Program Files\PostgreSQL\16\bin\psql.exe"
set PGHOST=localhost
set PGPORT=8532
set PGUSER=postgres
set PGDATABASE=stockdb
set PGPASSWORD=123456

echo [步骤 1/3] 正在检查当前状态...
echo ----------------------------------------
echo.

%PSQL% -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT COUNT(*) as total_count, SUM(CASE WHEN is_active = TRUE THEN 1 ELSE 0 END) as active_count, SUM(CASE WHEN stock_name != stock_code AND stock_name IS NOT NULL THEN 1 ELSE 0 END) as has_name_count FROM stock_info;"

echo.
echo 检查可疑代码...
%PSQL% -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT stock_code, stock_name, is_active FROM stock_info WHERE stock_code IN ('000001', '000300', '000139', '000046', '000914') ORDER BY stock_code;"

echo.
echo.
echo [步骤 2/3] 正在执行修复...
echo ----------------------------------------
echo.

%PSQL% -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -f "db\fix_stock_info_common_issues.sql"

echo.
echo.
echo [步骤 3/3] 验证修复结果...
echo ----------------------------------------
echo.

%PSQL% -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT COUNT(*) as total_count, SUM(CASE WHEN is_active = TRUE THEN 1 ELSE 0 END) as active_count, SUM(CASE WHEN stock_name != stock_code AND stock_name IS NOT NULL THEN 1 ELSE 0 END) as has_name_count FROM stock_info;"

echo.
echo.
echo ╔════════════════════════════════════════╗
echo ║            修复完成！                  ║
echo ╚════════════════════════════════════════╝
echo.
echo 建议：
echo 1. 重启应用程序以重新加载缓存
echo 2. 刷新过滤查看效果
echo.
pause
