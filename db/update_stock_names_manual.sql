-- 手动更新股票名称的SQL脚本
-- 用于更新stock_info表中缺少或错误的股票名称

-- 深市A股 000xxx 股票名称更新
-- 数据来源: 东方财富、同花顺等金融数据平台

-- 批量更新股票名称
UPDATE stock_info SET stock_name = '华讯方舟' WHERE stock_code = '000687' AND (stock_name IS NULL OR stock_name = '' OR stock_name = '000687');
UPDATE stock_info SET stock_name = 'ST宏业' WHERE stock_code = '000689' AND (stock_name IS NULL OR stock_name = '' OR stock_name = '000689');
UPDATE stock_info SET stock_name = '*ST华泽' WHERE stock_code = '000693' AND (stock_name IS NULL OR stock_name = '' OR stock_name = '000693');
UPDATE stock_info SET stock_name = 'S*ST佳纸' WHERE stock_code = '000699' AND (stock_name IS NULL OR stock_name = '' OR stock_name = '000699');

-- 查看更新结果
SELECT stock_code, stock_name, market_name, update_time
FROM stock_info
WHERE stock_code IN ('000687', '000689', '000693', '000699')
ORDER BY stock_code;
