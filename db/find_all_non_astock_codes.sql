-- ===================================================
-- 系统性查找所有非A股代码（指数、债券、基金等）
-- 生成时间: 2026-01-20
-- ===================================================

-- 1. 检查所有活跃的000开头但可能不是A股的代码
SELECT '【000开头的可疑代码】' as 检查类型;
SELECT 
    stock_code, 
    stock_name, 
    market_code, 
    market_name,
    is_active,
    CASE 
        WHEN stock_name LIKE '%指数%' THEN '⚠️ 指数'
        WHEN stock_name LIKE '%债%' THEN '⚠️ 债券'
        WHEN stock_name LIKE '%基金%' THEN '⚠️ 基金'
        WHEN stock_name LIKE '%ETF%' THEN '⚠️ ETF'
        WHEN stock_name LIKE '%LOF%' THEN '⚠️ LOF基金'
        WHEN stock_code ~ '^0000[0-9][0-9]$' THEN '⚠️ 可疑（000000-000099通常是指数）'
        ELSE '需人工确认'
    END as 类型判断
FROM stock_info
WHERE stock_code ~ '^000[0-9]{3}$'
  AND is_active = TRUE
  AND (
    stock_name LIKE '%指数%' OR
    stock_name LIKE '%债%' OR
    stock_name LIKE '%基金%' OR
    stock_name LIKE '%ETF%' OR
    stock_name LIKE '%LOF%' OR
    stock_code ~ '^0000[0-9][0-9]$'  -- 000000-000099 通常是指数
  )
ORDER BY stock_code;

-- 2. 检查000000-000199范围（此范围通常是指数/特殊代码）
SELECT '【000000-000199范围（高危区域）】' as 检查类型;
SELECT 
    stock_code, 
    stock_name, 
    market_code,
    is_active,
    CASE 
        WHEN stock_code::int < 100 THEN '⚠️ 极高可能性是指数（<000100）'
        WHEN stock_code::int < 200 THEN '⚠️ 高可能性是指数/特殊代码（<000200）'
    END as 风险等级
FROM stock_info
WHERE stock_code ~ '^000[0-1][0-9]{2}$'  -- 000000-000199
  AND is_active = TRUE
ORDER BY stock_code;

-- 3. 查找名称中包含关键词的非A股代码
SELECT '【关键词匹配：指数/债券/基金】' as 检查类型;
SELECT 
    stock_code, 
    stock_name,
    CASE 
        WHEN stock_name LIKE '%指数%' THEN '指数'
        WHEN stock_name LIKE '%债券%' OR stock_name LIKE '%债%' THEN '债券'
        WHEN stock_name LIKE '%基金%' THEN '基金'
        WHEN stock_name LIKE '%ETF%' THEN 'ETF'
        WHEN stock_name LIKE '%LOF%' THEN 'LOF'
    END as 类型
FROM stock_info
WHERE is_active = TRUE
  AND (
    stock_name LIKE '%指数%' OR
    stock_name LIKE '%债券%' OR
    stock_name LIKE '%债%' OR
    stock_name LIKE '%基金%' OR
    stock_name LIKE '%ETF%' OR
    stock_name LIKE '%LOF%'
  )
ORDER BY stock_code;

-- 4. 检查已知的问题代码（根据之前发现的模式）
SELECT '【已知问题代码验证】' as 检查类型;
SELECT 
    stock_code, 
    stock_name, 
    is_active,
    CASE 
        WHEN stock_code = '000101' THEN '上证5年期信用债指数（已确认）'
        WHEN stock_code = '000038' THEN '上证金融指数'
        WHEN stock_code = '000110' THEN '380金融指数'
        WHEN stock_code = '000914' THEN '300金融指数'
        WHEN stock_code = '000992' THEN '全指金融指数'
        WHEN stock_code = '000139' THEN '富国国企债基金'
        WHEN stock_code = '000046' THEN '*ST泛海（已退市）'
        ELSE '其他'
    END as 说明
FROM stock_info
WHERE stock_code IN (
    '000038', '000046', '000076', '000091', '000101', '000102', 
    '000110', '000132', '000137', '000139', '000146', 
    '000914', '000974', '000992'
)
ORDER BY stock_code;

-- 5. 检查B股代码（200xxx, 900xxx）
SELECT '【B股代码】' as 检查类型;
SELECT stock_code, stock_name, market_code, is_active
FROM stock_info
WHERE (stock_code ~ '^200' OR stock_code ~ '^900')
  AND is_active = TRUE
ORDER BY stock_code;

-- 6. 统计各类型数量
SELECT '【统计摘要】' as 检查类型;
SELECT 
    '指数类' as 类型,
    COUNT(*) as 数量
FROM stock_info
WHERE is_active = TRUE AND stock_name LIKE '%指数%'
UNION ALL
SELECT '债券类', COUNT(*) FROM stock_info WHERE is_active = TRUE AND (stock_name LIKE '%债%' OR stock_name LIKE '%债券%')
UNION ALL
SELECT '基金类', COUNT(*) FROM stock_info WHERE is_active = TRUE AND (stock_name LIKE '%基金%' OR stock_name LIKE '%ETF%' OR stock_name LIKE '%LOF%')
UNION ALL
SELECT 'B股', COUNT(*) FROM stock_info WHERE is_active = TRUE AND (stock_code ~ '^200' OR stock_code ~ '^900')
UNION ALL
SELECT '000000-000099', COUNT(*) FROM stock_info WHERE is_active = TRUE AND stock_code ~ '^0000[0-9]{2}$';

-- 7. 生成待确认列表（需人工在线查询）
SELECT '【待人工确认的代码列表】' as 检查类型;
SELECT 
    stock_code,
    stock_name,
    '访问 https://quote.eastmoney.com/concept/sz' || stock_code || '.html 确认' as 查询建议
FROM stock_info
WHERE stock_code ~ '^0000[0-9]{2}$'  -- 000000-000099
  AND is_active = TRUE
  AND stock_name NOT LIKE '%指数%'
  AND stock_name NOT LIKE '%债%'
  AND stock_name NOT LIKE '%基金%'
ORDER BY stock_code;

-- ===================================================
-- 执行建议：
-- 1. 在 pgAdmin 中执行此脚本
-- 2. 记录所有返回的可疑代码
-- 3. 对于每个代码，访问东方财富或新浪财经确认类型
-- 4. 将确认的非A股代码添加到黑名单
-- ===================================================
