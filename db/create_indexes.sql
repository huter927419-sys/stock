-- 性能优化索引脚本
-- 用于优化KD计算和数据查询性能

-- 1. 日线数据表索引（stock_code + trade_date 复合索引）
-- 用于加速按股票代码和日期范围查询
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date
ON stock_daily_data(stock_code, trade_date ASC);

-- 倒序索引，用于获取最近N条数据
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date_desc
ON stock_daily_data(stock_code, trade_date DESC);

-- 2. 实时数据表索引
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_code
ON stock_realtime_data(stock_code);

-- 3. 除权数据表索引
-- 用于按股票代码和日期查询除权记录
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_code_date
ON stock_exrights_data(stock_code, ex_rights_date ASC);

-- 4. 分析索引使用情况（运行后查看）
-- SELECT
--     schemaname,
--     tablename,
--     indexname,
--     idx_scan,
--     idx_tup_read,
--     idx_tup_fetch
-- FROM pg_stat_user_indexes
-- WHERE schemaname = 'public'
-- ORDER BY idx_scan DESC;

-- 5. 查看表大小和索引大小
-- SELECT
--     relname as table_name,
--     pg_size_pretty(pg_total_relation_size(relid)) as total_size,
--     pg_size_pretty(pg_relation_size(relid)) as table_size,
--     pg_size_pretty(pg_indexes_size(relid)) as index_size
-- FROM pg_catalog.pg_statio_user_tables
-- ORDER BY pg_total_relation_size(relid) DESC;
