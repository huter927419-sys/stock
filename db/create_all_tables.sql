-- ============================================
-- stockdb 数据库完整建库建表脚本
-- 整合所有表结构、索引和注释
-- ============================================
-- 执行方式：
-- 1. 先创建数据库：
--    psql -h localhost -p 8532 -U postgres -c "CREATE DATABASE stockdb;"
-- 2. 然后执行此脚本：
--    psql -h localhost -p 8532 -U postgres -d stockdb -f create_all_tables.sql
-- ============================================

-- ============================================
-- 1. 股票基本信息表
-- ============================================
CREATE TABLE IF NOT EXISTS stock_info (
    stock_code VARCHAR(10) PRIMARY KEY,       -- 股票代码
    stock_name VARCHAR(100),                  -- 股票名称
    market_code SMALLINT,                     -- 市场代码
    market_name VARCHAR(50),                  -- 市场名称
    stock_type VARCHAR(20),                   -- 股票类型：股票/指数
    is_active BOOLEAN DEFAULT TRUE,           -- 是否有效
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_stock_info_market_code ON stock_info(market_code);

COMMENT ON TABLE stock_info IS '股票基本信息表';

-- ============================================
-- 2. 日线数据主表
-- ============================================
CREATE TABLE IF NOT EXISTS stock_daily_data (
    -- 主键
    id BIGSERIAL PRIMARY KEY,

    -- 股票标识
    stock_code VARCHAR(10) NOT NULL,          -- 股票代码，如：000001
    market_code SMALLINT,                     -- 市场代码

    -- 时间信息
    trade_date DATE NOT NULL,                 -- 交易日期
    trade_datetime TIMESTAMP,                 -- 交易日期时间（精确到秒）
    time_stamp INTEGER,                       -- UTC时间戳（秒）

    -- 价格数据
    open_price NUMERIC(10, 3) NOT NULL,       -- 开盘价
    high_price NUMERIC(10, 3) NOT NULL,       -- 最高价
    low_price NUMERIC(10, 3) NOT NULL,        -- 最低价
    close_price NUMERIC(10, 3) NOT NULL,      -- 收盘价

    -- 复权价格（预计算，用于提高KD计算性能）
    adjusted_open_price NUMERIC(10, 3),       -- 前复权开盘价
    adjusted_high_price NUMERIC(10, 3),       -- 前复权最高价
    adjusted_low_price NUMERIC(10, 3),        -- 前复权最低价
    adjusted_close_price NUMERIC(10, 3),      -- 前复权收盘价

    -- 成交数据
    volume NUMERIC(20, 2) NOT NULL,           -- 成交量
    amount NUMERIC(20, 2) NOT NULL,           -- 成交额

    -- 指数专用字段（可选）
    advance_count SMALLINT,                   -- 上涨家数（仅指数有效）
    decline_count SMALLINT,                   -- 下跌家数（仅指数有效）

    -- 元数据
    data_source VARCHAR(50) DEFAULT '龙卷风', -- 数据来源
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- 唯一约束：同一股票同一日期只能有一条记录
    CONSTRAINT uk_stock_daily_data UNIQUE (stock_code, trade_date)
);

-- 日线数据表索引
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_stock_code ON stock_daily_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_trade_date ON stock_daily_data(trade_date);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_market_code ON stock_daily_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_time_stamp ON stock_daily_data(time_stamp);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date ON stock_daily_data(stock_code, trade_date ASC);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_code_date_desc ON stock_daily_data(stock_code, trade_date DESC);
CREATE INDEX IF NOT EXISTS idx_stock_daily_data_adjusted ON stock_daily_data(stock_code, trade_date)
    WHERE adjusted_close_price IS NOT NULL;

COMMENT ON TABLE stock_daily_data IS '股票日线数据表';
COMMENT ON COLUMN stock_daily_data.stock_code IS '股票代码，如：000001';
COMMENT ON COLUMN stock_daily_data.market_code IS '市场代码：0=深圳，1=上海';
COMMENT ON COLUMN stock_daily_data.trade_date IS '交易日期';
COMMENT ON COLUMN stock_daily_data.time_stamp IS 'UTC时间戳（秒）';
COMMENT ON COLUMN stock_daily_data.advance_count IS '上涨家数（仅指数有效）';
COMMENT ON COLUMN stock_daily_data.decline_count IS '下跌家数（仅指数有效）';
COMMENT ON COLUMN stock_daily_data.adjusted_open_price IS '前复权开盘价（入库时计算）';
COMMENT ON COLUMN stock_daily_data.adjusted_high_price IS '前复权最高价（入库时计算）';
COMMENT ON COLUMN stock_daily_data.adjusted_low_price IS '前复权最低价（入库时计算）';
COMMENT ON COLUMN stock_daily_data.adjusted_close_price IS '前复权收盘价（入库时计算）';

-- ============================================
-- 3. 实时数据表
-- ============================================
CREATE TABLE IF NOT EXISTS stock_realtime_data (
    -- 主键
    id BIGSERIAL PRIMARY KEY,

    -- 股票标识
    stock_code VARCHAR(10) NOT NULL UNIQUE,   -- 股票代码，唯一约束
    stock_name VARCHAR(100),                  -- 股票名称
    market_code SMALLINT,                     -- 市场代码

    -- 时间信息
    update_time TIMESTAMP NOT NULL,           -- 更新时间
    time_stamp INTEGER,                       -- UTC时间戳（秒）

    -- 价格数据
    last_close NUMERIC(10, 3) NOT NULL,       -- 昨收
    open_price NUMERIC(10, 3) NOT NULL,       -- 开盘
    high_price NUMERIC(10, 3) NOT NULL,       -- 最高
    low_price NUMERIC(10, 3) NOT NULL,        -- 最低
    new_price NUMERIC(10, 3) NOT NULL,        -- 最新价

    -- 成交数据
    volume NUMERIC(20, 2) NOT NULL,           -- 成交量
    amount NUMERIC(20, 2) NOT NULL,           -- 成交额

    -- 买盘数据（5档）
    buy_price_1 NUMERIC(10, 3),               -- 买1价
    buy_price_2 NUMERIC(10, 3),               -- 买2价
    buy_price_3 NUMERIC(10, 3),               -- 买3价
    buy_price_4 NUMERIC(10, 3),               -- 买4价
    buy_price_5 NUMERIC(10, 3),               -- 买5价
    buy_volume_1 NUMERIC(20, 2),              -- 买1量
    buy_volume_2 NUMERIC(20, 2),              -- 买2量
    buy_volume_3 NUMERIC(20, 2),              -- 买3量
    buy_volume_4 NUMERIC(20, 2),              -- 买4量
    buy_volume_5 NUMERIC(20, 2),              -- 买5量

    -- 卖盘数据（5档）
    sell_price_1 NUMERIC(10, 3),              -- 卖1价
    sell_price_2 NUMERIC(10, 3),              -- 卖2价
    sell_price_3 NUMERIC(10, 3),              -- 卖3价
    sell_price_4 NUMERIC(10, 3),              -- 卖4价
    sell_price_5 NUMERIC(10, 3),              -- 卖5价
    sell_volume_1 NUMERIC(20, 2),             -- 卖1量
    sell_volume_2 NUMERIC(20, 2),             -- 卖2量
    sell_volume_3 NUMERIC(20, 2),             -- 卖3量
    sell_volume_4 NUMERIC(20, 2),             -- 卖4量
    sell_volume_5 NUMERIC(20, 2),             -- 卖5量

    -- 元数据
    data_source VARCHAR(50) DEFAULT '龙卷风-MQ-实时',  -- 数据来源
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time_db TIMESTAMP DEFAULT CURRENT_TIMESTAMP -- 数据库更新时间
);

-- 实时数据表索引
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_stock_code ON stock_realtime_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_market_code ON stock_realtime_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_realtime_data_update_time ON stock_realtime_data(update_time DESC);

COMMENT ON TABLE stock_realtime_data IS '股票实时数据表（存储最新行情）';
COMMENT ON COLUMN stock_realtime_data.stock_code IS '股票代码，唯一约束，同一股票只保留最新一条记录';
COMMENT ON COLUMN stock_realtime_data.update_time IS '数据更新时间（来自数据源）';
COMMENT ON COLUMN stock_realtime_data.update_time_db IS '数据库记录更新时间';

-- ============================================
-- 4. 除权数据表
-- ============================================
CREATE TABLE IF NOT EXISTS stock_exrights_data (
    -- 主键
    id BIGSERIAL PRIMARY KEY,

    -- 股票标识
    stock_code VARCHAR(10) NOT NULL,          -- 股票代码
    market_code SMALLINT,                     -- 市场代码

    -- 时间信息
    ex_rights_date DATE NOT NULL,             -- 除权日期
    ex_rights_datetime TIMESTAMP,             -- 除权日期时间（精确到秒）
    time_stamp INTEGER,                       -- UTC时间戳（秒）

    -- 除权数据
    give_per_10_shares NUMERIC(10, 4) DEFAULT 0,  -- 每10股送股数
    pei_per_10_shares NUMERIC(10, 4) DEFAULT 0,   -- 每10股配股数
    pei_price NUMERIC(10, 3) DEFAULT 0,           -- 配股价（当配股数>0时有效）
    profit_per_share NUMERIC(10, 4) DEFAULT 0,    -- 每股红利

    -- 元数据
    data_source VARCHAR(50) DEFAULT '龙卷风-MQ',  -- 数据来源
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- 唯一约束：同一股票同一除权日期只能有一条记录
    CONSTRAINT uk_stock_exrights_data UNIQUE (stock_code, ex_rights_date)
);

-- 除权数据表索引
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_stock_code ON stock_exrights_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_ex_rights_date ON stock_exrights_data(ex_rights_date);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_market_code ON stock_exrights_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_time_stamp ON stock_exrights_data(time_stamp);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_code_date ON stock_exrights_data(stock_code, ex_rights_date ASC);

COMMENT ON TABLE stock_exrights_data IS '股票除权除息数据表';
COMMENT ON COLUMN stock_exrights_data.stock_code IS '股票代码，如：000001';
COMMENT ON COLUMN stock_exrights_data.market_code IS '市场代码：0=深圳，1=上海';
COMMENT ON COLUMN stock_exrights_data.ex_rights_date IS '除权日期';
COMMENT ON COLUMN stock_exrights_data.give_per_10_shares IS '每10股送股数';
COMMENT ON COLUMN stock_exrights_data.pei_per_10_shares IS '每10股配股数';
COMMENT ON COLUMN stock_exrights_data.pei_price IS '配股价（当配股数>0时有效）';
COMMENT ON COLUMN stock_exrights_data.profit_per_share IS '每股红利（现金分红）';

-- ============================================
-- 5. 复权计算任务表
-- ============================================
CREATE TABLE IF NOT EXISTS adjustment_task (
    id BIGSERIAL PRIMARY KEY,
    stock_code VARCHAR(10) NOT NULL,
    task_type VARCHAR(20) NOT NULL,
    -- 任务类型：
    -- 'new_daily' - 新日线数据需要计算复权价格
    -- 'new_exrights' - 新除权数据，需要计算该日期之后的复权价格
    -- 'update_exrights' - 除权数据更新，需要重新计算所有历史数据
    -- 'recalculate' - 手动触发的重算任务

    trigger_date DATE NOT NULL,               -- 触发日期
    status VARCHAR(20) DEFAULT 'pending',
    -- 状态：pending(待处理), processing(处理中), completed(已完成), failed(失败)

    priority INTEGER DEFAULT 5,
    -- 优先级：1-10，数字越小优先级越高
    -- 1-3: 高优先级（除权数据更新）
    -- 4-6: 中优先级（新除权数据）
    -- 7-10: 低优先级（新日线数据）

    error_message TEXT,                       -- 错误信息（如果失败）
    retry_count INTEGER DEFAULT 0,            -- 重试次数
    max_retries INTEGER DEFAULT 3,            -- 最大重试次数

    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    start_time TIMESTAMP,
    complete_time TIMESTAMP
);

-- 复权任务表索引
CREATE INDEX IF NOT EXISTS idx_adjustment_task_stock_code ON adjustment_task(stock_code);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_status ON adjustment_task(status, priority);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_type ON adjustment_task(task_type);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_pending ON adjustment_task(status, priority, create_time)
    WHERE status = 'pending';

COMMENT ON TABLE adjustment_task IS '复权计算任务表';
COMMENT ON COLUMN adjustment_task.task_type IS '任务类型：new_daily(新日线), new_exrights(新除权), update_exrights(更新除权), recalculate(重算)';
COMMENT ON COLUMN adjustment_task.status IS '任务状态：pending(待处理), processing(处理中), completed(已完成), failed(失败)';
COMMENT ON COLUMN adjustment_task.priority IS '优先级：1-10，数字越小优先级越高';

-- ============================================
-- 6. 数据接收日志表
-- ============================================
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

-- 日志表索引
CREATE INDEX IF NOT EXISTS idx_data_receive_log_receive_time ON data_receive_log(receive_time);
CREATE INDEX IF NOT EXISTS idx_data_receive_log_status ON data_receive_log(status);

COMMENT ON TABLE data_receive_log IS '数据接收日志表';

-- ============================================
-- 完成提示
-- ============================================
SELECT '所有表创建完成！' AS result;

-- 查看所有表
SELECT table_name, pg_size_pretty(pg_total_relation_size(quote_ident(table_name))) AS size
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;
