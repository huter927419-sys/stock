-- ============================================
-- 龙卷风日线数据数据库表结构 (PostgreSQL版本)
-- 创建时间: 2024
-- 数据库: PostgreSQL 9.5+
-- ============================================

-- 1. 股票基本信息表
CREATE TABLE IF NOT EXISTS stock_info (
    stock_code VARCHAR(10) PRIMARY KEY,  -- 股票代码
    stock_name VARCHAR(100),             -- 股票名称
    market_code SMALLINT,                -- 市场代码
    market_name VARCHAR(50),              -- 市场名称
    stock_type VARCHAR(20),              -- 股票类型：股票/指数
    is_active BOOLEAN DEFAULT TRUE,       -- 是否有效
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_stock_info_market_code ON stock_info(market_code);

-- 2. 日线数据主表
CREATE TABLE IF NOT EXISTS stock_daily_data (
    -- 主键
    id BIGSERIAL PRIMARY KEY,
    
    -- 股票标识
    stock_code VARCHAR(10) NOT NULL,      -- 股票代码，如：000001
    market_code SMALLINT,                 -- 市场代码
    
    -- 时间信息
    trade_date DATE NOT NULL,             -- 交易日期
    trade_datetime TIMESTAMP,             -- 交易日期时间（精确到秒）
    time_stamp INTEGER,                   -- UTC时间戳（秒）
    
    -- 价格数据
    open_price NUMERIC(10, 3) NOT NULL,   -- 开盘价
    high_price NUMERIC(10, 3) NOT NULL,   -- 最高价
    low_price NUMERIC(10, 3) NOT NULL,    -- 最低价
    close_price NUMERIC(10, 3) NOT NULL,  -- 收盘价
    
    -- 成交数据
    volume NUMERIC(20, 2) NOT NULL,       -- 成交量
    amount NUMERIC(20, 2) NOT NULL,       -- 成交额
    
    -- 指数专用字段（可选）
    advance_count SMALLINT,               -- 上涨家数（仅指数有效）
    decline_count SMALLINT,               -- 下跌家数（仅指数有效）
    
    -- 元数据
    data_source VARCHAR(50) DEFAULT '龙卷风',  -- 数据来源
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- 唯一约束：同一股票同一日期只能有一条记录
    CONSTRAINT uk_stock_daily_data UNIQUE (stock_code, trade_date)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_stock_code ON stock_daily_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_trade_date ON stock_daily_data(trade_date);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_market_code ON stock_daily_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_time_stamp ON stock_daily_data(time_stamp);

-- 复合索引：用于按股票代码和日期范围查询
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date ON stock_daily_data(stock_code, trade_date DESC);

-- 3. 数据接收日志表（可选，用于监控）
CREATE TABLE IF NOT EXISTS data_receive_log (
    id BIGSERIAL PRIMARY KEY,
    receive_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  -- 接收时间
    data_type VARCHAR(50),                             -- 数据类型：日线数据
    record_count INTEGER,                              -- 记录数
    queue_name VARCHAR(100),                           -- 队列名称
    source_ip VARCHAR(50),                             -- 来源IP
    status VARCHAR(20) DEFAULT 'success',              -- 状态：success/failed
    error_message TEXT,                                -- 错误信息（如果有）
    processing_time_ms INTEGER                         -- 处理耗时（毫秒）
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_data_receive_log_receive_time ON data_receive_log(receive_time);
CREATE INDEX IF NOT EXISTS idx_data_receive_log_status ON data_receive_log(status);

-- 添加注释
COMMENT ON TABLE stock_info IS '股票基本信息表';
COMMENT ON TABLE stock_daily_data IS '股票日线数据表';
COMMENT ON TABLE data_receive_log IS '数据接收日志表';

COMMENT ON COLUMN stock_daily_data.stock_code IS '股票代码，如：000001';
COMMENT ON COLUMN stock_daily_data.market_code IS '市场代码：0=深圳，1=上海';
COMMENT ON COLUMN stock_daily_data.trade_date IS '交易日期';
COMMENT ON COLUMN stock_daily_data.time_stamp IS 'UTC时间戳（秒）';
COMMENT ON COLUMN stock_daily_data.advance_count IS '上涨家数（仅指数有效）';
COMMENT ON COLUMN stock_daily_data.decline_count IS '下跌家数（仅指数有效）';

