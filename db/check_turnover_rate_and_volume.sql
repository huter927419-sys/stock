-- ============================================
-- 检查日线数据表中的换手率和成交量数据
-- ============================================

-- 1. 检查字段是否存在
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'stock_daily_data' 
  AND column_name IN ('turnover_rate', 'volume', 'amount')
ORDER BY column_name;

-- 2. 统计总记录数、有成交量的记录数、有换手率的记录数
SELECT 
    COUNT(*) as total_records,
    COUNT(volume) as records_with_volume,
    COUNT(amount) as records_with_amount,
    COUNT(turnover_rate) as records_with_turnover_rate,
    COUNT(CASE WHEN volume > 0 THEN 1 END) as records_with_positive_volume,
    COUNT(CASE WHEN turnover_rate IS NOT NULL AND turnover_rate > 0 THEN 1 END) as records_with_positive_turnover_rate
FROM stock_daily_data;

-- 3. 查看最近7天的数据统计（按日期）
SELECT 
    trade_date,
    COUNT(*) as total_records,
    COUNT(volume) as records_with_volume,
    COUNT(turnover_rate) as records_with_turnover_rate,
    COUNT(CASE WHEN volume > 0 THEN 1 END) as records_with_positive_volume,
    COUNT(CASE WHEN turnover_rate IS NOT NULL AND turnover_rate > 0 THEN 1 END) as records_with_positive_turnover_rate,
    AVG(volume) as avg_volume,
    AVG(turnover_rate) as avg_turnover_rate
FROM stock_daily_data
WHERE trade_date >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY trade_date
ORDER BY trade_date DESC;

-- 4. 查看有换手率数据的示例记录（最近10条）
SELECT 
    stock_code,
    trade_date,
    volume,
    amount,
    turnover_rate,
    open_price,
    close_price
FROM stock_daily_data
WHERE turnover_rate IS NOT NULL
ORDER BY trade_date DESC, stock_code
LIMIT 10;

-- 5. 查看没有换手率数据但有成交量的示例记录（最近10条）
SELECT 
    stock_code,
    trade_date,
    volume,
    amount,
    turnover_rate,
    open_price,
    close_price
FROM stock_daily_data
WHERE turnover_rate IS NULL 
  AND volume > 0
ORDER BY trade_date DESC, stock_code
LIMIT 10;

-- 6. 统计有换手率数据的股票数量（最近30天）
SELECT 
    COUNT(DISTINCT stock_code) as stocks_with_turnover_rate
FROM stock_daily_data
WHERE trade_date >= CURRENT_DATE - INTERVAL '30 days'
  AND turnover_rate IS NOT NULL
  AND turnover_rate > 0;

-- 7. 查看换手率数据的分布情况
SELECT 
    CASE 
        WHEN turnover_rate IS NULL THEN 'NULL'
        WHEN turnover_rate = 0 THEN '0'
        WHEN turnover_rate > 0 AND turnover_rate <= 1 THEN '0-1%'
        WHEN turnover_rate > 1 AND turnover_rate <= 3 THEN '1-3%'
        WHEN turnover_rate > 3 AND turnover_rate <= 5 THEN '3-5%'
        WHEN turnover_rate > 5 AND turnover_rate <= 10 THEN '5-10%'
        ELSE '>10%'
    END as turnover_rate_range,
    COUNT(*) as record_count
FROM stock_daily_data
WHERE trade_date >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY 
    CASE 
        WHEN turnover_rate IS NULL THEN 'NULL'
        WHEN turnover_rate = 0 THEN '0'
        WHEN turnover_rate > 0 AND turnover_rate <= 1 THEN '0-1%'
        WHEN turnover_rate > 1 AND turnover_rate <= 3 THEN '1-3%'
        WHEN turnover_rate > 3 AND turnover_rate <= 5 THEN '3-5%'
        WHEN turnover_rate > 5 AND turnover_rate <= 10 THEN '5-10%'
        ELSE '>10%'
    END
ORDER BY 
    CASE 
        WHEN turnover_rate IS NULL THEN 0
        WHEN turnover_rate = 0 THEN 1
        WHEN turnover_rate > 0 AND turnover_rate <= 1 THEN 2
        WHEN turnover_rate > 1 AND turnover_rate <= 3 THEN 3
        WHEN turnover_rate > 3 AND turnover_rate <= 5 THEN 4
        WHEN turnover_rate > 5 AND turnover_rate <= 10 THEN 5
        ELSE 6
    END;
