@echo off
chcp 65001 > nul
echo ========================================
echo   除权数据和复权价格检查工具
echo ========================================
echo.

set PGHOST=localhost
set PGPORT=8532
set PGDATABASE=stockdb
set PGUSER=postgres
set PGPASSWORD=cd123321

echo 正在连接数据库: %PGHOST%:%PGPORT%/%PGDATABASE%
echo.

echo ========================================
echo 1. 检查除权数据表
echo ========================================
psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'stock_exrights_data') as table_exists;"

psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT COUNT(*) as 总记录数, COUNT(DISTINCT stock_code) as 股票数量, MIN(ex_rights_date) as 最早除权日期, MAX(ex_rights_date) as 最新除权日期 FROM stock_exrights_data;"

echo.
echo 最近10条除权数据:
psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT stock_code, ex_rights_date, give_per_10_shares as 送股, pei_per_10_shares as 配股, pei_price as 配股价, profit_per_share as 红利 FROM stock_exrights_data ORDER BY ex_rights_date DESC LIMIT 10;"

echo.
echo ========================================
echo 2. 检查复权价格字段
echo ========================================
psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'stock_daily_data' AND column_name = 'adjusted_close_price') as has_adjusted_price;"

psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT COUNT(*) as 总记录数, COUNT(adjusted_close_price) as 有复权价, COUNT(*) - COUNT(adjusted_close_price) as 无复权价, ROUND(COUNT(adjusted_close_price) * 100.0 / COUNT(*), 2) as 覆盖率 FROM stock_daily_data;"

echo.
echo 复权价格示例:
psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT stock_code, trade_date, close_price as 原价, adjusted_close_price as 复权价 FROM stock_daily_data WHERE adjusted_close_price IS NOT NULL ORDER BY trade_date DESC LIMIT 5;"

echo.
echo ========================================
echo 检查完成
echo ========================================
pause
