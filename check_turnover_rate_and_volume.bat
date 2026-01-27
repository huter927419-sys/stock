@echo off
REM 检查日线数据表中的换手率和成交量数据

set PGHOST=localhost
set PGPORT=8532
set PGUSER=postgres
set PGDATABASE=stockdb
set PGPASSWORD=cd123321

echo ============================================
echo 检查日线数据表中的换手率和成交量数据
echo ============================================
echo.

REM 设置PGPASSWORD环境变量
set PGPASSWORD=%PGPASSWORD%

REM 执行SQL查询
psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -f "%~dp0db\check_turnover_rate_and_volume.sql"

echo.
echo ============================================
echo 查询完成
echo ============================================
pause
