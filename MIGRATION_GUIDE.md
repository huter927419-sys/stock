# PostgreSQL 到 RocksDB 迁移指南

## 概述

本指南介绍如何将数据从 PostgreSQL 数据库迁移到 RocksDB (文件系统) 存储。

## 架构说明

### 存储后端

系统现在支持两种存储后端：

1. **PostgreSQL** - 关系型数据库，适合复杂查询和数据分析
2. **RocksDB** - 基于文件系统的键值存储，适合高性能读写

### Repository 结构

```
IStockDataRepository (接口)
├── PostgresStockDataRepository (PostgreSQL 实现)
└── RocksDBStockDataRepository (RocksDB 实现)

IExRightsDataRepository (接口)
├── PostgresExRightsDataRepository (PostgreSQL 实现)
└── RocksDBExRightsDataRepository (RocksDB 实现)

IRealTimeDataRepository (接口)
├── PostgresRealTimeDataRepository (PostgreSQL 实现)
└── RocksDBRealTimeDataRepository (RocksDB 实现)
```

### Factory 模式

使用 `RepositoryFactory` 统一管理不同的存储后端：

```csharp
// 配置使用 RocksDB
RepositoryFactory.Configure(
    RepositoryFactory.StorageBackend.RocksDB,
    dbPath: "data/rocksdb"
);

// 获取仓储实例
var stockRepo = RepositoryFactory.GetStockDataRepository();
var exRightsRepo = RepositoryFactory.GetExRightsDataRepository();
```

## 迁移步骤

### 1. 准备工作

确保满足以下条件：

- PostgreSQL 数据库正常运行并包含数据
- 有足够的磁盘空间存储 RocksDB 数据
- 配置文件中包含正确的数据库连接信息

### 2. 运行迁移工具

#### 方法一：使用命令行工具

```bash
# 基本用法（使用默认路径 data/rocksdb）
MigratePostgresToRocksDB.exe

# 指定 RocksDB 路径
MigratePostgresToRocksDB.exe --path ./mydata/rocksdb

# 跳过实时数据迁移
MigratePostgresToRocksDB.exe --skip-realtime

# 不验证迁移结果（加快速度）
MigratePostgresToRocksDB.exe --no-verify
```

#### 方法二：使用代码

```csharp
using MQReceiver.Tools;

var migrationTool = new DataMigrationTool(
    pgConnectionString: null,  // null 表示使用配置文件
    rocksDbPath: "data/rocksdb"
);

// 执行完整迁移
bool success = migrationTool.MigrateAll(skipRealTime: false);

if (success)
{
    // 验证迁移结果
    migrationTool.VerifyMigration();
}
```

### 3. 切换到 RocksDB

迁移完成后，在应用程序启动时配置使用 RocksDB：

```csharp
// 在 Main 方法或启动代码中添加
using MQReceiver.DataProcessing.Factories;

// 配置使用 RocksDB
RepositoryFactory.Configure(
    RepositoryFactory.StorageBackend.RocksDB,
    dbPath: "data/rocksdb"
);
```

## 迁移过程说明

### 阶段 1: 连接测试

- 测试 PostgreSQL 数据库连接
- 初始化 RocksDB 存储目录

### 阶段 2: 股票日线数据迁移

- 从 PostgreSQL 获取所有股票代码
- 逐个股票读取日线数据
- 转换格式并保存到 RocksDB
- 显示进度和统计信息

### 阶段 3: 除权数据迁移

- 检查每只股票是否有除权数据
- 读取并保存到 RocksDB

### 阶段 4: 实时数据迁移（可选）

- 读取所有实时数据
- 保存到 RocksDB

### 阶段 5: 验证迁移结果

- 对比 PostgreSQL 和 RocksDB 的数据量
- 随机抽样验证数据一致性

## RocksDB 数据结构

### 目录结构

```
data/rocksdb/
├── kline/           # K线数据
│   ├── 000001.json
│   ├── 000002.json
│   └── ...
├── exrights/        # 除权数据
│   ├── 000001.json
│   ├── 000002.json
│   └── ...
├── realtime/        # 实时数据
│   ├── 000001.json
│   ├── 000002.json
│   └── ...
└── metadata/        # 元数据
    └── ...
```

### 数据格式

每个 JSON 文件包含该股票的所有历史数据，按日期排序：

```json
[
  {
    "TradeDate": "2024-01-01T00:00:00",
    "Open": 10.5,
    "High": 11.2,
    "Low": 10.3,
    "Close": 11.0,
    "Volume": 1000000,
    "Amount": 10500000,
    "TurnoverRate": 1.5
  },
  ...
]
```

## 性能对比

### PostgreSQL

- ✓ 支持复杂查询和聚合
- ✓ 数据一致性保证
- ✓ 成熟的备份和恢复机制
- ✗ 需要维护数据库服务
- ✗ 连接池和网络开销

### RocksDB (文件系统)

- ✓ 无需数据库服务
- ✓ 高性能读写
- ✓ 简单的文件备份
- ✓ 内存缓存加速
- ✗ 不支持复杂查询
- ✗ 需要应用层实现一致性

## 注意事项

### 数据一致性

- 迁移过程中建议停止数据写入
- 迁移完成后验证数据正确性
- 保留 PostgreSQL 数据作为备份

### 磁盘空间

- RocksDB 数据可能比 PostgreSQL 占用更多空间（JSON 格式）
- 建议预留至少 2 倍的数据大小空间

### 性能优化

- 迁移大量数据时，可以考虑分批迁移
- 使用 `--skip-realtime` 跳过不重要的实时数据
- 使用 `--no-verify` 跳过验证以加快速度

## 故障排除

### 迁移失败

1. 检查 PostgreSQL 连接是否正常
2. 检查磁盘空间是否充足
3. 检查文件权限
4. 查看详细错误日志

### 数据不一致

1. 重新运行迁移工具
2. 使用 `VerifyMigration()` 方法检查
3. 手动对比抽样数据

### 性能问题

1. 增加内存缓存大小
2. 使用 SSD 硬盘
3. 调整批处理大小

## 回退到 PostgreSQL

如果需要回退到 PostgreSQL：

```csharp
RepositoryFactory.Configure(
    RepositoryFactory.StorageBackend.PostgreSQL,
    connectionString: "your_connection_string"
);
```

## 支持与反馈

如有问题，请查看项目文档或提交 Issue。
