-- 更新A股和创业板股票的名称
-- 只更新在stock_info表中存在的股票，且确保is_active = TRUE

-- 更新000022 深赤湾A
UPDATE stock_info 
SET stock_name = '深赤湾A', 
    market_code = 0,
    market_name = '深圳',
    is_active = TRUE,
    update_time = CURRENT_TIMESTAMP
WHERE stock_code = '000022'
  AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code OR stock_name <> '深赤湾A');

-- 更新002043 兔宝宝
UPDATE stock_info 
SET stock_name = '兔宝宝', 
    market_code = 0,
    market_name = '深圳',
    is_active = TRUE,
    update_time = CURRENT_TIMESTAMP
WHERE stock_code = '002043'
  AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code OR stock_name <> '兔宝宝');

-- 更新000132 新湖中宝（如果存在）
UPDATE stock_info 
SET stock_name = '新湖中宝', 
    market_code = 0,
    market_name = '深圳',
    is_active = TRUE,
    update_time = CURRENT_TIMESTAMP
WHERE stock_code = '000132'
  AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code OR stock_name <> '新湖中宝');

-- 注意：000071, 000033, 000005, 000053, 000052, 000110, 000073 不是A股股票（是基金或指数）
-- 如果这些代码在stock_info表中，应该设置为 is_active = FALSE，这样缓存就不会加载它们

-- 将非A股/非创业板的代码设置为未激活
UPDATE stock_info 
SET is_active = FALSE,
    update_time = CURRENT_TIMESTAMP
WHERE stock_code IN ('000071', '000033', '000005', '000053', '000052', '000110', '000073')
  AND is_active = TRUE;

-- 更新000827（如果是A股，需要确认名称）
-- 注意：000827 需要确认是否是A股，如果是则添加名称，如果不是则设置为 is_active = FALSE
-- 暂时假设000827是A股，如果确认不是，请手动设置为 is_active = FALSE
UPDATE stock_info 
SET stock_name = COALESCE(stock_name, '东莞控股'),
    market_code = 0,
    market_name = '深圳',
    is_active = TRUE,
    update_time = CURRENT_TIMESTAMP
WHERE stock_code = '000827'
  AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);

-- 显示更新结果
SELECT 
    stock_code,
    stock_name,
    market_code,
    market_name,
    is_active,
    CASE 
        WHEN stock_code ~ '^(600|601|603|605|688)' THEN '沪市主板/科创板'
        WHEN stock_code ~ '^(000|001|002|003|004)' THEN '深市主板/中小板'
        WHEN stock_code ~ '^(300|301)' THEN '创业板'
        ELSE '未知'
    END AS stock_type
FROM stock_info
WHERE stock_code IN ('000022','000071','000033','000132','000005','000053','000052','000110','000073','000827','002043')
ORDER BY stock_code;
