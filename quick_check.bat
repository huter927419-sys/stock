@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo   快速数据验证
echo ========================================
echo.

set PGPASSWORD=5JkuPVfGrDY6qqzd
set PSQL="F:\dsfr\mqq\tools\bin\psql.exe"

echo 【1】检查股票000001的数据量...
echo.
%PSQL% -h localhost -p 8532 -U postgres -d stockdb -c "SELECT COUNT(*) as total_records, MIN(trade_date) as earliest_date, MAX(trade_date) as latest_date FROM stock_daily_data WHERE stock_code = '000001';"

echo.
echo 【2】检查股票000002的数据量...
echo.
%PSQL% -h localhost -p 8532 -U postgres -d stockdb -c "SELECT COUNT(*) as total_records, MIN(trade_date) as earliest_date, MAX(trade_date) as latest_date FROM stock_daily_data WHERE stock_code = '000002';"

echo.
echo 【3】检查股票600519的数据量（茅台）...
echo.
%PSQL% -h localhost -p 8532 -U postgres -d stockdb -c "SELECT COUNT(*) as total_records, MIN(trade_date) as earliest_date, MAX(trade_date) as latest_date FROM stock_daily_data WHERE stock_code = '600519';"

echo.
echo 【4】统计所有股票的数据量分布...
echo.
%PSQL% -h localhost -p 8532 -U postgres -d stockdb -c "SELECT CASE WHEN record_count < 100 THEN '< 100天' WHEN record_count < 500 THEN '100-500天' WHEN record_count < 1000 THEN '500-1000天' WHEN record_count < 2000 THEN '1000-2000天' WHEN record_count < 3000 THEN '2000-3000天' ELSE '> 3000天' END as data_range, COUNT(*) as stock_count FROM (SELECT stock_code, COUNT(*) as record_count FROM stock_daily_data GROUP BY stock_code) subquery GROUP BY data_range ORDER BY CASE data_range WHEN '< 100天' THEN 1 WHEN '100-500天' THEN 2 WHEN '500-1000天' THEN 3 WHEN '1000-2000天' THEN 4 WHEN '2000-3000天' THEN 5 ELSE 6 END;"

echo.
echo ========================================
echo 验证完成！
echo ========================================
pause
