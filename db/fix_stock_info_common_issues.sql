-- ========================================
-- 股票代码表（stock_info）常见问题修复
-- ========================================

BEGIN;

-- 1. 修复市场代码错误
-- 上海(1): 600/601/603/605(主板), 688(科创板), 900(B股)
-- 深圳(0): 000/001(主板), 002/003/004(中小板), 300/301(创业板), 200(B股)
-- 北京(2): 43/83/87/88开头
UPDATE stock_info
SET
    market_code = CASE
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN 1
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN 0
        WHEN stock_code ~ '^(43|83|87|88)' THEN 2
        ELSE 0
    END,
    market_name = CASE
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海'
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳'
        WHEN stock_code ~ '^(43|83|87|88)' THEN '北京'
        ELSE '深圳'
    END,
    update_time = CURRENT_TIMESTAMP
WHERE (
    (stock_code ~ '^(600|601|603|605|688)' AND market_code != 1) OR
    (stock_code ~ '^(000|001|002|003|004|300|301)' AND market_code != 0) OR
    (stock_code ~ '^(43|83|87|88)' AND market_code != 2)
);

-- 显示修复结果
SELECT '修复市场代码' as 操作, ROW_COUNT() as 影响行数;

-- 2. 停用指数代码（000001, 000300等）
UPDATE stock_info
SET is_active = FALSE,
    update_time = CURRENT_TIMESTAMP
WHERE stock_code IN (
    '000001', -- 上证指数
    '000002', -- A股指数
    '000003', -- 深证综指
    '000004', -- 深证100R
    '000005', -- （可能是指数）
    '000006', -- 深证综指
    '000008', -- 综合指数
    '000009', -- 上证380
    '000010', -- 上证180
    '000011', -- 基金指数
    '000012', -- 国债指数
    '000013', -- 新综指
    '000016', -- 上证50
    '000017', -- 新综指
    '000300', -- 沪深300
    '000688', -- 科创50
    '000905', -- 中证500
    '000906', -- 中证800
    '399001', -- 深证成指
    '399002', -- 深成指R
    '399003', -- 成份B指
    '399004', -- 深证100R
    '399005', -- 中小板指
    '399006', -- 创业板指
    '399007', -- 深证300
    '399008', -- 中小300
    '399100', -- 新指数
    '399101', -- 中小板综
    '399106', -- 深证综指
    '399107', -- 深证A指
    '399108', -- 深证B指
    '399333', -- 中小板R
    '399606'  -- 创业板R
)
AND is_active = TRUE;

-- 显示修复结果
SELECT '停用指数代码' as 操作, ROW_COUNT() as 影响行数;

-- 3. 停用B股（200、900开头）
UPDATE stock_info
SET is_active = FALSE,
    update_time = CURRENT_TIMESTAMP
WHERE (stock_code ~ '^200' OR stock_code ~ '^900')
AND is_active = TRUE;

-- 显示修复结果
SELECT '停用B股代码' as 操作, ROW_COUNT() as 影响行数;

-- 4. 停用已知的已退市股票
UPDATE stock_info
SET is_active = FALSE,
    update_time = CURRENT_TIMESTAMP
WHERE stock_code IN (
    '000018', -- 神城A退
    '000033', -- 新都退
    '000046', -- *ST泛海（已退市）
    '000139', -- 富国国企债（基金）
    '000669', -- 金鸿退
    '000760', -- *ST斯太
    '000816', -- 慧业退
    '000914', -- （非股票）
    '000981', -- *ST银亿
    '600656'  -- *ST退市博元
)
AND is_active = TRUE;

-- 显示修复结果
SELECT '停用已退市股票' as 操作, ROW_COUNT() as 影响行数;

-- 5. 修复名称为空的记录（使用代码作为名称）
UPDATE stock_info
SET stock_name = stock_code,
    update_time = CURRENT_TIMESTAMP
WHERE (stock_name IS NULL OR stock_name = '')
AND is_active = TRUE;

-- 显示修复结果
SELECT '修复空名称' as 操作, ROW_COUNT() as 影响行数;

-- 6. 统计修复后的情况
SELECT '=== 修复后统计 ===' as 标题;

SELECT 
    '总记录数' as 项目,
    COUNT(*) as 数量
FROM stock_info
UNION ALL
SELECT 
    '激活状态（is_active=TRUE）',
    COUNT(*)
FROM stock_info
WHERE is_active = TRUE
UNION ALL
SELECT 
    '停用状态（is_active=FALSE）',
    COUNT(*)
FROM stock_info
WHERE is_active = FALSE
UNION ALL
SELECT 
    '有名称的股票',
    COUNT(*)
FROM stock_info
WHERE stock_name IS NOT NULL AND stock_name != '' AND stock_name != stock_code
AND is_active = TRUE
UNION ALL
SELECT 
    '无名称的股票（名称=代码）',
    COUNT(*)
FROM stock_info
WHERE (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code)
AND is_active = TRUE;

-- 提交事务
COMMIT;

-- 显示最终建议
SELECT '=== 修复完成 ===' as 标题;
SELECT '建议: 重启应用程序以重新加载 StockInfoCache' as 建议;
