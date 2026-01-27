# 数据库迁移指南

## 概述

当将最新版本的数据迁移到新环境时，系统会自动检测并更新数据库表结构，确保字段与最新版本代码一致。

## 自动更新机制

### 1. 启动时自动更新

程序启动时会自动调用 `DatabaseInitializer.Initialize()`，它会：
- 创建缺失的表
- 添加缺失的字段（通过 `DatabaseSchemaUpdater.UpdateSchema()`）
- 创建必要的索引

### 2. 当前自动更新的字段

**日线数据表 (`stock_daily_data`)**：
- `turnover_rate` (NUMERIC(8,4)) - 换手率（%）

### 3. 如何添加新的自动更新字段

如果需要添加新的字段自动更新，请修改 `DatabaseSchemaUpdater.cs`：

```csharp
private static void UpdateDailyDataTable(NpgsqlConnection connection)
{
    var columnsToAdd = new Dictionary<string, string>
    {
        { "turnover_rate", "NUMERIC(8,4)" },
        { "new_field_name", "VARCHAR(100)" },  // 添加新字段
    };

    foreach (var column in columnsToAdd)
    {
        EnsureColumnExists(connection, "stock_daily_data", column.Key, column.Value);
    }
}
```

## 数据库表结构

### 1. 日线数据表 (`stock_daily_data`)

**核心字段**：
- `stock_code` (VARCHAR(10)) - 股票代码
- `trade_date` (DATE) - 交易日期
- `open_price`, `high_price`, `low_price`, `close_price` (NUMERIC(10,3)) - 价格
- `volume` (NUMERIC(20,2)) - 成交量
- `amount` (NUMERIC(20,2)) - 成交金额
- `turnover_rate` (NUMERIC(8,4)) - 换手率（%）

**唯一约束**：`(stock_code, trade_date)`

### 2. 实时数据表 (`stock_realtime_data`)

**核心字段**：
- `stock_code` (VARCHAR(10)) - 股票代码（唯一）
- `stock_name` (VARCHAR(100)) - 股票名称
- `new_price`, `open_price`, `high_price`, `low_price` - 价格
- `volume`, `amount` - 成交数据

### 3. 除权数据表 (`stock_exrights_data`)

**核心字段**：
- `stock_code` (VARCHAR(10)) - 股票代码
- `ex_rights_date` (DATE) - 除权日期
- `give_per_10_shares`, `pei_per_10_shares`, `pei_price`, `profit_per_share` - 除权信息

**唯一约束**：`(stock_code, ex_rights_date)`

### 4. 股票信息表 (`stock_info`)

**核心字段**：
- `stock_code` (VARCHAR(10)) - 股票代码（主键）
- `stock_name` (VARCHAR(100)) - 股票名称
- `market_code` (SMALLINT) - 市场代码
- `is_active` (BOOLEAN) - 是否有效

## 迁移步骤

### 方法1：使用自动更新（推荐）

1. **备份原数据库**
   ```bash
   pg_dump -h localhost -p 8532 -U postgres -d stockdb > backup.sql
   ```

2. **在新环境创建数据库**
   ```bash
   psql -h new_host -p new_port -U postgres -c "CREATE DATABASE stockdb;"
   ```

3. **导入数据**
   ```bash
   psql -h new_host -p new_port -U postgres -d stockdb < backup.sql
   ```

4. **运行程序**
   - 程序启动时会自动检测并添加缺失的字段
   - 查看控制台输出，确认字段已添加

### 方法2：手动执行SQL脚本

1. **执行建表脚本**
   ```bash
   psql -h new_host -p new_port -U postgres -d stockdb -f db/create_all_tables.sql
   ```

2. **导入数据**
   ```bash
   psql -h new_host -p new_port -U postgres -d stockdb < backup.sql
   ```

3. **执行字段更新脚本**（如果需要）
   ```sql
   ALTER TABLE stock_daily_data ADD COLUMN IF NOT EXISTS turnover_rate NUMERIC(8,4);
   ```

## 验证迁移

### 1. 检查表结构

```sql
-- 检查日线数据表的所有列
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'stock_daily_data'
ORDER BY ordinal_position;

-- 检查换手率字段是否存在
SELECT EXISTS (
    SELECT FROM information_schema.columns
    WHERE table_name = 'stock_daily_data'
    AND column_name = 'turnover_rate'
);
```

### 2. 检查数据完整性

```sql
-- 检查数据量
SELECT COUNT(*) FROM stock_daily_data;
SELECT COUNT(*) FROM stock_realtime_data;
SELECT COUNT(*) FROM stock_exrights_data;
SELECT COUNT(*) FROM stock_info;

-- 检查换手率数据
SELECT 
    COUNT(*) as total,
    COUNT(turnover_rate) as with_turnover_rate,
    COUNT(CASE WHEN turnover_rate IS NOT NULL THEN 1 END) as not_null_turnover_rate
FROM stock_daily_data
WHERE trade_date >= CURRENT_DATE - INTERVAL '30 days';
```

### 3. 检查索引

```sql
-- 检查索引是否存在
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'stock_daily_data';
```

## 常见问题

### Q1: 迁移后字段缺失怎么办？

**A**: 程序启动时会自动检测并添加缺失的字段。如果字段仍未添加，请检查：
1. `DatabaseAutoCreateTables` 配置是否为 `true`
2. 数据库连接是否正常
3. 查看控制台错误信息

### Q2: 如何手动添加字段？

**A**: 可以直接执行SQL：
```sql
ALTER TABLE stock_daily_data ADD COLUMN IF NOT EXISTS turnover_rate NUMERIC(8,4);
```

### Q3: 迁移后数据更新逻辑是否正常？

**A**: 是的。`PostgresStockDataRepository.SaveDailyData` 使用 `ON CONFLICT ... DO UPDATE SET`，会自动更新所有字段，包括：
- `volume` (成交量)
- `amount` (成交金额)
- `turnover_rate` (换手率)
- 所有价格字段

### Q4: 如何确认自动更新已执行？

**A**: 查看程序启动时的控制台输出：
```
正在检查并初始化数据库表结构...
  ✓ 表/索引已就绪: stock_info
  ✓ 表/索引已就绪: stock_daily_data
  ...
  ✓ 列已存在: stock_daily_data.turnover_rate
数据库表结构初始化完成
```

## 代码位置

- **数据库初始化**: `src/Core/Helpers/DatabaseInitializer.cs`
- **架构更新器**: `src/Core/Helpers/DatabaseSchemaUpdater.cs`
- **数据保存逻辑**: `src/DataProcessing/Repositories/PostgresStockDataRepository.cs`
- **SQL建表脚本**: `db/create_all_tables.sql`

## 注意事项

1. **备份数据**：迁移前务必备份原数据库
2. **测试环境**：建议先在测试环境验证迁移流程
3. **索引重建**：如果数据量很大，迁移后可能需要重建索引以提高性能
4. **权限检查**：确保数据库用户有创建表和添加列的权限
