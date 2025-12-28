-- ============================================
-- 复权计算任务表
-- 用于管理复权价格的计算任务
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
    
    trigger_date DATE NOT NULL,  -- 触发日期
    status VARCHAR(20) DEFAULT 'pending', 
    -- 状态：pending(待处理), processing(处理中), completed(已完成), failed(失败)
    
    priority INTEGER DEFAULT 5, 
    -- 优先级：1-10，数字越小优先级越高
    -- 1-3: 高优先级（除权数据更新）
    -- 4-6: 中优先级（新除权数据）
    -- 7-10: 低优先级（新日线数据）
    
    error_message TEXT,  -- 错误信息（如果失败）
    retry_count INTEGER DEFAULT 0,  -- 重试次数
    max_retries INTEGER DEFAULT 3,  -- 最大重试次数
    
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    start_time TIMESTAMP,
    complete_time TIMESTAMP
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_adjustment_task_stock_code ON adjustment_task(stock_code);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_status ON adjustment_task(status, priority);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_type ON adjustment_task(task_type);
CREATE INDEX IF NOT EXISTS idx_adjustment_task_pending ON adjustment_task(status, priority, create_time) 
WHERE status = 'pending';

-- 添加注释
COMMENT ON TABLE adjustment_task IS '复权计算任务表';
COMMENT ON COLUMN adjustment_task.task_type IS '任务类型：new_daily(新日线), new_exrights(新除权), update_exrights(更新除权), recalculate(重算)';
COMMENT ON COLUMN adjustment_task.status IS '任务状态：pending(待处理), processing(处理中), completed(已完成), failed(失败)';
COMMENT ON COLUMN adjustment_task.priority IS '优先级：1-10，数字越小优先级越高';

