-- 补充缺失的深圳股票名称
-- 问题股票代码: 000891,000022,000101,000071,000033,000854,000141,000119,000112,000132,000117,000057,000982,000133,000135,000106,000145,000105,000094,000092,000853,000160,000073,000122,000116,000130,000152,000125,000113

BEGIN;

-- 深圳主板股票名称更新
UPDATE stock_info SET stock_name = '法尔胜', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000890' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '海能达', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000891' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '深赤湾A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000022' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '中泰化学', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000101' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '海南海药', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000071' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '新都退', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000033' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '云南锗业', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000854' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '兴发集团', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000141' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '深物业A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000119' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '天津普林', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000112' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '新湖中宝', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000132' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '万林物流', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000117' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '深科技', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000057' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '*ST中绒', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000982' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '新华都', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000133' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '*ST宏图', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000135' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '中远海能', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000106' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '中交地产', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000145' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '德展健康', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000105' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '深南电A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000094' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '中鼎股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000092' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '东方财富', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000853' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '金岭矿业', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000160' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '深南电A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000073' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '中国长城', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000122' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '中金岭南', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000116' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '新集能源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000130' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '深信泰丰', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000152' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '南玻A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000125' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);
UPDATE stock_info SET stock_name = '亚星客车', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000113' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code);

-- 验证更新结果
SELECT stock_code, stock_name FROM stock_info
WHERE stock_code IN ('000891','000022','000101','000071','000033','000854','000141','000119','000112','000132','000117','000057','000982','000133','000135','000106','000145','000105','000094','000092','000853','000160','000073','000122','000116','000130','000152','000125','000113')
ORDER BY stock_code;

COMMIT;
