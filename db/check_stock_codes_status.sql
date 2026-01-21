-- 检查指定股票代码在数据库中的状态
-- 包括：是否在stock_info表中，是否激活，名称是什么，市场代码是什么

WITH codes AS (
    SELECT unnest(ARRAY['000022','000071','000033','000132','000005','000053','000052','000110','002043']) AS stock_code
)
SELECT
    c.stock_code,
    CASE 
        WHEN si.stock_code IS NULL THEN '不存在'
        WHEN si.is_active = FALSE THEN '未激活'
        ELSE '已激活'
    END AS status,
    COALESCE(si.stock_name, '-') AS stock_name,
    COALESCE(si.market_code::text, '-') AS market_code,
    COALESCE(si.market_name, '-') AS market_name,
    CASE 
        WHEN si.stock_code IS NULL THEN '不在stock_info表中'
        WHEN si.stock_name IS NULL OR si.stock_name = '' OR si.stock_name = si.stock_code THEN '名称缺失'
        ELSE '有名称'
    END AS name_status,
    CASE 
        -- 判断是否为A股或创业板
        WHEN c.stock_code ~ '^(600|601|603|605|688)' THEN '沪市主板/科创板'
        WHEN c.stock_code ~ '^(000|001|002|003|004)' THEN '深市主板/中小板'
        WHEN c.stock_code ~ '^(300|301)' THEN '创业板'
        ELSE '未知'
    END AS stock_type
FROM codes c
LEFT JOIN stock_info si ON c.stock_code = si.stock_code
ORDER BY c.stock_code;
