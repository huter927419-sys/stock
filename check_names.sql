-- 检查指定股票代码在各表中的情况
WITH codes AS (
    SELECT unnest(ARRAY['000891','000022','000101','000071','000033','000854','000141','000119','000112','000132','000117','000057','000982','000133','000135','000106','000145','000105','000094','000092','000853','000160','000073','000122','000116','000130','000152','000125','000113']) AS stock_code
),
info_data AS (
    SELECT stock_code, stock_name AS info_name FROM stock_info
),
realtime_data AS (
    SELECT DISTINCT ON (stock_code) stock_code, stock_name AS rt_name
    FROM stock_realtime_data
    ORDER BY stock_code, update_time DESC
),
daily_exists AS (
    SELECT DISTINCT stock_code, TRUE as has_daily FROM stock_daily_data
)
SELECT
    c.stock_code,
    COALESCE(i.info_name, '-') AS stock_info_name,
    COALESCE(r.rt_name, '-') AS realtime_name,
    CASE WHEN d.has_daily THEN '是' ELSE '否' END AS has_daily
FROM codes c
LEFT JOIN info_data i ON c.stock_code = i.stock_code
LEFT JOIN realtime_data r ON c.stock_code = r.stock_code
LEFT JOIN daily_exists d ON c.stock_code = d.stock_code
ORDER BY c.stock_code;
