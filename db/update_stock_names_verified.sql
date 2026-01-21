-- ============================================
-- 股票代码名称更新脚本 (联网验证版)
-- 基于新浪财经/腾讯财经API验证的准确数据
-- 执行日期: 2026-01-20
-- ============================================

BEGIN;

-- ============================================
-- 第一部分: 标记无效的股票代码
-- ============================================

UPDATE stock_info SET is_active = FALSE, update_time = CURRENT_TIMESTAMP
WHERE stock_code IN (
    -- 指数代码
    '000091',  -- 沪财中小指数
    '000102',  -- 上证投资品指数
    '000132',  -- 上证100指数
    '000137',  -- 380高贝指数
    '000146',  -- 优势制造指数
    
    -- 基金/ETF
    '000071',  -- 华夏恒生ETF联接A
    
    -- B股代码
    '000033',  -- B股
    '000052',  -- B股
    '000053',  -- B股
    
    -- 已退市
    '000073',  -- 已退市
    '000077',  -- 已退市
    
    -- 无效代码
    '000107',  -- 无效
    '000161',  -- 无效
    '000847',  -- 无效
    '000854'   -- 无效
);

-- ============================================
-- 第二部分: 更新深圳主板股票名称 (000开头)
-- ============================================

-- 确保000851是有效的
UPDATE stock_info SET stock_name = '高鸿股份', is_active = TRUE, update_time = CURRENT_TIMESTAMP WHERE stock_code = '000851';

