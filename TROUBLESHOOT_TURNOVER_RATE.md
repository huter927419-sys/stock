# 换手率数据未更新问题排查指南

## 问题现象
数据库中 `stock_daily_data.turnover_rate` 字段全部为 NULL。

## 可能的原因

### 1. 发送端（mqqc）没有发送换手率数据（最可能）

**检查方法**：
- 查看发送端（mqqc）日志，确认是否计算并发送了换手率
- 检查发送端的 `CirculatingSharesCache` 是否有流通股本数据
- 查看发送端是否调用了 `AskStockFin()` 请求财务数据

**解决方案**：
- 确保发送端订阅了财务数据
- 检查发送端的流通股本缓存是否已加载
- 查看发送端代码中 `DailyDataProcessor_MQ.ConvertToDailyDataRecord` 是否正确计算换手率

### 2. JSON 字段名不匹配

**检查方法**：
- 启用调试日志：设置环境变量 `DEBUG_TURNOVER=1` 后运行程序
- 查看控制台输出，检查 JSON 中是否包含 `turnover_rate`、`pct` 或 `turnover` 字段

**解决方案**：
- 如果 JSON 中的字段名不同，修改 `StockDataParser.ParseDailyRecord` 中的字段名解析逻辑

### 3. 数据库字段不存在

**检查方法**：
```sql
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'stock_daily_data' 
  AND column_name = 'turnover_rate';
```

**解决方案**：
- 如果字段不存在，程序启动时会自动添加（通过 `DatabaseSchemaUpdater`）
- 或手动执行：`ALTER TABLE stock_daily_data ADD COLUMN IF NOT EXISTS turnover_rate NUMERIC(8,4);`

### 4. 数据更新逻辑问题

**检查方法**：
- 查看 `PostgresStockDataRepository.SaveDailyData` 的 SQL 语句
- 确认 `ON CONFLICT ... DO UPDATE SET` 中包含了 `turnover_rate = EXCLUDED.turnover_rate`

**当前状态**：
- ✅ SQL 已包含 `turnover_rate = EXCLUDED.turnover_rate`
- ✅ 参数绑定正确：`@turnover_rate` 绑定到 `record.TurnoverRate`

## 调试步骤

### 步骤1：启用调试日志

设置环境变量后运行程序：
```bash
set DEBUG_TURNOVER=1
MQReceiver.exe
```

或在 Visual Studio 中：
- 项目属性 → 调试 → 环境变量 → 添加 `DEBUG_TURNOVER=1`

### 步骤2：查看日志输出

程序会输出以下调试信息：
- `[换手率调试]` - 解析换手率时的详细信息
- `[保存换手率]` - 保存到数据库时的详细信息
- `[日线数据]` - 统计有/无换手率的记录数量

### 步骤3：检查数据库

```sql
-- 检查最近的数据中换手率情况
SELECT 
    stock_code,
    trade_date,
    volume,
    amount,
    turnover_rate,
    update_time
FROM stock_daily_data
WHERE trade_date >= CURRENT_DATE - INTERVAL '7 days'
ORDER BY update_time DESC
LIMIT 20;

-- 统计换手率数据分布
SELECT 
    COUNT(*) as total,
    COUNT(turnover_rate) as with_turnover_rate,
    COUNT(CASE WHEN turnover_rate IS NOT NULL AND turnover_rate > 0 THEN 1 END) as with_positive_turnover_rate
FROM stock_daily_data
WHERE trade_date >= CURRENT_DATE - INTERVAL '7 days';
```

### 步骤4：检查发送端（mqqc）

在发送端代码中检查：
1. `DailyDataProcessor_MQ.ConvertToDailyDataRecord` 是否计算了换手率
2. `CirculatingSharesCache.Instance.CalculateTurnoverRate` 是否返回了值
3. `DailyDataMQSender.SerializeToJson` 是否在 JSON 中包含了 `turnover_rate` 字段

## 快速验证

### 验证1：检查字段是否存在
```sql
SELECT EXISTS (
    SELECT FROM information_schema.columns
    WHERE table_name = 'stock_daily_data'
    AND column_name = 'turnover_rate'
);
```

### 验证2：检查最近更新的数据
```sql
SELECT stock_code, trade_date, turnover_rate, update_time
FROM stock_daily_data
WHERE update_time >= CURRENT_TIMESTAMP - INTERVAL '1 hour'
ORDER BY update_time DESC
LIMIT 10;
```

### 验证3：手动测试更新
```sql
-- 手动更新一条记录的换手率（测试用）
UPDATE stock_daily_data
SET turnover_rate = 3.5
WHERE stock_code = '000001' 
  AND trade_date = CURRENT_DATE - INTERVAL '1 day';

-- 检查是否更新成功
SELECT stock_code, trade_date, turnover_rate
FROM stock_daily_data
WHERE stock_code = '000001' 
  AND trade_date = CURRENT_DATE - INTERVAL '1 day';
```

## 解决方案总结

1. **如果发送端没有发送换手率**：
   - 检查发送端的流通股本缓存
   - 确保财务数据已加载
   - 检查发送端代码逻辑

2. **如果字段名不匹配**：
   - 启用调试日志查看实际 JSON 格式
   - 修改 `StockDataParser` 中的字段名解析逻辑

3. **如果数据库字段不存在**：
   - 程序启动时会自动添加
   - 或手动执行 SQL 添加字段

4. **如果数据已解析但未保存**：
   - 检查 SQL 更新语句
   - 查看是否有异常被捕获

## 代码位置

- **解析换手率**: `src/Core/Helpers/StockDataParser.cs` (第242-248行)
- **保存换手率**: `src/DataProcessing/Repositories/PostgresStockDataRepository.cs` (第119-122行)
- **处理日线数据**: `src/MQ/MQService.cs` (第363-379行)
