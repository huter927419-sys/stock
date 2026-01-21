-- ============================================
-- 获取有效A股股票代码（SQL层面过滤）
-- 在数据库层面就过滤掉非A股、ST股票、退市股票等
-- ============================================
-- 使用方式：
-- psql -h localhost -p 8532 -U postgres -d stockdb -f get_valid_stock_codes.sql
-- ============================================

WITH max_date AS (
    SELECT MAX(trade_date) as latest_date FROM stock_daily_data
),
recent_data AS (
    SELECT 
        d.stock_code,
        COUNT(*) as data_count,
        MAX(d.trade_date) as last_date,
        MIN(d.trade_date) as first_date
    FROM stock_daily_data d
    -- 关联 stock_info 表，获取股票信息
    LEFT JOIN stock_info i ON d.stock_code = i.stock_code
    WHERE d.trade_date >= (SELECT latest_date FROM max_date) - INTERVAL '365 days'
      -- 1. 股票代码格式过滤（A股、创业板、科创板）
      AND d.stock_code ~ '^(600|601|603|605|688|000|001|002|003|300|301)[0-9]{3}$'
      -- 2. 排除已知的无效代码（指数、基金、B股等）
      AND d.stock_code NOT IN (
          '000001',  -- 上证指数
          '000002',  -- A股指数
          '000003',  -- B股指数
          '000005',  -- ST星源（如果需要过滤ST，这里可以添加）
          '000038',  -- 深大通（已退市）
          '000110',  -- 非A股
          '000076',  -- 非A股
          '000974',  -- 非A股
          '000992',  -- 非A股
          '000914',  -- 非A股
          '000046',  -- 非A股
          '000139',  -- 非A股
          '000101'   -- 上证5年期信用债指数
          -- 可以根据需要继续添加
      )
      -- 3. 通过 stock_info 表过滤（如果表中有数据）
      AND (i.stock_code IS NULL OR (
          -- 只选择有效的股票（is_active = TRUE）
          (i.is_active IS NULL OR i.is_active = TRUE)
          -- 排除ST股票（名称中包含ST或*ST）
          AND (i.stock_name IS NULL OR (
              i.stock_name !~ '\*?ST' 
              AND i.stock_name NOT LIKE '%ST%'
          ))
          -- 排除指数、基金等（stock_type = '股票'）
          AND (i.stock_type IS NULL OR i.stock_type = '股票')
      ))
    GROUP BY d.stock_code
    HAVING 
        -- 数据量要求：最近一年至少200个交易日
        COUNT(*) >= 200
        -- 数据新鲜度：最新数据在30天内（排除退市股票）
        AND MAX(d.trade_date) >= (SELECT latest_date FROM max_date) - INTERVAL '30 days'
)
SELECT 
    stock_code,
    (SELECT stock_name FROM stock_info WHERE stock_code = recent_data.stock_code LIMIT 1) as stock_name
FROM recent_data 
ORDER BY stock_code;

-- ============================================
-- 统计信息查询（可选）
-- ============================================
-- SELECT 
--     COUNT(*) as total_valid_stocks,
--     COUNT(CASE WHEN stock_name IS NOT NULL AND stock_name <> stock_code THEN 1 END) as has_name_count,
--     COUNT(CASE WHEN stock_name IS NULL OR stock_name = stock_code THEN 1 END) as missing_name_count
-- FROM (
--     -- 上面的查询
-- ) as stats;
