-- 快速检查603699的除权数据
SELECT COUNT(*) as exrights_count FROM stock_exrights_data WHERE stock_code = '603699';

-- 查看具体的除权记录
SELECT stock_code, ex_rights_date, give_per_10, pei_per_10, profit_per_share
FROM stock_exrights_data
WHERE stock_code = '603699'
ORDER BY ex_rights_date DESC;
