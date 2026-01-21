-- ============================================
-- 批量更新股票信息（第二批）
-- ============================================

-- 1. 更新有效的A股股票信息
UPDATE stock_info 
SET 
    stock_name = '高鸿股份',
    is_active = TRUE
WHERE stock_code = '000851';

-- 2. 将非A股代码设置为无效（指数、基金、退市等）
UPDATE stock_info 
SET is_active = FALSE
WHERE stock_code IN (
    '000091',  -- 沪财中小指数
    '000071',  -- 华夏恒生ETF联接A基金
    '000132',  -- 上证100指数
    '000137',  -- 非A股
    '000102',  -- 上证投资品指数
    '000146',  -- 优势制造指数
    '000073',  -- 无数据/已退市
    '000077',  -- 无数据/已退市
    '000847',  -- 无数据
    '000854',  -- 无数据
    '000161',  -- 无数据
    '000107'   -- 无数据
);

-- 3. 验证更新结果
SELECT 
    stock_code,
    stock_name,
    is_active,
    CASE 
        WHEN is_active THEN '✅ 有效A股'
        ELSE '❌ 已过滤'
    END as status
FROM stock_info
WHERE stock_code IN (
    '000132', '000091', '000851', '000847', '000071', 
    '000137', '000854', '000073', '000077', '000102', 
    '000146', '000161', '000107'
)
ORDER BY is_active DESC, stock_code;
