-- ============================================
-- 检查内置字典中的股票代码是否正确
-- ============================================

-- 检查内置字典中提到的关键股票
SELECT 
    '内置字典检查' as 检查项,
    stock_code as 代码, 
    stock_name as 数据库名称,
    CASE 
        WHEN stock_code = '000827' THEN '东莞控股(字典中)'
        WHEN stock_code = '000828' THEN '东莞控股(应该是)'
        WHEN stock_code = '000830' THEN '鲁西化工'
        WHEN stock_code = '000851' THEN '高鸿股份'
        WHEN stock_code = '001872' THEN '招商港口'
        WHEN stock_code = '000022' THEN '深赤湾A'
        WHEN stock_code = '600132' THEN '重庆啤酒'
        WHEN stock_code = '000853' THEN '冀东装备'
        WHEN stock_code = '600854' THEN '春兰股份'
        WHEN stock_code = '000891' THEN '阳光城'
        WHEN stock_code = '000982' THEN '宁波能源'
        ELSE '未知'
    END as 字典中名称,
    is_active as 是否活跃
FROM stock_info 
WHERE stock_code IN (
    '000827', '000828', '000830', '000851', 
    '001872', '000022', '600132',
    '000853', '600854', '000891', '000982'
)
ORDER BY stock_code;

-- 检查东莞控股的正确代码
SELECT 
    '东莞控股代码检查' as 检查项,
    stock_code as 代码, 
    stock_name as 名称,
    is_active as 是否活跃
FROM stock_info 
WHERE stock_name LIKE '%东莞%'
ORDER BY stock_code;

-- 检查招商港口的正确代码
SELECT 
    '招商港口代码检查' as 检查项,
    stock_code as 代码, 
    stock_name as 名称,
    is_active as 是否活跃
FROM stock_info 
WHERE stock_name LIKE '%招商港%'
ORDER BY stock_code;

-- 检查重庆啤酒的正确代码
SELECT 
    '重庆啤酒代码检查' as 检查项,
    stock_code as 代码, 
    stock_name as 名称,
    is_active as 是否活跃
FROM stock_info 
WHERE stock_name LIKE '%重庆啤%'
ORDER BY stock_code;
