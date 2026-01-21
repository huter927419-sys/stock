-- 检查各表的数据量
SELECT '股票信息表' as table_name, COUNT(*) as count FROM stock_info;
SELECT '日线数据表' as table_name, COUNT(*) as count FROM stock_daily_data;
SELECT '实时数据表' as table_name, COUNT(*) as count FROM stock_realtime_data;
SELECT '除权数据表' as table_name, COUNT(*) as count FROM stock_exrights_data;

-- 检查日线数据的日期范围
SELECT '日线数据日期范围' as info, MIN(trade_date) as min_date, MAX(trade_date) as max_date FROM stock_daily_data;

-- 检查最近5个交易日的数据量
SELECT trade_date, COUNT(DISTINCT stock_code) as stock_count 
FROM stock_daily_data 
WHERE trade_date >= CURRENT_DATE - INTERVAL '10 days'
GROUP BY trade_date 
ORDER BY trade_date DESC 
LIMIT 10;

-- 检查实时数据的更新时间
SELECT '实时数据更新时间' as info, MIN(update_time) as min_time, MAX(update_time) as max_time FROM stock_realtime_data;

-- 检查股票信息表中名称缺失情况
SELECT 
    '股票名称统计' as info,
    COUNT(*) as total,
    SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL OR stock_name = '' THEN 1 ELSE 0 END) as missing_name,
    SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL AND stock_name <> '' THEN 1 ELSE 0 END) as has_name
FROM stock_info;

-- 抽样检查几只股票的日线数据
SELECT stock_code, trade_date, open_price, high_price, low_price, close_price, volume
FROM stock_daily_data
WHERE stock_code IN ('000001', '600000', '300001')
ORDER BY stock_code, trade_date DESC
LIMIT 15;
