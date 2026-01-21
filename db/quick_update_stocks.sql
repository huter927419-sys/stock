-- ============================================
-- 快速股票代码更新脚本 (精简版)
-- ============================================

-- 第一步: 标记无效代码
UPDATE stock_info SET is_active = FALSE WHERE stock_code IN (
    '000091', '000102', '000132', '000137', '000146',
    '000071', '000033', '000052', '000053',
    '000073', '000077', '000107', '000161', '000847', '000854'
);

-- 第二步: 确保000851有效
UPDATE stock_info SET stock_name = '高鸿股份', is_active = TRUE WHERE stock_code = '000851';

-- 第三步: 更新常见股票名称
UPDATE stock_info SET stock_name = '平安银行' WHERE stock_code = '000001';
UPDATE stock_info SET stock_name = '万科A' WHERE stock_code = '000002';
UPDATE stock_info SET stock_name = '鲁西化工' WHERE stock_code = '000830';
UPDATE stock_info SET stock_name = '东莞控股' WHERE stock_code = '000828';
UPDATE stock_info SET stock_name = '贵州茅台' WHERE stock_code = '600519';
UPDATE stock_info SET stock_name = '同花顺' WHERE stock_code = '300033';

-- 查看结果
SELECT '更新完成' as 状态;
SELECT stock_code, stock_name, is_active FROM stock_info WHERE stock_code = '000851';
SELECT COUNT(*) as 无效代码数 FROM stock_info WHERE is_active = FALSE;
SELECT COUNT(*) as 有效代码数 FROM stock_info WHERE is_active = TRUE;
