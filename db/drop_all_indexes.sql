-- Drop all indexes from stock_daily_data table
-- This script will drop all indexes to speed up data import

-- Drop indexes for stock_daily_data
DROP INDEX IF EXISTS public.idx_stock_daily_data_stock_code;
DROP INDEX IF EXISTS public.idx_stock_daily_data_trade_date;
DROP INDEX IF EXISTS public.idx_stock_daily_data_code_date;
DROP INDEX IF EXISTS public.idx_stock_daily_data_code_date_asc;
DROP INDEX IF EXISTS public.idx_stock_daily_data_code_date_desc;
DROP INDEX IF EXISTS public.idx_stock_daily_data_market_code;
DROP INDEX IF EXISTS public.idx_stock_daily_data_time_stamp;
DROP INDEX IF EXISTS public.idx_stock_daily_data_adjusted;

-- Drop indexes for other tables (if they exist)
DROP INDEX IF EXISTS public.idx_stock_info_market_code;
DROP INDEX IF EXISTS public.idx_stock_exrights_data_stock_code;
DROP INDEX IF EXISTS public.idx_stock_exrights_data_code_date;
DROP INDEX IF EXISTS public.idx_stock_exrights_data_ex_rights_date;
DROP INDEX IF EXISTS public.idx_stock_exrights_data_market_code;
DROP INDEX IF EXISTS public.idx_stock_exrights_data_time_stamp;
DROP INDEX IF EXISTS public.idx_stock_realtime_data_stock_code;
DROP INDEX IF EXISTS public.idx_stock_realtime_data_market_code;
DROP INDEX IF EXISTS public.idx_stock_realtime_data_update_time;
DROP INDEX IF EXISTS public.idx_stock_realtime_data_code;

-- Show remaining indexes count
SELECT 
    'Indexes dropped' AS status,
    COUNT(*) AS remaining_indexes
FROM pg_indexes 
WHERE schemaname = 'public' 
  AND (tablename LIKE 'stock_%' OR tablename = 'data_receive_log');
