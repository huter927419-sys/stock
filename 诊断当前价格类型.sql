-- 诊断当前系统使用的价格类型
-- 通过对比原始价格和复权价格来判断

-- 1. 检查复权价格字段是否存在
SELECT
    '1. 系统配置检查' as 检查项,
    CASE
        WHEN EXISTS (
            SELECT FROM information_schema.columns
            WHERE table_name = 'stock_daily_data'
            AND column_name = 'adjusted_close_price'
        ) THEN '✓ 复权价格字段存在'
        ELSE '✗ 复权价格字段不存在（使用原始价格）'
    END as 结果;

-- 2. 检查复权价格是否有值
SELECT
    '2. 复权数据状态' as 检查项,
    COUNT(*) as 总记录数,
    COUNT(adjusted_close_price) as 有复权价格,
    COUNT(*) - COUNT(adjusted_close_price) as 无复权价格,
    CASE
        WHEN COUNT(adjusted_close_price) = 0 THEN '✗ 没有复权数据（系统使用原始价格）'
        WHEN COUNT(adjusted_close_price) < COUNT(*) THEN '⚠ 部分有复权数据'
        ELSE '✓ 全部有复权数据'
    END as 状态
FROM stock_daily_data;

-- 3. 随机抽样对比原始价格和复权价格
SELECT
    '3. 价格对比示例（最近10条）' as 说明,
    stock_code as 股票代码,
    TO_CHAR(trade_date, 'YYYY-MM-DD') as 交易日期,
    close_price as 原始收盘价,
    adjusted_close_price as 复权收盘价,
    CASE
        WHEN adjusted_close_price IS NULL THEN '原始价格'
        WHEN adjusted_close_price = close_price THEN '复权价格=原始价格'
        ELSE '复权价格≠原始价格'
    END as 价格类型,
    CASE
        WHEN adjusted_close_price IS NOT NULL AND adjusted_close_price != close_price
        THEN ROUND((adjusted_close_price - close_price) / close_price * 100, 2)
        ELSE 0
    END as 差异百分比
FROM stock_daily_data
WHERE trade_date >= CURRENT_DATE - INTERVAL '30 days'
ORDER BY trade_date DESC, stock_code
LIMIT 10;

-- 4. 检查代码中当前使用的字段
SELECT
    '4. 代码配置' as 检查项,
    '根据 PostgresKlineDataRepository.cs:42 行' as 位置,
    'SELECT trade_date, open_price, high_price, low_price, close_price, volume' as 当前SQL,
    '使用原始价格（未复权）' as 当前状态,
    '修改为 COALESCE(adjusted_open_price, open_price) 等可切换为复权价格' as 切换方法;

-- 5. 总结当前状态
SELECT
    '5. 总结' as 项目,
    CASE
        WHEN (SELECT COUNT(adjusted_close_price) FROM stock_daily_data) = 0
        THEN '【当前使用：原始价格】- 复权数据不存在，系统只能使用原始价格'
        WHEN (SELECT COUNT(adjusted_close_price) FROM stock_daily_data) > 0
        THEN '【当前使用：原始价格】- 复权数据存在但未启用，需修改代码启用'
        ELSE '状态未知'
    END as 当前状态;

-- 6. 验证：查看除权数据
SELECT
    '6. 除权数据验证' as 检查项,
    COUNT(*) as 除权记录数,
    COUNT(DISTINCT stock_code) as 有除权的股票数,
    CASE
        WHEN COUNT(*) = 0 THEN '没有除权数据'
        ELSE CONCAT('有 ', COUNT(*), ' 条除权记录')
    END as 状态
FROM stock_exrights_data;
