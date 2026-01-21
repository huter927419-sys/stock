-- ========================================
-- 验证K线数据加载是否完整
-- ========================================

-- 1. 检查典型股票的数据量
SELECT 
    stock_code,
    COUNT(*) as total_records,
    MIN(trade_date) as earliest_date,
    MAX(trade_date) as latest_date,
    MAX(trade_date) - MIN(trade_date) as date_span_days
FROM stock_daily_data
WHERE stock_code IN ('000001', '000002', '600000', '600519')
GROUP BY stock_code
ORDER BY stock_code;

-- 2. 检查所有股票的数据量分布
SELECT 
    CASE 
        WHEN record_count < 100 THEN '< 100天'
        WHEN record_count < 500 THEN '100-500天'
        WHEN record_count < 1000 THEN '500-1000天'
        WHEN record_count < 2000 THEN '1000-2000天'
        WHEN record_count < 3000 THEN '2000-3000天'
        ELSE '> 3000天'
    END as data_range,
    COUNT(*) as stock_count
FROM (
    SELECT stock_code, COUNT(*) as record_count
    FROM stock_daily_data
    GROUP BY stock_code
) subquery
GROUP BY data_range
ORDER BY 
    CASE data_range
        WHEN '< 100天' THEN 1
        WHEN '100-500天' THEN 2
        WHEN '500-1000天' THEN 3
        WHEN '1000-2000天' THEN 4
        WHEN '2000-3000天' THEN 5
        ELSE 6
    END;

-- 3. 检查数据最全的前10只股票
SELECT 
    stock_code,
    COUNT(*) as total_records,
    MIN(trade_date) as earliest_date,
    MAX(trade_date) as latest_date
FROM stock_daily_data
GROUP BY stock_code
ORDER BY total_records DESC
LIMIT 10;

-- 4. 检查最近更新的股票（验证数据是否最新）
SELECT 
    stock_code,
    MAX(trade_date) as latest_date,
    COUNT(*) as total_records
FROM stock_daily_data
GROUP BY stock_code
ORDER BY latest_date DESC
LIMIT 20;

-- 5. 检查股票000001的完整信息（平安银行）
SELECT 
    COUNT(*) as total_records,
    MIN(trade_date) as earliest_date,
    MAX(trade_date) as latest_date,
    MAX(trade_date) - MIN(trade_date) as date_span_days,
    ROUND(COUNT(*)::numeric / NULLIF((MAX(trade_date) - MIN(trade_date))::numeric, 0) * 365, 2) as avg_trading_days_per_year
FROM stock_daily_data
WHERE stock_code = '000001';

-- 6. 查看000001最早和最晚的几条记录
(SELECT '最早5条' as type, trade_date, open_price, high_price, low_price, close_price, volume
 FROM stock_daily_data
 WHERE stock_code = '000001'
 ORDER BY trade_date ASC
 LIMIT 5)
UNION ALL
(SELECT '最晚5条' as type, trade_date, open_price, high_price, low_price, close_price, volume
 FROM stock_daily_data
 WHERE stock_code = '000001'
 ORDER BY trade_date DESC
 LIMIT 5)
ORDER BY 
    CASE type WHEN '最早5条' THEN 1 ELSE 2 END,
    trade_date;
