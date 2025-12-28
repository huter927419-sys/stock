-- ============================================
-- 除权数据表结构 (PostgreSQL版本)
-- 创建时间: 2024
-- 数据库: PostgreSQL 9.5+
-- ============================================

-- 除权数据表（存储股票除权除息信息）
CREATE TABLE IF NOT EXISTS stock_exrights_data (
    -- 主键
    id BIGSERIAL PRIMARY KEY,
    
    -- 股票标识
    stock_code VARCHAR(10) NOT NULL,      -- 股票代码
    market_code SMALLINT,                  -- 市场代码
    
    -- 时间信息
    ex_rights_date DATE NOT NULL,         -- 除权日期
    ex_rights_datetime TIMESTAMP,         -- 除权日期时间（精确到秒）
    time_stamp INTEGER,                    -- UTC时间戳（秒）
    
    -- 除权数据
    give_per_10_shares NUMERIC(10, 4) DEFAULT 0,  -- 每10股送股数
    pei_per_10_shares NUMERIC(10, 4) DEFAULT 0,    -- 每10股配股数
    pei_price NUMERIC(10, 3) DEFAULT 0,            -- 配股价（当配股数>0时有效）
    profit_per_share NUMERIC(10, 4) DEFAULT 0,     -- 每股红利
    
    -- 元数据
    data_source VARCHAR(50) DEFAULT '龙卷风-MQ',  -- 数据来源
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- 唯一约束：同一股票同一除权日期只能有一条记录
    CONSTRAINT uk_stock_exrights_data UNIQUE (stock_code, ex_rights_date)
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_stock_code ON stock_exrights_data(stock_code);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_ex_rights_date ON stock_exrights_data(ex_rights_date);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_market_code ON stock_exrights_data(market_code);
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_time_stamp ON stock_exrights_data(time_stamp);

-- 复合索引：用于按股票代码和日期范围查询
CREATE INDEX IF NOT EXISTS idx_stock_exrights_data_code_date ON stock_exrights_data(stock_code, ex_rights_date DESC);

-- 添加注释
COMMENT ON TABLE stock_exrights_data IS '股票除权除息数据表';
COMMENT ON COLUMN stock_exrights_data.stock_code IS '股票代码，如：000001';
COMMENT ON COLUMN stock_exrights_data.market_code IS '市场代码：0=深圳，1=上海';
COMMENT ON COLUMN stock_exrights_data.ex_rights_date IS '除权日期';
COMMENT ON COLUMN stock_exrights_data.give_per_10_shares IS '每10股送股数';
COMMENT ON COLUMN stock_exrights_data.pei_per_10_shares IS '每10股配股数';
COMMENT ON COLUMN stock_exrights_data.pei_price IS '配股价（当配股数>0时有效）';
COMMENT ON COLUMN stock_exrights_data.profit_per_share IS '每股红利（现金分红）';

