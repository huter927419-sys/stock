-- 修复ST类股票名称
-- 从实时数据表中获取完整的ST股票名称

BEGIN;

-- 查看当前被截断的ST股票
SELECT '=== 当前被截断的ST股票 ===' as info;
SELECT stock_code, stock_name
FROM stock_info
WHERE stock_name ~ '^[S\*]*ST$'
   OR (stock_name ~ '^[S\*]*ST' AND LENGTH(stock_name) <= 4)
ORDER BY stock_code
LIMIT 20;

-- 从实时数据表同步完整的ST股票名称
UPDATE stock_info si
SET stock_name = rt.stock_name,
    update_time = CURRENT_TIMESTAMP
FROM (
    SELECT DISTINCT ON (stock_code) stock_code, stock_name
    FROM stock_realtime_data
    WHERE stock_name ~ '^[S\*]*ST'
      AND LENGTH(stock_name) > 4
    ORDER BY stock_code, update_time DESC
) rt
WHERE si.stock_code = rt.stock_code
  AND (si.stock_name ~ '^[S\*]*ST$' OR LENGTH(si.stock_name) <= 4);

-- 显示修复后的结果
SELECT '=== 修复后的ST股票名称 ===' as info;
SELECT stock_code, stock_name
FROM stock_info
WHERE stock_name ~ '^[S\*]*ST'
ORDER BY stock_code
LIMIT 30;

COMMIT;
