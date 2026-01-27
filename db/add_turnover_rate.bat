@echo off
chcp 65001 >nul
echo ========================================
echo   为 stock_daily_data 增加 turnover_rate 字段
echo ========================================
echo.

set PGHOST=localhost
set PGPORT=8532
set PGDATABASE=stockdb
set PGUSER=postgres
set PGPASSWORD=cd123321

echo 正在连接: %PGHOST%:%PGPORT%/%PGDATABASE%
echo.

psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -f "%~dp0alter_add_turnover_rate.sql"
if errorlevel 1 (
    echo 执行失败，请检查数据库连接和 psql 是否在 PATH 中。
    pause
    exit /b 1
)

echo.
echo 验证: 检查 turnover_rate 列是否存在
psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'stock_daily_data' AND column_name = 'turnover_rate';"

echo.
echo ========================================
echo   字段已添加完成
echo ========================================
pause
