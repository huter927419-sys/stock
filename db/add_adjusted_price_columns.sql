-- ============================================
-- 为日线数据表添加复权价格字段
-- 用于预计算复权价格，提高KD计算性能
-- ============================================

-- 添加复权价格字段
ALTER TABLE stock_daily_data 
ADD COLUMN IF NOT EXISTS adjusted_open_price NUMERIC(10, 3),
ADD COLUMN IF NOT EXISTS adjusted_high_price NUMERIC(10, 3),
ADD COLUMN IF NOT EXISTS adjusted_low_price NUMERIC(10, 3),
ADD COLUMN IF NOT EXISTS adjusted_close_price NUMERIC(10, 3);

-- 添加注释
COMMENT ON COLUMN stock_daily_data.adjusted_open_price IS '前复权开盘价（入库时计算）';
COMMENT ON COLUMN stock_daily_data.adjusted_high_price IS '前复权最高价（入库时计算）';
COMMENT ON COLUMN stock_daily_data.adjusted_low_price IS '前复权最低价（入库时计算）';
COMMENT ON COLUMN stock_daily_data.adjusted_close_price IS '前复权收盘价（入库时计算）';

-- 创建索引（用于快速查询有复权价格的数据）
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_adjusted ON stock_daily_data(stock_code, trade_date) 
WHERE adjusted_close_price IS NOT NULL;

-- 说明：
-- 1. 这些字段在入库时由程序自动计算并填充
-- 2. 如果adjusted_close_price为NULL，说明该条数据还未计算复权价格
-- 3. 当有新的除权数据时，需要重新计算该股票的历史复权价格

