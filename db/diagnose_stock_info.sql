-- ========================================
-- 股票代码表（stock_info）全面诊断
-- ========================================

-- 1. 基本统计
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
UNION ALL
SELECT 
    '无名称的股票（名称=代码）',
    COUNT(*)
FROM stock_info
WHERE stock_name IS NULL OR stock_name = '' OR stock_name = stock_code;

-- 2. 按市场分类统计
SELECT 
    '=== 按市场分类 ===' as 分类,
    '' as 数量;

SELECT 
    CASE 
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海市场'
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳市场'
        WHEN stock_code ~ '^(43|83|87|88)' THEN '北京市场'
        ELSE '未知市场'
    END as 市场,
    COUNT(*) as 股票数量,
    SUM(CASE WHEN stock_name != stock_code THEN 1 ELSE 0 END) as 有名称数量
FROM stock_info
WHERE is_active = TRUE
GROUP BY 
    CASE 
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海市场'
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳市场'
        WHEN stock_code ~ '^(43|83|87|88)' THEN '北京市场'
        ELSE '未知市场'
    END
ORDER BY 股票数量 DESC;

-- 3. 检查可疑的股票代码（可能是指数、基金、B股等）
SELECT 
    '=== 可疑代码检查 ===' as 检查项,
    '' as 代码,
    '' as 名称;

-- 指数代码（000001-000999中的特殊代码）
SELECT 
    '可能是指数' as 类型,
    stock_code as 代码,
    stock_name as 名称
FROM stock_info
WHERE stock_code IN ('000001', '000300', '000905', '000016', '399001', '399006')
  AND is_active = TRUE
ORDER BY stock_code;

-- B股代码（200、900开头）
SELECT 
    'B股（应过滤）' as 类型,
    stock_code as 代码,
    stock_name as 名称
FROM stock_info
WHERE (stock_code ~ '^200' OR stock_code ~ '^900')
  AND is_active = TRUE
ORDER BY stock_code
LIMIT 10;

-- 4. 检查市场代码错误
SELECT 
    '=== 市场代码错误检查 ===' as 检查项,
    '' as 代码,
    '' as 应该是,
    '' as 实际是;

SELECT 
    '市场代码错误' as 类型,
    stock_code as 代码,
    CASE 
        WHEN stock_code ~ '^(600|601|603|605|688|900)' THEN '上海(1)'
        WHEN stock_code ~ '^(000|001|002|003|004|300|301|200)' THEN '深圳(0)'
        WHEN stock_code ~ '^(43|83|87|88)' THEN '北京(2)'
        ELSE '未知'
    END as 应该是,
    CASE market_code
        WHEN 0 THEN '深圳(0)'
        WHEN 1 THEN '上海(1)'
        WHEN 2 THEN '北京(2)'
        ELSE '未知'
    END as 实际是
FROM stock_info
WHERE is_active = TRUE
  AND (
    (stock_code ~ '^(600|601|603|605|688)' AND market_code != 1) OR
    (stock_code ~ '^(000|001|002|003|004|300|301)' AND market_code != 0) OR
    (stock_code ~ '^(43|83|87|88)' AND market_code != 2)
  )
LIMIT 20;

-- 5. 检查名称异常
SELECT 
    '=== 名称异常检查 ===' as 检查项,
    '' as 代码,
    '' as 名称,
    '' as 问题;

-- 名称为空或等于代码
SELECT 
    '名称缺失' as 类型,
    stock_code as 代码,
    COALESCE(stock_name, '(NULL)') as 名称,
    '名称为空或等于代码' as 问题
FROM stock_info
WHERE is_active = TRUE
  AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code)
ORDER BY stock_code
LIMIT 20;

-- 名称过长（>8个字符，可能是公司全称）
SELECT 
    '名称过长' as 类型,
    stock_code as 代码,
    stock_name as 名称,
    LENGTH(stock_name) || '字符' as 问题
FROM stock_info
WHERE is_active = TRUE
  AND stock_name IS NOT NULL
  AND LENGTH(stock_name) > 8
ORDER BY LENGTH(stock_name) DESC
LIMIT 10;

-- ST股票检查（名称应该包含ST）
SELECT 
    'ST股票' as 类型,
    stock_code as 代码,
    stock_name as 名称,
    '检查ST前缀' as 问题
FROM stock_info
WHERE is_active = TRUE
  AND stock_name ~ '^[S\*]*ST'
ORDER BY stock_code
LIMIT 10;

-- 6. 检查重复代码
SELECT 
    '=== 重复代码检查 ===' as 检查项,
    '' as 代码,
    '' as 出现次数;

SELECT 
    stock_code as 代码,
    COUNT(*) as 出现次数
FROM stock_info
GROUP BY stock_code
HAVING COUNT(*) > 1;

-- 7. 抽样检查（随机10只股票）
SELECT 
    '=== 抽样检查（随机10只） ===' as 检查项,
    '' as 代码,
    '' as 名称,
    '' as 市场,
    '' as 状态;

SELECT 
    stock_code as 代码,
    stock_name as 名称,
    CASE market_code
        WHEN 0 THEN '深圳'
        WHEN 1 THEN '上海'
        WHEN 2 THEN '北京'
        ELSE '未知'
    END as 市场,
    CASE is_active
        WHEN TRUE THEN '激活'
        ELSE '停用'
    END as 状态
FROM stock_info
WHERE is_active = TRUE
ORDER BY RANDOM()
LIMIT 10;

-- 8. 检查是否有日线数据但 stock_info 中缺失的股票
SELECT 
    '=== 日线数据但未在stock_info中 ===' as 检查项,
    '' as 代码;

SELECT 
    DISTINCT d.stock_code as 代码,
    '在日线数据中，但不在stock_info' as 问题
FROM stock_daily_data d
LEFT JOIN stock_info s ON d.stock_code = s.stock_code
WHERE s.stock_code IS NULL
LIMIT 10;

-- 9. 总结建议
SELECT 
    '=== 诊断总结 ===' as 项目,
    '' as 建议;

SELECT 
    '1. 数据完整性' as 项目,
    CASE 
        WHEN (SELECT COUNT(*) FROM stock_info WHERE is_active = TRUE) > 3000 
        THEN '✓ 记录数量正常（>3000）'
        ELSE '✗ 记录数量偏少（<3000）'
    END as 建议
UNION ALL
SELECT 
    '2. 名称完整性',
    CASE 
        WHEN (SELECT COUNT(*) FROM stock_info WHERE is_active = TRUE AND stock_name != stock_code) * 100.0 / 
             NULLIF((SELECT COUNT(*) FROM stock_info WHERE is_active = TRUE), 0) > 90
        THEN '✓ 名称覆盖率>90%'
        ELSE '✗ 名称覆盖率<90%，建议补充'
    END
UNION ALL
SELECT 
    '3. 市场代码准确性',
    CASE 
        WHEN (SELECT COUNT(*) FROM stock_info WHERE is_active = TRUE AND (
            (stock_code ~ '^(600|601|603|605|688)' AND market_code != 1) OR
            (stock_code ~ '^(000|001|002|003|004|300|301)' AND market_code != 0)
        )) = 0
        THEN '✓ 市场代码准确'
        ELSE '✗ 有市场代码错误，建议修复'
    END
UNION ALL
SELECT 
    '4. 重复代码',
    CASE 
        WHEN (SELECT COUNT(*) FROM (SELECT stock_code FROM stock_info GROUP BY stock_code HAVING COUNT(*) > 1) t) = 0
        THEN '✓ 无重复代码'
        ELSE '✗ 有重复代码，建议清理'
    END;
