-- 快速测试：查看股票000001的K线数据是否有变化
-- 如果High, Low都相同，RSV会是固定值，导致K-D差值为0

SELECT 
    trade_date,
    open_price,
    high_price,
    low_price,
    close_price,
    high_price - low_price as price_range,
    CASE 
        WHEN high_price = low_price THEN '涨跌停或无波动'
        ELSE '正常波动'
    END as status
FROM stock_daily_data
WHERE stock_code = '000001'
ORDER BY trade_date DESC
LIMIT 20;

-- 统计最近一年有多少天是涨跌停或无波动的
SELECT 
    COUNT(*) as total_days,
    SUM(CASE WHEN high_price = low_price THEN 1 ELSE 0 END) as no_fluctuation_days,
    AVG(high_price - low_price) as avg_price_range,
    MIN(high_price - low_price) as min_price_range,
    MAX(high_price - low_price) as max_price_range
FROM stock_daily_data
WHERE stock_code = '000001'
  AND trade_date >= CURRENT_DATE - INTERVAL '1 year';

-- 检查是否所有数据的high=low（这会导致RSV=50，K和D最终都收敛到50）
SELECT 
    CASE 
        WHEN high_price = low_price THEN '无波动'
        ELSE '有波动'
    END as fluctuation_status,
    COUNT(*) as count
FROM stock_daily_data
WHERE stock_code = '000001'
GROUP BY CASE WHEN high_price = low_price THEN '无波动' ELSE '有波动' END;
