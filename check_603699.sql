-- 检查603699的除权数据
SELECT stock_code, ex_rights_date, give_per_10, pei_per_10, pei_price, profit_per_share
FROM stock_exrights_data
WHERE stock_code = '603699'
ORDER BY ex_rights_date DESC
LIMIT 10;

-- 检查603699在除权日前后的K线数据（如果有除权）
SELECT trade_date, open, high, low, close,
       LAG(close, 1) OVER (ORDER BY trade_date) as prev_close,
       close - LAG(close, 1) OVER (ORDER BY trade_date) as price_change,
       (close - LAG(close, 1) OVER (ORDER BY trade_date)) / LAG(close, 1) OVER (ORDER BY trade_date) * 100 as change_pct
FROM stock_daily_data
WHERE stock_code = '603699'
  AND trade_date >= '2020-01-01'
ORDER BY trade_date;
