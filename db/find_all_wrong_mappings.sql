-- ===================================================
-- 全面检查股票代码-名称映射错误
-- 生成时间: 2026-01-20
-- ===================================================

-- 1. 检查明星电力的映射（重点错误）
SELECT '【重点错误：明星电力】' as 检查类型;
SELECT stock_code, stock_name, market_code, market_name, is_active
FROM stock_info
WHERE stock_code IN ('000101', '600101')
ORDER BY stock_code;

-- 2. 检查市场代码不匹配（上海股票market_code应该是1，深圳应该是0）
SELECT '【市场代码不匹配】' as 检查类型;
SELECT stock_code, stock_name, market_code, market_name, is_active
FROM stock_info
WHERE is_active = TRUE 
  AND (
    (stock_code ~ '^6' AND market_code != 1) OR  -- 上海股票但市场代码不是1
    (stock_code ~ '^0|^3' AND market_code != 0)   -- 深圳/创业板但市场代码不是0
  )
ORDER BY stock_code
LIMIT 100;

-- 3. 检查重复的股票名称（同一个名称对应多个代码）
SELECT '【重复名称】' as 检查类型;
SELECT stock_name, STRING_AGG(stock_code, ', ' ORDER BY stock_code) as 代码列表, COUNT(*) as 重复次数
FROM stock_info
WHERE stock_name IS NOT NULL 
  AND stock_name != '' 
  AND stock_name != stock_code 
  AND is_active = TRUE
GROUP BY stock_name
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC, stock_name
LIMIT 50;

-- 4. 检查市场名称不匹配
SELECT '【市场名称错误】' as 检查类型;
SELECT stock_code, stock_name, market_code, market_name, is_active
FROM stock_info
WHERE is_active = TRUE 
  AND (
    (stock_code ~ '^6' AND market_name != '上海') OR
    (stock_code ~ '^0|^3' AND market_name != '深圳')
  )
ORDER BY stock_code
LIMIT 100;

-- 5. 检查名称不完整的股票（只有代码没有名称，或名称就是代码）
SELECT '【名称缺失或不完整】' as 检查类型;
SELECT stock_code, stock_name, market_code, is_active
FROM stock_info
WHERE is_active = TRUE 
  AND (
    stock_name IS NULL OR 
    stock_name = '' OR 
    stock_name = stock_code OR
    stock_name ~ '^[S\*]*ST.?$'  -- 只有"ST"、"*ST"等不完整名称
  )
ORDER BY stock_code
LIMIT 100;

-- 6. 检查疑似非A股的代码（这些应该被标记为is_active=FALSE）
SELECT '【疑似非A股代码】' as 检查类型;
SELECT stock_code, stock_name, market_code, is_active
FROM stock_info
WHERE is_active = TRUE
  AND stock_code IN (
    '000005',  -- 需要确认
    '000038',  -- 需要确认
    '000046',  -- *ST泛海（已退市）
    '000076',  -- 需要确认
    '000110',  -- 需要确认
    '000139',  -- 富国国企债基金（非股票）
    '000914',  -- 300金融指数
    '000974',  -- 需要确认
    '000992'   -- 需要确认
  )
ORDER BY stock_code;

-- 7. 统计总体数据质量
SELECT '【数据质量统计】' as 检查类型;
SELECT 
    COUNT(*) as 总记录数,
    SUM(CASE WHEN is_active = TRUE THEN 1 ELSE 0 END) as 活跃记录数,
    SUM(CASE WHEN stock_name IS NULL OR stock_name = '' OR stock_name = stock_code THEN 1 ELSE 0 END) as 名称缺失数,
    SUM(CASE WHEN (stock_code ~ '^6' AND market_code != 1) OR (stock_code ~ '^0|^3' AND market_code != 0) THEN 1 ELSE 0 END) as 市场代码错误数
FROM stock_info;

-- 8. 查找000101（当前错误）和600101（正确）的完整信息对比
SELECT '【明星电力详细对比】' as 检查类型;
SELECT 
    stock_code, 
    stock_name, 
    market_code, 
    market_name,
    is_active,
    update_time,
    CASE 
        WHEN stock_code = '000101' THEN '错误映射：应该是恒邦股份（深圳主板）'
        WHEN stock_code = '600101' THEN '正确映射：明星电力（上海主板）'
    END as 正确性说明
FROM stock_info
WHERE stock_code IN ('000101', '600101');

-- 9. 生成修复建议
SELECT '【修复建议总结】' as 检查类型;
SELECT '基于以上检查，建议执行以下修复操作：' as 建议;
SELECT '1. 修正000101的名称为"恒邦股份"，600101保持为"明星电力"' as 步骤1;
SELECT '2. 统一修正所有市场代码：6开头→1（上海），0/3开头→0（深圳）' as 步骤2;
SELECT '3. 停用所有非A股代码（指数、基金、退市股等）' as 步骤3;
SELECT '4. 补全所有缺失或不完整的股票名称' as 步骤4;
SELECT '5. 解决重复名称问题（同名不同码）' as 步骤5;

-- ===================================================
-- 请在pgAdmin中执行此脚本，查看所有潜在的数据质量问题
-- ===================================================
