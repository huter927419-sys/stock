-- ============================================
-- 修复 stock_daily_data 表价格字段精度不足的问题
-- 将 NUMERIC(10, 3) 改为 NUMERIC(20, 3) 以支持更大的价格值
-- ============================================

-- 修改 stock_daily_data 表的价格字段精度
-- 从 NUMERIC(10, 3) 改为 NUMERIC(20, 3)

ALTER TABLE public.stock_daily_data 
    ALTER COLUMN open_price TYPE NUMERIC(20, 3),
    ALTER COLUMN high_price TYPE NUMERIC(20, 3),
    ALTER COLUMN low_price TYPE NUMERIC(20, 3),
    ALTER COLUMN close_price TYPE NUMERIC(20, 3),
    ALTER COLUMN adjusted_open_price TYPE NUMERIC(20, 3),
    ALTER COLUMN adjusted_high_price TYPE NUMERIC(20, 3),
    ALTER COLUMN adjusted_low_price TYPE NUMERIC(20, 3),
    ALTER COLUMN adjusted_close_price TYPE NUMERIC(20, 3);

-- 如果需要，也可以修改其他相关表的价格字段
-- stock_realtime_data 表
ALTER TABLE public.stock_realtime_data 
    ALTER COLUMN last_close TYPE NUMERIC(20, 3),
    ALTER COLUMN open_price TYPE NUMERIC(20, 3),
    ALTER COLUMN high_price TYPE NUMERIC(20, 3),
    ALTER COLUMN low_price TYPE NUMERIC(20, 3),
    ALTER COLUMN new_price TYPE NUMERIC(20, 3);

-- 更新注释
COMMENT ON COLUMN public.stock_daily_data.open_price IS '开盘价（精度已扩展至20位）';
COMMENT ON COLUMN public.stock_daily_data.high_price IS '最高价（精度已扩展至20位）';
COMMENT ON COLUMN public.stock_daily_data.low_price IS '最低价（精度已扩展至20位）';
COMMENT ON COLUMN public.stock_daily_data.close_price IS '收盘价（精度已扩展至20位）';
COMMENT ON COLUMN public.stock_daily_data.adjusted_open_price IS '前复权开盘价（精度已扩展至20位）';
COMMENT ON COLUMN public.stock_daily_data.adjusted_high_price IS '前复权最高价（精度已扩展至20位）';
COMMENT ON COLUMN public.stock_daily_data.adjusted_low_price IS '前复权最低价（精度已扩展至20位）';
COMMENT ON COLUMN public.stock_daily_data.adjusted_close_price IS '前复权收盘价（精度已扩展至20位）';
