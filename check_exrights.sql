-- 检查除权数据表
SELECT '=== 除权数据表统计 ===' as info;
SELECT 
    COUNT(*) as 总记录数,
    COUNT(DISTINCT stock_code) as 股票数量,
    MIN(ex_rights_date) as 最早除权日期,
    MAX(ex_rights_date) as 最新除权日期
FROM stock_exrights_data;

SELECT '=== 前10条除权数据示例 ===' as info;
SELECT 
    stock_code as 股票代码,
    ex_rights_date as 除权日期,
    give_per_10_shares as 送股,
    pei_per_10_shares as 配股,
    pei_price as 配股价,
    profit_per_share as 每股红利
FROM stock_exrights_data
ORDER BY ex_rights_date DESC
LIMIT 10;

SELECT '=== 最近除权的股票 ===' as info;
SELECT 
    stock_code as 股票代码,
    ex_rights_date as 除权日期,
    CASE 
        WHEN give_per_10_shares > 0 THEN CONCAT('10送', give_per_10_shares::text)
        ELSE ''
    END ||
    CASE 
        WHEN pei_per_10_shares > 0 THEN CONCAT(' 10配', pei_per_10_shares::text)
        ELSE ''
    END ||
    CASE 
        WHEN profit_per_share > 0 THEN CONCAT(' 派', profit_per_share::text)
        ELSE ''
    END as 除权方案
FROM stock_exrights_data
WHERE ex_rights_date >= CURRENT_DATE - INTERVAL '1 year'
ORDER BY ex_rights_date DESC
LIMIT 15;
