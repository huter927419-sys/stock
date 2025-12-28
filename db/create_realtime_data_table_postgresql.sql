-- ============================================
-- 实时数据表结构 (PostgreSQL版本)
-- 创建时间: 2024
-- 数据库: PostgreSQL 9.5+
-- ============================================

-- 实时数据表（存储最新的实时行情数据）
CREATE TABLE IF NOT EXISTS stock_realtime_data (
    -- 主键
    id BIGSERIAL PRIMARY KEY,
    
    -- 股票标识
    stock_code VARCHAR(10) NOT NULL UNIQUE,  -- 股票代码，唯一约束
    stock_name VARCHAR(100),                 -- 股票名称
    market_code SMALLINT,                    -- 市场代码
    
    -- 时间信息
    update_time TIMESTAMP NOT NULL,          -- 更新时间
    time_stamp INTEGER,                      -- UTC时间戳（秒）
    
    -- 价格数据
    last_close NUMERIC(10, 3) NOT NULL,      -- 昨收
    open_price NUMERIC(10, 3) NOT NULL,      -- 开盘
    high_price NUMERIC(10, 3) NOT NULL,       -- 最高
    low_price NUMERIC(10, 3) NOT NULL,       -- 最低
    new_price NUMERIC(10, 3) NOT NULL,       -- 最新价
    
    -- 成交数据
    volume NUMERIC(20, 2) NOT NULL,          -- 成交量
    amount NUMERIC(20, 2) NOT NULL,         -- 成交额
    
    -- 买盘数据（5档）
    buy_price_1 NUMERIC(10, 3),              -- 买1价
    buy_price_2 NUMERIC(10, 3),              -- 买2价
    buy_price_3 NUMERIC(10, 3),              -- 买3价
    buy_price_4 NUMERIC(10, 3),              -- 买4价
    buy_price_5 NUMERIC(10, 3),              -- 买5价
    buy_volume_1 NUMERIC(20, 2),             -- 买1量
    buy_volume_2 NUMERIC(20, 2),             -- 买2量
    buy_volume_3 NUMERIC(20, 2),             -- 买3量
    buy_volume_4 NUMERIC(20, 2),             -- 买4量
    buy_volume_5 NUMERIC(20, 2),             -- 买5量
    
    -- 卖盘数据（5档）
    sell_price_1 NUMERIC(10, 3),             -- 卖1价
    sell_price_2 NUMERIC(10, 3),             -- 卖2价
    sell_price_3 NUMERIC(10, 3),             -- 卖3价
    sell_price_4 NUMERIC(10, 3),             -- 卖4价
    sell_price_5 NUMERIC(10, 3),             -- 卖5价
    sell_volume_1 NUMERIC(20, 2),            -- 卖1量
    sell_volume_2 NUMERIC(20, 2),            -- 卖2量
    sell_volume_3 NUMERIC(20, 2),            -- 卖3量
    sell_volume_4 NUMERIC(20, 2),            -- 卖4量
    sell_volume_5 NUMERIC(20, 2),            -- 卖5量
    
    -- 元数据
    data_source VARCHAR(50) DEFAULT '龙卷风-MQ-实时',  -- 数据来源
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time_db TIMESTAMP DEFAULT CURRENT_TIMESTAMP  -- 数据库更新时间
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_stock_code ON stock_realtime_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_market_code ON stock_realtime_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_update_time ON stock_realtime_data(update_time DESC);

-- 添加注释
COMMENT ON TABLE stock_realtime_data IS '股票实时数据表（存储最新行情）';
COMMENT ON COLUMN stock_realtime_data.stock_code IS '股票代码，唯一约束，同一股票只保留最新一条记录';
COMMENT ON COLUMN stock_realtime_data.update_time IS '数据更新时间（来自数据源）';
COMMENT ON COLUMN stock_realtime_data.update_time_db IS '数据库记录更新时间';

