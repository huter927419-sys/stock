-- 同步股票代码：从日线数据同步到stock_info表
-- 运行方式: psql -h localhost -p 8532 -U postgres -d stockdb -f sync_stock_codes.sql

-- 1. 查看当前状态
SELECT '当前stock_info表记录数' as info, COUNT(*) as count FROM stock_info;
SELECT '日线数据中股票数量' as info, COUNT(DISTINCT stock_code) as count FROM stock_daily_data;

-- 2. 同步股票代码到stock_info表
INSERT INTO stock_info (stock_code, stock_name, market_code, market_name, stock_type, is_active)
SELECT DISTINCT ON (stock_code)
    stock_code,
    stock_code AS stock_name,
    CASE
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN 1
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN 0
        WHEN stock_code ~ '^(43|83|87|88)' THEN 2
        ELSE 0
    END AS market_code,
    CASE
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海'
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳'
        WHEN stock_code ~ '^(43|83|87|88)' THEN '北京'
        ELSE '深圳'
    END AS market_name,
    '股票' AS stock_type,
    TRUE AS is_active
FROM stock_daily_data
WHERE stock_code IS NOT NULL
  AND stock_code ~ '^[0-9]{6}$'
ON CONFLICT (stock_code) DO UPDATE SET
    market_code = EXCLUDED.market_code,
    market_name = EXCLUDED.market_name,
    update_time = CURRENT_TIMESTAMP;

-- 3. 查看同步后的统计
SELECT '同步后统计' as info;
SELECT
    COUNT(*) AS total,
    SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL OR stock_name = '' THEN 1 ELSE 0 END) AS missing_name,
    SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL AND stock_name <> '' THEN 1 ELSE 0 END) AS has_name
FROM stock_info;

-- 4. 查看最近添加的股票（按更新时间）
SELECT '最近更新的10只股票' as info;
SELECT stock_code, stock_name, market_name, update_time
FROM stock_info
ORDER BY update_time DESC
LIMIT 10;
