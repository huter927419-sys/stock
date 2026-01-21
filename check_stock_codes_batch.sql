-- 查询指定股票代码的信息
SELECT 
    stock_code,
    stock_name,
    is_active,
    CASE 
        WHEN stock_code ~ '^(000|001)[0-9]{3}$' THEN '深圳主板'
        WHEN stock_code ~ '^002[0-9]{3}$' THEN '中小板'
        WHEN stock_code ~ '^300[0-9]{3}$' THEN '创业板'
        WHEN stock_code ~ '^(600|601|603|605)[0-9]{3}$' THEN '上海主板'
        WHEN stock_code ~ '^688[0-9]{3}$' THEN '科创板'
        ELSE '其他'
    END AS market_type,
    (SELECT COUNT(*) FROM stock_daily_data WHERE stock_code = si.stock_code) as data_count,
    (SELECT MAX(trade_date) FROM stock_daily_data WHERE stock_code = si.stock_code) as last_trade_date
FROM stock_info si
WHERE stock_code IN (
    '000132', '000091', '000851', '000847', '000071', 
    '000137', '000854', '000073', '000077', '000102', 
    '000146', '000161', '000107'
)
ORDER BY stock_code;

-- 同时检查这些代码在日线数据表中的情况（即使stock_info中没有）
SELECT 
    stock_code,
    stock_name,
    COUNT(*) as data_count,
    MIN(trade_date) as first_date,
    MAX(trade_date) as last_date
FROM stock_daily_data
WHERE stock_code IN (
    '000132', '000091', '000851', '000847', '000071', 
    '000137', '000854', '000073', '000077', '000102', 
    '000146', '000161', '000107'
)
GROUP BY stock_code, stock_name
ORDER BY stock_code;
