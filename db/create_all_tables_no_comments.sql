-- ============================================
-- stockdb database schema creation script
-- All tables, indexes without Chinese comments
-- ============================================

-- ============================================
-- 1. Stock Info Table
-- ============================================
CREATE TABLE IF NOT EXISTS stock_info (
    stock_code VARCHAR(10) PRIMARY KEY,
    stock_name VARCHAR(100),
    market_code SMALLINT,
    market_name VARCHAR(50),
    stock_type VARCHAR(20),
    is_active BOOLEAN DEFAULT TRUE,
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_stock_info_market_code ON stock_info(market_code);

-- ============================================
-- 2. Daily Data Table
-- ============================================
CREATE TABLE IF NOT EXISTS stock_daily_data (
    id BIGSERIAL PRIMARY KEY,
    stock_code VARCHAR(10) NOT NULL,
    market_code SMALLINT,
    trade_date DATE NOT NULL,
    trade_datetime TIMESTAMP,
    time_stamp INTEGER,
    open_price NUMERIC(15, 3) NOT NULL,
    high_price NUMERIC(15, 3) NOT NULL,
    low_price NUMERIC(15, 3) NOT NULL,
    close_price NUMERIC(15, 3) NOT NULL,
    adjusted_open_price NUMERIC(15, 3),
    adjusted_high_price NUMERIC(15, 3),
    adjusted_low_price NUMERIC(15, 3),
    adjusted_close_price NUMERIC(15, 3),
    volume NUMERIC(20, 2) NOT NULL,
    amount NUMERIC(20, 2) NOT NULL,
    advance_count SMALLINT,
    decline_count SMALLINT,
    data_source VARCHAR(50) DEFAULT 'Tornado',
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uk_stock_daily_data UNIQUE (stock_code, trade_date)
);

CREATE INDEX IF NOT EXISTS idx_stock_daily_data_stock_code ON stock_daily_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_trade_date ON stock_daily_data(trade_date);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date ON stock_daily_data(stock_code, trade_date DESC);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date_asc ON stock_daily_data(stock_code, trade_date ASC);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_market_code ON stock_daily_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_time_stamp ON stock_daily_data(time_stamp);

-- ============================================
-- 3. Realtime Data Table
-- ============================================
CREATE TABLE IF NOT EXISTS stock_realtime_data (
    id BIGSERIAL PRIMARY KEY,
    stock_code VARCHAR(10) NOT NULL UNIQUE,
    stock_name VARCHAR(100),
    market_code SMALLINT,
    update_time TIMESTAMP NOT NULL,
    time_stamp INTEGER,
    last_close NUMERIC(10, 3) NOT NULL,
    open_price NUMERIC(10, 3) NOT NULL,
    high_price NUMERIC(10, 3) NOT NULL,
    low_price NUMERIC(10, 3) NOT NULL,
    new_price NUMERIC(10, 3) NOT NULL,
    volume NUMERIC(20, 2) NOT NULL,
    amount NUMERIC(20, 2) NOT NULL,
    buy_price_1 NUMERIC(10, 3),
    buy_price_2 NUMERIC(10, 3),
    buy_price_3 NUMERIC(10, 3),
    buy_price_4 NUMERIC(10, 3),
    buy_price_5 NUMERIC(10, 3),
    buy_volume_1 NUMERIC(20, 2),
    buy_volume_2 NUMERIC(20, 2),
    buy_volume_3 NUMERIC(20, 2),
    buy_volume_4 NUMERIC(20, 2),
    buy_volume_5 NUMERIC(20, 2),
    sell_price_1 NUMERIC(10, 3),
    sell_price_2 NUMERIC(10, 3),
    sell_price_3 NUMERIC(10, 3),
    sell_price_4 NUMERIC(10, 3),
    sell_price_5 NUMERIC(10, 3),
    sell_volume_1 NUMERIC(20, 2),
    sell_volume_2 NUMERIC(20, 2),
    sell_volume_3 NUMERIC(20, 2),
    sell_volume_4 NUMERIC(20, 2),
    sell_volume_5 NUMERIC(20, 2),
    data_source VARCHAR(50) DEFAULT 'Tornado-MQ-Realtime',
    update_time_db TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uk_stock_realtime_data UNIQUE (stock_code)
);

CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_stock_code ON stock_realtime_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_market_code ON stock_realtime_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_update_time ON stock_realtime_data(update_time);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_code ON stock_realtime_data(stock_code);

-- ============================================
-- 4. Exrights Data Table
-- ============================================
CREATE TABLE IF NOT EXISTS stock_exrights_data (
    id BIGSERIAL PRIMARY KEY,
    stock_code VARCHAR(10) NOT NULL,
    market_code SMALLINT,
    ex_rights_date DATE NOT NULL,
    ex_rights_datetime TIMESTAMP,
    time_stamp INTEGER,
    give_per_10_shares NUMERIC(10, 4) DEFAULT 0,
    pei_per_10_shares NUMERIC(10, 4) DEFAULT 0,
    pei_price NUMERIC(10, 3) DEFAULT 0,
    profit_per_share NUMERIC(10, 4) DEFAULT 0,
    data_source VARCHAR(50) DEFAULT 'Tornado-MQ',
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uk_stock_exrights_data UNIQUE (stock_code, ex_rights_date)
);

CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_stock_code ON stock_exrights_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_code_date ON stock_exrights_data(stock_code, ex_rights_date);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_ex_rights_date ON stock_exrights_data(ex_rights_date);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_market_code ON stock_exrights_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_time_stamp ON stock_exrights_data(time_stamp);

-- ============================================
-- 5. Adjustment Task Table
-- ============================================
CREATE TABLE IF NOT EXISTS adjustment_task (
    id BIGSERIAL PRIMARY KEY,
    task_type VARCHAR(50) NOT NULL,
    trigger_date DATE NOT NULL,
    status VARCHAR(20) DEFAULT 'pending',
    priority INTEGER DEFAULT 10,
    error_message TEXT,
    retry_count INTEGER DEFAULT 0,
    max_retries INTEGER DEFAULT 3,
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_adjustment_task_status ON adjustment_task(status);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_priority ON adjustment_task(priority);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_type ON adjustment_task(task_type);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_trigger_date ON adjustment_task(trigger_date);

-- ============================================
-- 6. Data Receive Log Table
-- ============================================
CREATE TABLE IF NOT EXISTS data_receive_log (
    id BIGSERIAL PRIMARY KEY,
    receive_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    data_type VARCHAR(50),
    record_count INTEGER,
    queue_name VARCHAR(100),
    source_ip VARCHAR(50),
    status VARCHAR(20) DEFAULT 'success',
    error_message TEXT,
    processing_time_ms INTEGER
);

CREATE INDEX IF NOT EXISTS idx_data_receive_log_receive_time ON data_receive_log(receive_time);
CREATE INDEX IF NOT EXISTS idx_data_receive_log_status ON data_receive_log(status);
