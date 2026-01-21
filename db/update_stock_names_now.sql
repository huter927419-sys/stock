-- 更新股票名称 (直接覆盖)
-- 执行: psql -h localhost -p 8532 -U postgres -d stockdb -f update_stock_names_now.sql

BEGIN;

UPDATE stock_info SET stock_name = '招商港口', update_time = CURRENT_TIMESTAMP WHERE stock_code = '001872';
UPDATE stock_info SET stock_name = '同花顺', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300033';
UPDATE stock_info SET stock_name = '厦门象屿', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600057';
UPDATE stock_info SET stock_name = '凤凰光学', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600071';
UPDATE stock_info SET stock_name = '光明肉业', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600073';
UPDATE stock_info SET stock_name = '惠天热电', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000092';
UPDATE stock_info SET stock_name = '大名城', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000094';
UPDATE stock_info SET stock_name = '明星电力', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000101';
UPDATE stock_info SET stock_name = '永鼎股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000105';
UPDATE stock_info SET stock_name = '重庆路桥', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000106';
UPDATE stock_info SET stock_name = '浙江东日', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000112';
UPDATE stock_info SET stock_name = '浙江东方', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000113';
UPDATE stock_info SET stock_name = '三峡水利', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000116';
UPDATE stock_info SET stock_name = '西宁特钢', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000117';
UPDATE stock_info SET stock_name = '瑞茂通', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000119';
UPDATE stock_info SET stock_name = '兰花科创', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000122';
UPDATE stock_info SET stock_name = '铁龙物流', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000125';
UPDATE stock_info SET stock_name = '波导股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000130';
UPDATE stock_info SET stock_name = '重庆啤酒', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600132';
UPDATE stock_info SET stock_name = '东湖高新', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000133';
UPDATE stock_info SET stock_name = '乐凯胶片', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000135';
UPDATE stock_info SET stock_name = '中国宝安', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000141';
UPDATE stock_info SET stock_name = '廊坊发展', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000145';
UPDATE stock_info SET stock_name = '维科技术', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000152';
UPDATE stock_info SET stock_name = '巨化股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000160';
UPDATE stock_info SET stock_name = '冀东装备', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000853';
UPDATE stock_info SET stock_name = '春兰股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600854';
UPDATE stock_info SET stock_name = '阳光城', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000891';
UPDATE stock_info SET stock_name = '宁波能源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000982';

-- 验证结果
SELECT stock_code, stock_name FROM stock_info
WHERE stock_code IN ('001872','300033','600057','600071','600073','600132','600854','000101','000112','000119','000141','000891','000982')
ORDER BY stock_code;

COMMIT;