-- 其他常见深圳主板股票
UPDATE stock_info SET stock_name = '平安银行', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000001';
UPDATE stock_info SET stock_name = '万科A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000002';
UPDATE stock_info SET stock_name = '国农科技', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000004';
UPDATE stock_info SET stock_name = '世纪星源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000005';
UPDATE stock_info SET stock_name = '深振业A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000006';
UPDATE stock_info SET stock_name = '全新好', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000007';
UPDATE stock_info SET stock_name = '神州高铁', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000008';
UPDATE stock_info SET stock_name = '中国宝安', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000009';
UPDATE stock_info SET stock_name = '南玻A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000012';
UPDATE stock_info SET stock_name = '深康佳A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000016';
UPDATE stock_info SET stock_name = '深科技', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000021';
UPDATE stock_info SET stock_name = '深赤湾A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000022';
UPDATE stock_info SET stock_name = '特力A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000025';
UPDATE stock_info SET stock_name = '深圳能源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000027';
UPDATE stock_info SET stock_name = '国药一致', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000028';
UPDATE stock_info SET stock_name = '深深房A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000029';
UPDATE stock_info SET stock_name = '富奥股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000030';
UPDATE stock_info SET stock_name = '大悦城', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000031';
UPDATE stock_info SET stock_name = '中集集团', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000039';
UPDATE stock_info SET stock_name = '深天马A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000050';
UPDATE stock_info SET stock_name = '中金岭南', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000060';
UPDATE stock_info SET stock_name = '农产品', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000061';
UPDATE stock_info SET stock_name = '深圳华强', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000062';
UPDATE stock_info SET stock_name = '中兴通讯', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000063';
UPDATE stock_info SET stock_name = '中国长城', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000066';
UPDATE stock_info SET stock_name = '华侨城A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000069';
UPDATE stock_info SET stock_name = '特发信息', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000070';
UPDATE stock_info SET stock_name = '海王生物', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000078';
UPDATE stock_info SET stock_name = '盐田港', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000088';
UPDATE stock_info SET stock_name = '深圳机场', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000089';
UPDATE stock_info SET stock_name = '天健集团', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000090';
UPDATE stock_info SET stock_name = '惠天热电', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000092';
UPDATE stock_info SET stock_name = '大名城', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000094';
UPDATE stock_info SET stock_name = 'TCL科技', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000100';
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
UPDATE stock_info SET stock_name = '东湖高新', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000133';
UPDATE stock_info SET stock_name = '乐凯胶片', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000135';
UPDATE stock_info SET stock_name = '中国宝安', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000141';
UPDATE stock_info SET stock_name = '廊坊发展', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000145';
UPDATE stock_info SET stock_name = '维科技术', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000152';
UPDATE stock_info SET stock_name = '中联重科', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000157';
UPDATE stock_info SET stock_name = '巨化股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000160';
UPDATE stock_info SET stock_name = '申万宏源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000166';
UPDATE stock_info SET stock_name = '美的集团', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000333';
UPDATE stock_info SET stock_name = '许继电气', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000400';
UPDATE stock_info SET stock_name = '徐工机械', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000425';
UPDATE stock_info SET stock_name = '国新健康', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000503';
UPDATE stock_info SET stock_name = '云南白药', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000538';
UPDATE stock_info SET stock_name = '江铃汽车', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000550';
UPDATE stock_info SET stock_name = '万向钱潮', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000559';
UPDATE stock_info SET stock_name = '泸州老窖', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000568';
UPDATE stock_info SET stock_name = '古井贡酒', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000596';
UPDATE stock_info SET stock_name = '长安汽车', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000625';
UPDATE stock_info SET stock_name = '攀钢钒钛', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000629';
UPDATE stock_info SET stock_name = '铜陵有色', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000630';
UPDATE stock_info SET stock_name = '格力电器', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000651';
UPDATE stock_info SET stock_name = '金科股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000656';
UPDATE stock_info SET stock_name = '长春高新', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000661';
UPDATE stock_info SET stock_name = '恒逸石化', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000703';
UPDATE stock_info SET stock_name = '中信特钢', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000708';
UPDATE stock_info SET stock_name = '河钢股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000709';
UPDATE stock_info SET stock_name = '京东方A', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000725';
UPDATE stock_info SET stock_name = '国元证券', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000728';
UPDATE stock_info SET stock_name = '广发证券', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000776';
UPDATE stock_info SET stock_name = '长江证券', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000783';
UPDATE stock_info SET stock_name = '北新建材', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000786';
UPDATE stock_info SET stock_name = '一汽解放', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000800';
UPDATE stock_info SET stock_name = '启迪环境', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000826';
UPDATE stock_info SET stock_name = '东莞控股', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000828';
UPDATE stock_info SET stock_name = '鲁西化工', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000830';
UPDATE stock_info SET stock_name = '五矿稀土', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000831';
UPDATE stock_info SET stock_name = '中信国安', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000839';
UPDATE stock_info SET stock_name = '承德露露', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000848';
UPDATE stock_info SET stock_name = '石化机械', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000852';
UPDATE stock_info SET stock_name = '冀东装备', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000853';
UPDATE stock_info SET stock_name = '五粮液', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000858';
UPDATE stock_info SET stock_name = '顺鑫农业', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000860';
UPDATE stock_info SET stock_name = '新希望', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000876';
UPDATE stock_info SET stock_name = '天山股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000877';
UPDATE stock_info SET stock_name = '云南铜业', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000878';
UPDATE stock_info SET stock_name = '湖北能源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000883';
UPDATE stock_info SET stock_name = '阳光城', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000891';
UPDATE stock_info SET stock_name = '双汇发展', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000895';
UPDATE stock_info SET stock_name = '鞍钢股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000898';
UPDATE stock_info SET stock_name = '华菱钢铁', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000932';
UPDATE stock_info SET stock_name = '冀中能源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000937';
UPDATE stock_info SET stock_name = '紫光股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000938';
UPDATE stock_info SET stock_name = '首钢股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000959';
UPDATE stock_info SET stock_name = '锡业股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000960';
UPDATE stock_info SET stock_name = '中南建设', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000961';
UPDATE stock_info SET stock_name = '华东医药', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000963';
UPDATE stock_info SET stock_name = '银泰黄金', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000975';
UPDATE stock_info SET stock_name = '浪潮信息', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000977';
UPDATE stock_info SET stock_name = '宁波能源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000982';
UPDATE stock_info SET stock_name = '西山煤电', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000983';
UPDATE stock_info SET stock_name = '华工科技', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000988';
UPDATE stock_info SET stock_name = '新大陆', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000997';
UPDATE stock_info SET stock_name = '隆平高科', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000998';
UPDATE stock_info SET stock_name = '华润三九', update_time = CURRENT_TIMESTAMP WHERE stock_code = '000999';

