-- ============================================
-- 更新新验证的股票代码
-- ============================================

-- 标记无效代码（指数和基金）
UPDATE stock_info SET is_active = FALSE WHERE stock_code IN (
    '000038',  -- 上证金融指数
    '000110',  -- 380金融指数
    '000076',  -- 华夏恒生ETF联接现钞（基金）
    '000974',  -- 安信消费医药股票A（基金）
    '000992'   -- 全指金融指数
);

-- 确保000005是有效的A股
UPDATE stock_info 
SET stock_name = 'ST星源', is_active = TRUE, update_time = CURRENT_TIMESTAMP 
WHERE stock_code = '000005';

-- 验证结果
SELECT '验证结果' as 状态;

-- 查看000005
SELECT stock_code, stock_name, is_active 
FROM stock_info 
WHERE stock_code = '000005';

-- 查看被过滤的代码
SELECT stock_code, stock_name, is_active 
FROM stock_info 
WHERE stock_code IN ('000038', '000110', '000076', '000974', '000992')
ORDER BY stock_code;

-- 统计
SELECT 
    CASE WHEN is_active THEN '有效A股' ELSE '已过滤' END as 类型,
    COUNT(*) as 数量
FROM stock_info 
WHERE stock_code ~ '^[0-9]{6}$'
GROUP BY is_active;
