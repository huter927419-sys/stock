-- 快速诊断SQL脚本
-- 请在PostgreSQL中执行以下查询，然后把结果复制给我

-- 查询1：检查最近的日线数据和成交金额
SELECT
    trade_date,
    COUNT(*) as total_stocks,
    COUNT(amount) as has_amount,
    COUNT(CASE WHEN amount >= 500000000 THEN 1 END) as amount_gte_5yi,
    COUNT(CASE WHEN amount >= 200000000 THEN 1 END) as amount_gte_2yi,
    ROUND(AVG(amount) / 100000000, 2) as avg_amount_yi
FROM daily_data
WHERE trade_date >= CURRENT_DATE - INTERVAL '5 days'
GROUP BY trade_date
ORDER BY trade_date DESC;

-- 预期结果示例：
-- trade_date  | total_stocks | has_amount | amount_gte_5yi | amount_gte_2yi | avg_amount_yi
-- 2026-01-30  |     4246     |    4246    |      800       |      1500      |      3.5
--
-- 如果 has_amount = 0 或很少 → 成交金额数据缺失
-- 如果 amount_gte_5yi = 0 → N=5亿的阈值太高


-- 查询2：检查KD数据是否存在（如果有kd_values表）
SELECT COUNT(DISTINCT stock_code) as stocks_with_kd
FROM kd_values
WHERE trade_date >= CURRENT_DATE - INTERVAL '3 days';

-- 预期结果：
-- stocks_with_kd
--     3500
--
-- 如果 = 0 → KD数据未入库（可能是批量计算模式，KD在内存中）


-- 查询3：随机查看5只股票的最新数据
SELECT
    dd.stock_code,
    dd.trade_date,
    dd.close,
    dd.amount / 100000000 as amount_yi,
    dd.turnover_rate
FROM daily_data dd
WHERE dd.trade_date = (SELECT MAX(trade_date) FROM daily_data)
  AND dd.amount IS NOT NULL
ORDER BY dd.amount DESC
LIMIT 5;

-- 预期结果示例：
-- stock_code | trade_date | close  | amount_yi | turnover_rate
-- 600519     | 2026-01-30 | 1850.5 |    25.5   |     0.85
-- 000001     | 2026-01-30 | 12.34  |    15.2   |     1.20
--
-- 如果没有数据 → 日线数据未入库


-- 查询4：检查配置中的阈值（如果存储在数据库中）
-- 如果配置在文件中，请查看配置文件并提供以下值：
-- GlobalThreshold_M1 = ?
-- GlobalThreshold_M2 = ?
-- GlobalThreshold_M3 = ?
-- GlobalThreshold_M4 = ?
-- GlobalThreshold_N = ?
-- PriceChangeFilterThreshold = ?
