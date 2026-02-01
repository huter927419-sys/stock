# RocksDB 日线数据说明

## 数据在哪里

- **RocksDB 路径**由 `App.config` 中的 `RocksDBPath` 决定（默认 `data/rocksdb`）。
- 该路径是**相对于 exe 所在目录**解析的，不是相对于项目根目录。
- 日线数据实际存放在：`<exe所在目录>\data\rocksdb\kline\` 下，每只股票一个 `股票代码.json` 文件。

常见情况：

| 运行方式           | exe 位置              | 日线数据实际路径                    |
|--------------------|------------------------|-------------------------------------|
| Visual Studio 选 x64 运行 | `bin\x64\Debug\`       | `bin\x64\Debug\data\rocksdb\kline\` |
| Visual Studio 选 AnyCPU 运行 | `bin\Debug\`           | `bin\Debug\data\rocksdb\kline\`     |
| 双击 check_rocksdb.bat | 见批处理内使用的 exe   | 同上，在对应 exe 的 data\rocksdb    |

当前您机器上**已有日线数据**的位置是：

- `F:\dsfr\mqq\bin\x64\Debug\data\rocksdb\kline\`  
  - 约 2309 万条日线、15167 只股票（迁移后统计）。

若在**主程序/过滤/图表里“看不到”日线数据**，多半是：

- 实际运行的是 **AnyCPU** 的 exe（在 `bin\Debug\`），那里的 `data\rocksdb` 为空；  
  而数据在 **x64** 的 exe 目录下（`bin\x64\Debug\data\rocksdb`）。

**解决办法（任选其一）：**

1. **用 x64 运行**  
   - 在 Visual Studio 顶部把运行配置选为 **x64**（或对应生成 x64 的配置），再运行，这样会用到 `bin\x64\Debug\data\rocksdb` 里的日线数据。

2. **不改平台，改配置路径**  
   - 在 `App.config` 里把 `RocksDBPath` 改成**绝对路径**，指向已有数据目录，例如：  
     `F:\dsfr\mqq\bin\x64\Debug\data\rocksdb`  
   - 保存后，无论用 AnyCPU 还是 x64 运行，都会读这一份日线数据。

## 如何“看到”日线数据

1. **命令行查询并生成报告**  
   - 运行：`MQReceiver.exe --check-rocksdb`  
   - 或双击项目里的 `check_rocksdb.bat`（会优先用 x64 的 exe）。  
   - 程序会统计股票数、日线条数、日期范围，并抽样几只股票的最早/最新几条，写入 exe 同目录下的 `rocksdb_check_report.txt`。

2. **数据迁移窗口**  
   - 启动菜单选 **[3] 数据迁移**，在迁移窗口里可：  
     - 查看“数据库概况”（日线/除权等表记录数）；  
     - 使用“查看最早数据”“查询该股最早数据”查看具体日线内容。

3. **直接看文件**  
   - 用资源管理器打开：`bin\x64\Debug\data\rocksdb\kline\`  
   - 每个 `股票代码.json` 即该股票的日 K 线数组（按日期排序）。

## 小结

- RocksDB 日线数据**已经存在**于 `bin\x64\Debug\data\rocksdb\kline\`。  
- “看不到”多半是**运行目录/平台**和**数据目录**不一致：请用 x64 运行，或把 `RocksDBPath` 改为上述绝对路径。
