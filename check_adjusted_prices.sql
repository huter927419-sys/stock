-- 检查复权价格字段
SELECT '=== 复权价格字段统计 ===' as info;
SELECT 
    COUNT(*) as 总记录数,
    COUNT(adjusted_close_price) as 有复权收盘价,
    COUNT(*) - COUNT(adjusted_close_price) as 无复权收盘价,
    ROUND(COUNT(adjusted_close_price) * 100.0 / COUNT(*), 2) as 复权数据覆盖率
FROM stock_daily_data;

SELECT '=== 按股票统计复权数据 ===' as info;
SELECT 
    stock_code as 股票代码,
    COUNT(*) as 总记录数,
    COUNT(adjusted_close_price) as 有复权价,
    MIN(trade_date) as 最早日期,
    MAX(trade_date) as 最新日期
FROM stock_daily_data
GROUP BY stock_code
ORDER BY stock_code
LIMIT 10;

SELECT '=== 复权价格示例（如果有） ===' as info;
SELECT 
    stock_code as 股票代码,
    trade_date as 交易日期,
    close_price as 原始收盘价,
    adjusted_close_price as 复权收盘价,
    ROUND((adjusted_close_price - close_price) / close_price * 100, 2) as 差异百分比
FROM stock_daily_data
WHERE adjusted_close_price IS NOT NULL
ORDER BY trade_date DESC
LIMIT 10;
