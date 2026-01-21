@echo off
chcp 65001 >nul
echo ========================================
echo        股票名称诊断工具
echo ========================================
echo.

set PGPASSWORD=123456
set PGHOST=192.168.1.82
set PGUSER=postgres
set PGDATABASE=aistock

echo --- 1. 总体统计 ---
psql -c "SELECT COUNT(*) as 总记录数, SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL THEN 1 ELSE 0 END) as 有名称, SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL OR stock_name = '' THEN 1 ELSE 0 END) as 缺名称 FROM stock_info;"

echo.
echo --- 2. 按市场统计 ---
psql -c "SELECT COALESCE(market_name, 'NULL') as 市场, COUNT(*) as 总数, SUM(CASE WHEN stock_name <> stock_code THEN 1 ELSE 0 END) as 有名称 FROM stock_info GROUP BY market_name ORDER BY market_name;"

echo.
echo --- 3. 缺少名称的股票(前20个) ---
psql -c "SELECT stock_code, stock_name, market_name FROM stock_info WHERE stock_name = stock_code OR stock_name IS NULL LIMIT 20;"

echo.
echo --- 4. 检查截图中的股票 ---
psql -c "SELECT stock_code, stock_name, market_name FROM stock_info WHERE stock_code IN ('000849','000891','000170','000687','000982','000139') ORDER BY stock_code;"

echo.
echo ========================================
pause
