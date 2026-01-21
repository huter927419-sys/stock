-- 检查002569的除权数据
SELECT stock_code, ex_rights_date, give_per_10, pei_per_10, pei_price, profit_per_share
FROM stock_exrights_data
WHERE stock_code = '002569'
ORDER BY ex_rights_date DESC
LIMIT 10;

-- 检查002569在缺口日期附近的K线数据
SELECT trade_date, open, high, low, close
FROM stock_daily_data
WHERE stock_code = '002569'
  AND trade_date BETWEEN '2019-07-01' AND '2019-08-15'
ORDER BY trade_date;
