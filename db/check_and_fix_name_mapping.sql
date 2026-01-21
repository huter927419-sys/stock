-- 股票代码-名称映射检查和修复
-- 特别关注"明星电力"等可能错误的映射

BEGIN;

-- 1. 检查当前的"明星电力"映射
SELECT '=== 当前明星电力映射 ===' as title;
SELECT stock_code, stock_name, is_active, market_code, update_time
FROM stock_info
WHERE stock_name LIKE '%明星%' OR stock_code IN ('000101', '000722')
ORDER BY stock_code;

-- 2. 检查这些代码的最近交易数据（验证实际是哪只股票）
SELECT '=== 000101最近交易数据 ===' as title;
SELECT trade_date, open, high, low, close, volume
FROM stock_daily_data
WHERE stock_code = '000101'
ORDER BY trade_date DESC
LIMIT 5;

-- 3. 检查所有可能重复或错误的名称映射
SELECT '=== 可能错误的映射 ===' as title;
SELECT stock_code, stock_name, COUNT(*) as appear_count
FROM stock_info
WHERE stock_name IN (
    SELECT stock_name
    FROM stock_info
    WHERE stock_name IS NOT NULL AND stock_name != ''
    GROUP BY stock_name
    HAVING COUNT(DISTINCT stock_code) > 1
)
GROUP BY stock_code, stock_name
ORDER BY stock_name, stock_code;

-- 4. 修复明星电力的映射（如果000101不是明星电力）
-- 根据实际情况，您可能需要调整这个UPDATE语句
-- SELECT '=== 建议的修复 ===' as title;
-- UPDATE stock_info
-- SET stock_name = '正确的名称', 
--     update_time = CURRENT_TIMESTAMP
-- WHERE stock_code = '000101';

ROLLBACK;  -- 先回滚，让用户确认后再手动执行修复

-- 提示信息
SELECT '=== 检查完成 ===' as title;
SELECT '请根据上述检查结果，确认000101的正确名称' as message;
SELECT '如果需要修复，请在上面的UPDATE语句中填入正确名称后执行' as next_step;