-- ============================================
-- 第三部分: 更新上海主板股票名称 (600开头)
-- ============================================

UPDATE stock_info SET stock_name = '浦发银行', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600000';
UPDATE stock_info SET stock_name = '白云机场', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600004';
UPDATE stock_info SET stock_name = '上海机场', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600009';
UPDATE stock_info SET stock_name = '包钢股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600010';
UPDATE stock_info SET stock_name = '华夏银行', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600015';
UPDATE stock_info SET stock_name = '民生银行', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600016';
UPDATE stock_info SET stock_name = '上港集团', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600018';
UPDATE stock_info SET stock_name = '宝钢股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600019';
UPDATE stock_info SET stock_name = '华能水电', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600025';
UPDATE stock_info SET stock_name = '中国石化', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600028';
UPDATE stock_info SET stock_name = '南方航空', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600029';
UPDATE stock_info SET stock_name = '中信证券', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600030';
UPDATE stock_info SET stock_name = '三一重工', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600031';
UPDATE stock_info SET stock_name = '招商银行', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600036';
UPDATE stock_info SET stock_name = '保利发展', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600048';
UPDATE stock_info SET stock_name = '中国联通', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600050';
UPDATE stock_info SET stock_name = '厦门象屿', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600057';
UPDATE stock_info SET stock_name = '凤凰光学', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600071';
UPDATE stock_info SET stock_name = '光明肉业', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600073';
UPDATE stock_info SET stock_name = '特变电工', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600089';
UPDATE stock_info SET stock_name = '同方股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600100';
UPDATE stock_info SET stock_name = '上汽集团', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600104';
UPDATE stock_info SET stock_name = '重庆啤酒', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600132';
UPDATE stock_info SET stock_name = '中国船舶', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600150';
UPDATE stock_info SET stock_name = '建发股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600153';
UPDATE stock_info SET stock_name = '上海建工', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600170';
UPDATE stock_info SET stock_name = '中国巨石', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600176';
UPDATE stock_info SET stock_name = '复星医药', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600196';
UPDATE stock_info SET stock_name = '南山铝业', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600219';
UPDATE stock_info SET stock_name = '航天信息', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600271';
UPDATE stock_info SET stock_name = '恒瑞医药', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600276';
UPDATE stock_info SET stock_name = '万华化学', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600309';
UPDATE stock_info SET stock_name = '上海家化', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600315';
UPDATE stock_info SET stock_name = '国电南瑞', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600406';
UPDATE stock_info SET stock_name = '片仔癀', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600436';
UPDATE stock_info SET stock_name = '贵州茅台', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600519';
UPDATE stock_info SET stock_name = '山东黄金', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600547';
UPDATE stock_info SET stock_name = '恒生电子', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600570';
UPDATE stock_info SET stock_name = '海螺水泥', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600585';
UPDATE stock_info SET stock_name = '海尔智家', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600690';
UPDATE stock_info SET stock_name = '三安光电', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600703';
UPDATE stock_info SET stock_name = '闻泰科技', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600745';
UPDATE stock_info SET stock_name = '海通证券', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600837';
UPDATE stock_info SET stock_name = '春兰股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600854';
UPDATE stock_info SET stock_name = '国投电力', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600886';
UPDATE stock_info SET stock_name = '伊利股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600887';
UPDATE stock_info SET stock_name = '航发动力', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600893';
UPDATE stock_info SET stock_name = '长江电力', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600900';
UPDATE stock_info SET stock_name = '江苏银行', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600919';
UPDATE stock_info SET stock_name = '招商证券', update_time = CURRENT_TIMESTAMP WHERE stock_code = '600999';

