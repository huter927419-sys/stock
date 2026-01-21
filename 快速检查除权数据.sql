-- 快速检查除权数据和复权价格状态
-- 数据库: localhost:8532/stockdb

-- ========================================
-- 1. 检查除权数据表是否存在
-- ========================================
SELECT
    '1. 除权数据表状态' as 检查项,
    CASE
        WHEN EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'stock_exrights_data')
        THEN '✓ 表存在'
        ELSE '✗ 表不存在'
    END as 结果;

-- ========================================
-- 2. 统计除权数据
-- ========================================
SELECT
    '2. 除权数据统计' as 检查项,
    COUNT(*) as 总记录数,
    COUNT(DISTINCT stock_code) as 股票数量,
    TO_CHAR(MIN(ex_rights_date), 'YYYY-MM-DD') as 最早日期,
    TO_CHAR(MAX(ex_rights_date), 'YYYY-MM-DD') as 最新日期
FROM stock_exrights_data;

-- ========================================
-- 3. 检查复权价格字段是否存在
-- ========================================
SELECT
    '3. 复权价格字段状态' as 检查项,
    CASE
        WHEN EXISTS (
            SELECT FROM information_schema.columns
            WHERE table_name = 'stock_daily_data'
            AND column_name = 'adjusted_close_price'
        )
        THEN '✓ 字段存在'
        ELSE '✗ 字段不存在'
    END as 结果;

-- ========================================
-- 4. 统计复权价格数据
-- ========================================
SELECT
    '4. 复权价格统计' as 检查项,
    COUNT(*) as 总记录数,
    COUNT(adjusted_close_price) as 有复权价,
    COUNT(*) - COUNT(adjusted_close_price) as 无复权价,
    ROUND(COUNT(adjusted_close_price) * 100.0 / NULLIF(COUNT(*), 0), 2) as 覆盖率百分比
FROM stock_daily_data;

-- ========================================
-- 5. 最近的除权数据示例
-- ========================================
SELECT
    '5. 最近除权数据' as 示例,
    stock_code as 股票代码,
    TO_CHAR(ex_rights_date, 'YYYY-MM-DD') as 除权日期,
    give_per_10_shares as 每10股送,
    pei_per_10_shares as 每10股配,
    pei_price as 配股价,
    profit_per_share as 每股红利
FROM stock_exrights_data
ORDER BY ex_rights_date DESC
LIMIT 5;

-- ========================================
-- 6. 复权价格示例
-- ========================================
SELECT
    '6. 复权价格示例' as 示例,
    stock_code as 股票代码,
    TO_CHAR(trade_date, 'YYYY-MM-DD') as 交易日期,
    close_price as 原始收盘价,
    adjusted_close_price as 复权收盘价,
    ROUND((adjusted_close_price - close_price) / NULLIF(close_price, 0) * 100, 2) as 差异百分比
FROM stock_daily_data
WHERE adjusted_close_price IS NOT NULL
ORDER BY trade_date DESC
LIMIT 5;

-- ========================================
-- 7. 结论和建议
-- ========================================
SELECT
    '7. 诊断结论' as 类型,
    CASE
        WHEN (SELECT COUNT(*) FROM stock_exrights_data) = 0
        THEN '需要导入除权数据'
        WHEN (SELECT COUNT(adjusted_close_price) FROM stock_daily_data) = 0
        THEN '除权数据已有，需要运行复权计算程序'
        WHEN (SELECT COUNT(adjusted_close_price) FROM stock_daily_data) > 0
        THEN '✓ 复权数据完备，可启用复权价格'
        ELSE '状态未知'
    END as 建议;
