-- ============================================
-- 为日线数据表 stock_daily_data 添加换手率字段
-- 用于过滤条件：昨天换手率>3%（表格统一条件）
-- ============================================

ALTER TABLE stock_daily_data ADD COLUMN IF NOT EXISTS turnover_rate NUMERIC(8,4);

COMMENT ON COLUMN stock_daily_data.turnover_rate IS '换手率（%），如 3.5 表示 3.5%；需从日线 JSON 解析或由 成交量/流通股本 计算';

-- 说明：
-- 1. 程序启动时 DatabaseInitializer 也会执行 ADD COLUMN IF NOT EXISTS
-- 2. 写入：若日线 MQ 有 pct/turnover_rate 等字段，需在 ParseDailyRecord、SaveDailyData 中解析并写入
-- 3. 若数据源无换手率，可后续用 换手率=成交量/流通股本*100 计算后 UPDATE
