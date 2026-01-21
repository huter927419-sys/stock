-- ===================================================
-- 修正明星电力的代码-名称映射错误
-- 问题：000101被错误地映射为"明星电力"
-- 正确：000101应该是"恒邦股份"，600101才是"明星电力"
-- ===================================================

BEGIN;

-- 备份当前数据（可选）
SELECT '修复前的数据：' as 说明;
SELECT stock_code, stock_name, market_code, market_name, is_active
FROM stock_info
WHERE stock_code IN ('000101', '600101');

-- 修正000101（深圳主板 - 恒邦股份）
UPDATE stock_info 
SET stock_name = '恒邦股份',
    market_code = 0,
    market_name = '深圳',
    update_time = CURRENT_TIMESTAMP
WHERE stock_code = '000101';

-- 修正600101（上海主板 - 明星电力）
UPDATE stock_info 
SET stock_name = '明星电力',
    market_code = 1,
    market_name = '上海',
    is_active = TRUE,  -- 确保是活跃状态
    update_time = CURRENT_TIMESTAMP
WHERE stock_code = '600101';

-- 如果600101不存在，则插入
INSERT INTO stock_info (stock_code, stock_name, market_code, market_name, is_active, update_time)
SELECT '600101', '明星电力', 1, '上海', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM stock_info WHERE stock_code = '600101');

COMMIT;

-- 验证修复结果
SELECT '修复后的数据：' as 说明;
SELECT stock_code, stock_name, market_code, market_name, is_active
FROM stock_info
WHERE stock_code IN ('000101', '600101')
ORDER BY stock_code;

-- 预期结果：
-- 000101 | 恒邦股份 | 0 | 深圳 | TRUE
-- 600101 | 明星电力 | 1 | 上海 | TRUE

SELECT '修复完成！' as 状态;
