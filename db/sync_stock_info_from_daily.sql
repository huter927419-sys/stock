-- ============================================
-- 从日线数据同步股票代码到stock_info表
-- 用途：确保所有在日线数据中出现的股票都在stock_info表中有记录
-- ============================================

-- 从 stock_daily_data 提取所有唯一的股票代码，插入到 stock_info 表
-- 如果已存在则不更新（保留现有的股票名称）
INSERT INTO stock_info (stock_code, stock_name, market_code, market_name, stock_type, is_active)
SELECT DISTINCT
    stock_code,
    stock_code AS stock_name,  -- 默认使用股票代码作为名称，后续MQ推送会更新
    COALESCE(market_code, 0),
    CASE
        WHEN market_code = 1 THEN '上海'
        WHEN market_code = 0 THEN '深圳'
        ELSE '未知'
    END AS market_name,
    '股票' AS stock_type,
    TRUE AS is_active
FROM stock_daily_data
WHERE stock_code IS NOT NULL
ON CONFLICT (stock_code) DO NOTHING;  -- 已存在的记录不更新

-- 查看同步结果
SELECT
    COUNT(*) AS total_count,
    SUM(CASE WHEN stock_name = stock_code THEN 1 ELSE 0 END) AS no_name_count,
    SUM(CASE WHEN stock_name <> stock_code THEN 1 ELSE 0 END) AS has_name_count
FROM stock_info;
