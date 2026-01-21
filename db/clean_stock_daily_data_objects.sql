-- 清理 stock_daily_data 表的约束和索引
-- 用于在导入 dump 文件前避免冲突

-- 删除索引
DROP INDEX IF EXISTS public.idx_stock_daily_data_code_date;
DROP INDEX IF EXISTS public.idx_stock_daily_data_code_date_asc;
DROP INDEX IF EXISTS public.idx_stock_daily_data_code_date_desc;
DROP INDEX IF EXISTS public.idx_stock_daily_data_stock_code;
DROP INDEX IF EXISTS public.idx_stock_daily_data_trade_date;
DROP INDEX IF EXISTS public.idx_stock_daily_data_market_code;
DROP INDEX IF EXISTS public.idx_stock_daily_data_time_stamp;
DROP INDEX IF EXISTS public.idx_stock_daily_data_adjusted;

-- 删除约束
ALTER TABLE IF EXISTS public.stock_daily_data DROP CONSTRAINT IF EXISTS uk_stock_daily_data;
ALTER TABLE IF EXISTS public.stock_daily_data DROP CONSTRAINT IF EXISTS stock_daily_data_pkey;

-- 删除序列（如果需要）
DROP SEQUENCE IF EXISTS public.stock_daily_data_id_seq CASCADE;

-- 显示清理结果
SELECT 
    '索引和约束已清理' AS status,
    COUNT(*) AS remaining_indexes
FROM pg_indexes 
WHERE tablename = 'stock_daily_data' 
  AND schemaname = 'public';
