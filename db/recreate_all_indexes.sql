-- Recreate all indexes after data import
-- This script will recreate all indexes that were dropped before import

-- Recreate indexes for stock_daily_data
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_stock_code ON public.stock_daily_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_trade_date ON public.stock_daily_data(trade_date);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date ON public.stock_daily_data(stock_code, trade_date DESC);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date_asc ON public.stock_daily_data(stock_code, trade_date ASC);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_market_code ON public.stock_daily_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_time_stamp ON public.stock_daily_data(time_stamp);

-- Recreate indexes for stock_info
CREATE INDEX IF NOT EXISTS idx_stock_info_market_code ON public.stock_info(market_code);

-- Recreate indexes for stock_exrights_data
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_stock_code ON public.stock_exrights_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_code_date ON public.stock_exrights_data(stock_code, ex_rights_date);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_ex_rights_date ON public.stock_exrights_data(ex_rights_date);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_market_code ON public.stock_exrights_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_time_stamp ON public.stock_exrights_data(time_stamp);

-- Recreate indexes for stock_realtime_data
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_stock_code ON public.stock_realtime_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_market_code ON public.stock_realtime_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_update_time ON public.stock_realtime_data(update_time);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_code ON public.stock_realtime_data(stock_code);

-- Show created indexes count
SELECT 
    'Indexes recreated' AS status,
    COUNT(*) AS total_indexes
FROM pg_indexes 
WHERE schemaname = 'public' 
  AND (tablename LIKE 'stock_%' OR tablename = 'data_receive_log');
