-- 快速检查stock_info表
SELECT 
    COUNT(*) as "总记录数",
    SUM(CASE WHEN is_active = TRUE THEN 1 ELSE 0 END) as "激活数",
    SUM(CASE WHEN stock_name IS NOT NULL AND stock_name != '' AND stock_name != stock_code THEN 1 ELSE 0 END) as "有名称数",
    SUM(CASE WHEN stock_name IS NULL OR stock_name = '' OR stock_name = stock_code THEN 1 ELSE 0 END) as "无名称数"
FROM stock_info;

-- 检查可疑代码
SELECT '检查可疑代码:' as "检查项";
SELECT stock_code, stock_name, is_active 
FROM stock_info 
WHERE stock_code IN ('000001', '000300', '000139', '000046', '000914')
ORDER BY stock_code;

-- 检查市场代码错误
SELECT '检查市场代码错误:' as "检查项";
SELECT COUNT(*) as "市场代码错误数量"
FROM stock_info
WHERE is_active = TRUE AND (
    (stock_code ~ '^(600|601|603|605|688)' AND market_code != 1) OR
    (stock_code ~ '^(000|001|002|003|004|300|301)' AND market_code != 0)
);
