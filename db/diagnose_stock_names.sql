-- 股票名称诊断SQL
-- 在PostgreSQL中执行此文件查看股票名称情况

-- 1. 总体统计
SELECT '=== 数据库总体统计 ===' as info;
SELECT
    COUNT(*) as 总记录数,
    SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL AND stock_name <> '' THEN 1 ELSE 0 END) as 有名称,
    SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL OR stock_name = '' THEN 1 ELSE 0 END) as 缺少名称
FROM stock_info;

-- 2. 按市场统计
SELECT '=== 按市场统计 ===' as info;
SELECT
    COALESCE(market_name, 'NULL') as 市场,
    COUNT(*) as 总数,
    SUM(CASE WHEN stock_name <> stock_code AND stock_name IS NOT NULL THEN 1 ELSE 0 END) as 有名称
FROM stock_info
GROUP BY market_name
ORDER BY market_name;

-- 3. 缺少名称的股票（前50个）
SELECT '=== 缺少名称的股票(前50个) ===' as info;
SELECT stock_code as 代码, stock_name as 名称, market_name as 市场
FROM stock_info
WHERE stock_name = stock_code OR stock_name IS NULL OR stock_name = ''
ORDER BY stock_code
LIMIT 50;

-- 4. 检查截图中标红的股票
SELECT '=== 检查特定股票 ===' as info;
SELECT stock_code as 代码, stock_name as 名称, market_name as 市场, is_active as 激活
FROM stock_info
WHERE stock_code IN ('000849', '000891', '000170', '000687', '000982', '000139', '000135', '000120', '000847', '000865', '000137', '000145', '000105', '000855', '000125', '000300')
ORDER BY stock_code;

-- 5. 检查stock_info表结构
SELECT '=== stock_info表结构 ===' as info;
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'stock_info'
ORDER BY ordinal_position;

-- 6. 检查是否有重复的股票代码
SELECT '=== 重复的股票代码 ===' as info;
SELECT stock_code, COUNT(*) as cnt
FROM stock_info
GROUP BY stock_code
HAVING COUNT(*) > 1
LIMIT 10;

-- 7. 查看最近更新的记录
SELECT '=== 最近更新的10条记录 ===' as info;
SELECT stock_code, stock_name, market_name, update_time
FROM stock_info
ORDER BY update_time DESC NULLS LAST
LIMIT 10;
