-- 只保留A股和创业板股票，其他全部过滤
-- 1. 更新有效的A股股票信息
UPDATE stock_info 
SET 
    stock_name = '高鸿股份',
    is_active = TRUE
WHERE stock_code = '000851';

-- 2. 将非A股代码设置为不显示
UPDATE stock_info 
SET is_active = FALSE
WHERE stock_code IN (
    '000091', '000071', '000132', '000137', '000854',
    '000073', '000077', '000102', '000146', '000161', '000107'
);

-- 3. 验证结果
SELECT 
    stock_code AS "股票代码",
    stock_name AS "股票名称",
    CASE 
        WHEN is_active THEN 'A股/创业板（显示）'
        ELSE '非A股（不显示）'
    END AS "状态"
FROM stock_info
WHERE stock_code IN (
    '000132', '000091', '000851', '000847', '000071', 
    '000137', '000854', '000073', '000077', '000102', 
    '000146', '000161', '000107'
)
ORDER BY is_active DESC, stock_code;
