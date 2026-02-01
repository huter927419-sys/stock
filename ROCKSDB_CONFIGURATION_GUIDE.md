# RocksDB 存储后端完整配置指南

## 概述

本项目已完成从 PostgreSQL 到 RocksDB（文件系统）的全面迁移支持，现在支持两种存储后端的无缝切换。

## 数据库表结构映射

PostgreSQL 中的所有表都已映射到 RocksDB 文件系统：

| PostgreSQL 表 | RocksDB 文件 | Repository 类 |
|--------------|-------------|--------------|
| stock_daily_data | kline/*.json | RocksDBStockDataRepository |
| stock_info | metadata/stock_names.json | RocksDBStockDataRepository |
| stock_exrights_data | exrights/*.json | RocksDBExRightsDataRepository |
| stock_realtime_data | realtime/*.json | RocksDBRealTimeDataRepository |
| adjustment_task | tasks/adjustment_tasks.json | RocksDB AdjustmentTaskRepository |
| data_receive_log | logs/receive_logs.json | RocksDBDataReceiveLogRepository |

## RocksDB 目录结构

```
data/rocksdb/
├── kline/                    # K线数据
│   ├── 000001.json          # 平安银行
│   ├── 000002.json          # 万科A
│   └── ...                  # 其他股票
├── exrights/                # 除权数据
│   ├── 000001.json
│   └── ...
├── realtime/                # 实时数据
│   ├── 000001.json
│   └── ...
├── metadata/                # 元数据
│   └── stock_names.json     # 股票名称映射
├── tasks/                   # 任务数据
│   └── adjustment_tasks.json # 复权计算任务
└── logs/                    # 日志数据
    └── receive_logs.json    # 数据接收日志
```

## 快速开始

### 1. 配置存储后端

在应用程序启动时配置：

```csharp
using MQReceiver.DataProcessing.Factories;

// 方式1: 使用 RocksDB (推荐用于生产环境)
RepositoryFactory.Configure(
    RepositoryFactory.StorageBackend.RocksDB,
    dbPath: "data/rocksdb"
);

// 方式2: 使用 PostgreSQL
RepositoryFactory.Configure(
    RepositoryFactory.StorageBackend.PostgreSQL,
    connectionString: "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=your_password"
);
```

### 2. 使用 StorageConfiguration 类

```csharp
using MQReceiver;

// 在应用程序启动时
StorageConfiguration.Initialize();  // 自动从配置文件读取并初始化

// 测试连接
bool connected = StorageConfiguration.TestConnection();

// 运行时切换（不推荐）
StorageConfiguration.SwitchBackend("RocksDB");
```

### 3. 配置文件设置

在 `appsettings.json` 或配置文件中添加：

```json
{
  "StorageBackend": "RocksDB",
  "RocksDBPath": "data/rocksdb",

  "DatabaseHost": "localhost",
  "DatabasePort": 8532,
  "DatabaseName": "stockdb",
  "DatabaseUser": "postgres",
  "DatabasePassword": "your_password"
}
```

## 数据迁移

### 完整迁移所有表

```bash
# 迁移所有数据（推荐）
MigratePostgresToRocksDB.exe

# 跳过实时数据和日志
MigratePostgresToRocksDB.exe --skip-realtime --skip-logs

# 自定义路径
MigratePostgresToRocksDB.exe --path ./custom/path/rocksdb

# 不验证（加快速度）
MigratePostgresToRocksDB.exe --no-verify
```

### 在代码中迁移

```csharp
using MQReceiver.Tools;

var migrationTool = new DataMigrationTool(
    pgConnectionString: null,  // 使用配置文件
    rocksDbPath: "data/rocksdb"
);

// 执行完整迁移（所有表）
bool success = migrationTool.MigrateAll(
    skipRealTime: false,  // 是否跳过实时数据
    skipLogs: false       // 是否跳过日志数据
);

if (success)
{
    // 验证迁移结果
    migrationTool.VerifyMigration();
}
```

### 单独迁移特定表

```csharp
// 只迁移股票数据
migrationTool.MigrateStockData();

// 只迁移股票信息
migrationTool.MigrateStockInfo();

// 只迁移除权数据
migrationTool.MigrateExRightsData();

// 只迁移实时数据
migrationTool.MigrateRealTimeData();

// 只迁移复权任务
migrationTool.MigrateAdjustmentTasks();

// 只迁移日志数据
migrationTool.MigrateDataReceiveLogs();
```

## 使用仓储

所有仓储都通过 RepositoryFactory 获取，自动使用配置的存储后端：

```csharp
using MQReceiver.DataProcessing.Factories;

// 获取股票数据仓储
var stockRepo = RepositoryFactory.GetStockDataRepository();

// 获取除权数据仓储
var exRightsRepo = RepositoryFactory.GetExRightsDataRepository();

// 获取实时数据仓储
var realTimeRepo = RepositoryFactory.GetRealTimeDataRepository();

// 使用方法完全相同，无需关心底层存储
var stockCodes = stockRepo.GetAllStockCodes();
var data = stockRepo.GetDailyData("000001", startDate, endDate);
```

## 完整的接口实现

### IStockDataRepository

```csharp
// 测试连接
bool TestConnection();

// 保存日线数据
int SaveDailyData(List<DailyDataRecord> records);

// 获取日线数据
List<DailyKlineData> GetDailyData(string stockCode, DateTime startDate, DateTime endDate);
List<DailyKlineData> GetLatestDailyData(string stockCode, int count);

// 检查数据
bool HasData(string stockCode);
(DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode);

// 获取股票列表
List<string> GetAllStockCodes();
DateTime? GetLatestTradeDate();

// 股票信息
Dictionary<string, string> GetAllStockNames();
int SaveStockInfo(List<(string StockCode, string StockName, ushort MarketCode)> stockInfoList);
int InitializeStockInfoFromDailyData();

// 删除数据
bool DeleteStockData(string stockCode);

// 更新数据
int UpdateDailyData(List<KlineData> dataList);

// 获取成交金额和换手率
(decimal? Amount, decimal? TurnoverRate) GetYesterdayAmountAndTurnoverRate(string stockCode, DateTime tradeDate);
Dictionary<string, (decimal? Amount, decimal? TurnoverRate)> GetYesterdayAmountAndTurnoverRateBatch(List<string> stockCodes, DateTime tradeDate);
Dictionary<string, decimal?> GetYesterdayAmountBatch(List<string> stockCodes, DateTime tradeDate);
```

### IExRightsDataRepository

```csharp
bool TestConnection();
int SaveExRightsData(List<ExRightsDataRecord> records);
List<ExRightsDataRecord> GetExRightsData(string stockCode);
List<ExRightsDataRecord> GetExRightsData(string stockCode, DateTime startDate, DateTime endDate);
bool HasExRightsData(string stockCode);
bool DeleteExRightsData(string stockCode);
List<ExRightsDataRecord> GetExRightsDataAfterDate(string stockCode, DateTime targetDate);
List<ExRightsDataRecord> GetExRightsDataBeforeDate(string stockCode, DateTime targetDate);
```

### IRealTimeDataRepository

```csharp
bool TestConnection();
int SaveRealTimeData(List<RealTimeDataRecord> records);
RealTimeDataRecord GetRealTimeData(string stockCode);
List<RealTimeDataRecord> GetAllRealTimeData();
```

### IAdjustmentTaskRepository

```csharp
int AddTask(AdjustmentTask task);
int AddTasks(List<AdjustmentTask> tasks);
List<AdjustmentTask> GetPendingTasks(int limit = 100);
bool UpdateTaskStatus(long taskId, string status, string errorMessage = null);
List<AdjustmentTask> GetTasksByStockCode(string stockCode);
int DeleteCompletedTasks(DateTime beforeDate);
```

### IDataReceiveLogRepository

```csharp
int AddLog(DataReceiveLog log);
List<DataReceiveLog> GetRecentLogs(int limit = 100);
List<DataReceiveLog> GetLogsByTimeRange(DateTime startTime, DateTime endTime);
List<DataReceiveLog> GetFailedLogs(int limit = 100);
int DeleteOldLogs(DateTime beforeDate);
```

## 性能对比

| 操作 | PostgreSQL | RocksDB | 说明 |
|-----|-----------|---------|------|
| 连接开销 | 需要网络连接 | 无 | RocksDB 是本地文件 |
| 查询单股票 | ~10-50ms | ~1-5ms | RocksDB 使用内存缓存 |
| 批量查询 | 较快（数据库优化） | 非常快 | 文件系统读取 |
| 写入性能 | 中等（事务保证） | 快 | 直接写JSON文件 |
| 复杂查询 | 支持 | 不支持 | 需要应用层实现 |
| 部署依赖 | 需要PostgreSQL服务 | 无 | 仅需文件系统 |

## 备份和恢复

### PostgreSQL 备份

```bash
pg_dump -h localhost -p 8532 -U postgres -d stockdb > backup.sql
```

### RocksDB 备份

```bash
# 直接复制整个目录
xcopy /E /I data\rocksdb backup\rocksdb_20240101

# 或使用压缩
tar -czf rocksdb_backup_20240101.tar.gz data/rocksdb
```

### RocksDB 恢复

```bash
# 直接复制回来
xcopy /E /I backup\rocksdb_20240101 data\rocksdb

# 或解压
tar -xzf rocksdb_backup_20240101.tar.gz
```

## 注意事项

### 磁盘空间

- RocksDB 使用 JSON 格式，可能比 PostgreSQL 占用更多空间
- 建议预留至少 2-3 倍数据大小的磁盘空间
- 定期清理旧日志和已完成的任务

### 数据一致性

- RocksDB 没有事务保证，需要在应用层处理
- 建议定期备份重要数据
- 迁移后保留 PostgreSQL 数据作为备份

### 并发访问

- RocksDB 实现使用文件锁保证线程安全
- 不支持跨进程并发写入
- 单进程多线程访问是安全的

## 故障排除

### 迁移失败

```
问题：连接 PostgreSQL 失败
解决：检查连接字符串、防火墙、数据库服务状态

问题：磁盘空间不足
解决：清理磁盘或指定其他路径

问题：数据不一致
解决：重新运行迁移工具，或手动验证
```

### 性能问题

```
问题：查询速度慢
解决：检查内存缓存是否启用、增加缓存大小

问题：写入速度慢
解决：使用 SSD 硬盘、减少同步频率

问题：文件太多
解决：定期清理旧数据、压缩历史数据
```

## 常见问题

**Q: 可以同时使用 PostgreSQL 和 RocksDB 吗？**
A: 可以，但需要手动管理，不建议在生产环境这样做。建议选择一种作为主存储。

**Q: 如何回退到 PostgreSQL？**
A: 在配置中切换 `StorageBackend` 为 `PostgreSQL`，重启应用即可。

**Q: RocksDB 数据会自动同步到 PostgreSQL 吗？**
A: 不会。两个存储是独立的，需要手动迁移。

**Q: 迁移需要多长时间？**
A: 取决于数据量，1万只股票约需要 10-30 分钟。

**Q: 可以增量迁移吗？**
A: 可以，重复运行迁移工具会覆盖现有数据。

## 相关文档

- [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md) - 详细的迁移指南
- [DATABASE_MIGRATION_GUIDE.md](DATABASE_MIGRATION_GUIDE.md) - 数据库迁移说明

## 技术支持

如有问题，请查看项目文档或提交 Issue。