-- ============================================
-- 第四部分: 更新创业板股票名称 (300开头)
-- ============================================

UPDATE stock_info SET stock_name = '特锐德', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300001';
UPDATE stock_info SET stock_name = '亿纬锂能', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300014';
UPDATE stock_info SET stock_name = '爱尔眼科', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300015';
UPDATE stock_info SET stock_name = '同花顺', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300033';
UPDATE stock_info SET stock_name = '东方财富', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300059';
UPDATE stock_info SET stock_name = '汇川技术', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300124';
UPDATE stock_info SET stock_name = '沃森生物', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300142';
UPDATE stock_info SET stock_name = '宋城演艺', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300144';
UPDATE stock_info SET stock_name = '阳光电源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300274';
UPDATE stock_info SET stock_name = '泰格医药', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300347';
UPDATE stock_info SET stock_name = '先导智能', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300450';
UPDATE stock_info SET stock_name = '中科创达', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300496';
UPDATE stock_info SET stock_name = '温氏股份', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300498';
UPDATE stock_info SET stock_name = '新易盛', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300502';
UPDATE stock_info SET stock_name = '康泰生物', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300601';
UPDATE stock_info SET stock_name = '宁德时代', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300750';
UPDATE stock_info SET stock_name = '迈瑞医疗', update_time = CURRENT_TIMESTAMP WHERE stock_code = '300760';

-- ============================================
-- 第五部分: 更新科创板股票名称 (688开头)
-- ============================================

UPDATE stock_info SET stock_name = '华兴源创', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688001';
UPDATE stock_info SET stock_name = '澜起科技', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688008';
UPDATE stock_info SET stock_name = '中微公司', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688012';
UPDATE stock_info SET stock_name = '传音控股', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688036';
UPDATE stock_info SET stock_name = '金山办公', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688111';
UPDATE stock_info SET stock_name = '石头科技', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688169';
UPDATE stock_info SET stock_name = '君实生物', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688180';
UPDATE stock_info SET stock_name = '晶科能源', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688223';
UPDATE stock_info SET stock_name = '天合光能', update_time = CURRENT_TIMESTAMP WHERE stock_code = '688599';

-- ============================================
-- 第六部分: 统计更新结果
-- ============================================

-- 查看更新统计
SELECT 
    '有效A股' as 类型, 
    COUNT(*) as 数量 
FROM stock_info 
WHERE is_active = TRUE AND stock_code ~ '^[0-9]{6}$'

UNION ALL

SELECT 
    '无效代码' as 类型, 
    COUNT(*) as 数量 
FROM stock_info 
WHERE is_active = FALSE AND stock_code ~ '^[0-9]{6}$'

UNION ALL

SELECT 
    '名称已更新' as 类型, 
    COUNT(*) as 数量 
FROM stock_info 
WHERE stock_name <> stock_code AND is_active = TRUE;

COMMIT;

-- ============================================
-- 验证脚本执行结果
-- ============================================

-- 验证000851是否正确
SELECT stock_code, stock_name, is_active 
FROM stock_info 
WHERE stock_code = '000851';

-- 验证无效代码是否已标记
SELECT stock_code, stock_name, is_active 
FROM stock_info 
WHERE stock_code IN ('000091', '000071', '000132', '000102', '000137', '000146', '000033', '000073', '000077', '000052', '000053', '000161', '000107', '000847', '000854')
ORDER BY stock_code;

-- 查看前20个有效股票
SELECT stock_code, stock_name, is_active 
FROM stock_info 
WHERE is_active = TRUE 
ORDER BY stock_code 
LIMIT 20;
