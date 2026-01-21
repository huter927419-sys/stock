-- ============================================
-- 修复 stock_info 表数据
-- 1. 修复 stock_name 为 null 的记录（设置为股票代码）
-- 2. 修复 market_code 错误的记录（根据股票代码前缀判断）
-- ============================================

-- 1. 修复 stock_name 为 null 的记录
UPDATE stock_info
SET stock_name = stock_code
WHERE stock_name IS NULL;

-- 2. 修复 market_code
-- 上海(1): 600/601/603/605(主板), 688(科创板), 900(B股)
-- 深圳(0): 000/001(主板), 002/003/004(中小板), 300/301(创业板), 200(B股)
-- 北京(2): 43/83/87/88开头
UPDATE stock_info
SET
    market_code = CASE
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN 1
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN 0
        WHEN stock_code ~ '^(43|83|87|88)' THEN 2
        ELSE 0
    END,
    market_name = CASE
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海'
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳'
        WHEN stock_code ~ '^(43|83|87|88)' THEN '北京'
        ELSE '深圳'
    END
WHERE market_code NOT IN (0, 1, 2) OR market_code IS NULL;

-- 3. 查看修复后的统计
SELECT
    COUNT(*) AS total_count,
    SUM(CASE WHEN stock_name = stock_code THEN 1 ELSE 0 END) AS no_name_count,
    SUM(CASE WHEN stock_name <> stock_code THEN 1 ELSE 0 END) AS has_name_count,
    SUM(CASE WHEN market_code = 1 THEN 1 ELSE 0 END) AS shanghai_count,
    SUM(CASE WHEN market_code = 0 THEN 1 ELSE 0 END) AS shenzhen_count,
    SUM(CASE WHEN market_code = 2 THEN 1 ELSE 0 END) AS beijing_count
FROM stock_info;

-- 4. 查看前20条记录
SELECT stock_code, stock_name, market_code, market_name, stock_type
FROM stock_info
ORDER BY stock_code
LIMIT 20;
