-- ===================================================
-- 批量停用所有非A股代码
-- 包括：指数、债券、基金、B股等
-- ===================================================

BEGIN;

-- 备份当前要停用的数据
CREATE TEMP TABLE temp_to_disable AS
SELECT stock_code, stock_name, market_code
FROM stock_info
WHERE is_active = TRUE
AND (
    -- 1. 000000-000199范围（极高可能性是指数/债券）
    (stock_code ~ '^000[0-1][0-9]{2}$' AND stock_code::int < 200)
    
    -- 2. 名称包含关键词
    OR stock_name LIKE '%指数%'
    OR stock_name LIKE '%债券%'
    OR stock_name LIKE '%债%'
    OR stock_name LIKE '%基金%'
    OR stock_name LIKE '%ETF%'
    OR stock_name LIKE '%LOF%'
    
    -- 3. B股代码
    OR stock_code ~ '^200'
    OR stock_code ~ '^900'
    
    -- 4. 已知退市股票
    OR stock_code IN ('000018', '000816')
);

-- 显示即将停用的代码
SELECT '即将停用以下代码：' as 提示;
SELECT stock_code, stock_name, 
    CASE 
        WHEN stock_code::int < 200 AND stock_code ~ '^000' THEN '指数/债券（代码范围）'
        WHEN stock_name LIKE '%指数%' THEN '指数（名称）'
        WHEN stock_name LIKE '%债%' THEN '债券（名称）'
        WHEN stock_name LIKE '%基金%' THEN '基金（名称）'
        WHEN stock_name LIKE '%ETF%' THEN 'ETF（名称）'
        WHEN stock_code ~ '^200' OR stock_code ~ '^900' THEN 'B股'
        ELSE '其他'
    END as 类型
FROM temp_to_disable
ORDER BY stock_code;

SELECT '总计: ' || COUNT(*) || ' 个代码将被停用' as 统计
FROM temp_to_disable;

-- 执行停用操作
UPDATE stock_info
SET is_active = FALSE,
    update_time = CURRENT_TIMESTAMP
WHERE stock_code IN (SELECT stock_code FROM temp_to_disable);

COMMIT;

-- 验证结果
SELECT '停用完成！' as 状态;
SELECT COUNT(*) as 已停用数量
FROM stock_info
WHERE is_active = FALSE;

SELECT '剩余活跃A股代码：' as 提示;
SELECT COUNT(*) as 活跃数量
FROM stock_info
WHERE is_active = TRUE;

-- 显示一些剩余的活跃代码样本（验证是否正确）
SELECT '活跃代码样本（前20个）：' as 提示;
SELECT stock_code, stock_name, market_code
FROM stock_info
WHERE is_active = TRUE
ORDER BY stock_code
LIMIT 20;
