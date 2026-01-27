# 股票数据管理系统 - 安装指南

## 目录
1. [系统要求](#系统要求)
2. [环境准备](#环境准备)
3. [数据库配置](#数据库配置)
4. [应用程序配置](#应用程序配置)
5. [安装与部署](#安装与部署)
6. [启动与验证](#启动与验证)
7. [常见问题](#常见问题)

---

## 系统要求

### 操作系统
- **Windows 10** (版本 1809 或更高)
- **Windows 11**
- **Windows Server 2016** 或更高版本

### 硬件要求

#### 最低配置
- **CPU**：双核 2.0 GHz 或更高
- **内存**：4 GB RAM
- **硬盘空间**：20 GB 可用空间
- **显示器**：分辨率 1280×720 或更高
- **网络**：用于接收股票数据（TCP端口 5678）

#### 推荐配置
- **CPU**：四核 3.0 GHz 或更高
- **内存**：8 GB RAM 或更高
- **硬盘空间**：50 GB 可用空间（SSD推荐）
- **显示器**：分辨率 1920×1080 或更高，支持多屏显示
- **网络**：稳定的网络连接

### 磁盘空间分配建议

| 组件 | 所需空间 | 说明 |
|------|----------|------|
| 应用程序 | 100 MB | 程序文件及依赖库 |
| PostgreSQL | 500 MB | 数据库软件安装 |
| 数据库数据 | 10-30 GB | 根据数据量增长（建议预留30GB） |
| Redis | 50 MB | Redis软件（如使用） |
| 系统临时文件 | 2 GB | 运行时临时文件 |
| **总计** | **约 20-35 GB** | 建议预留 50 GB |

**注意**：数据库数据会持续增长，建议定期备份并清理历史数据。

---

## 环境准备

### 1. 安装 .NET Framework 4.8（必需）

#### 检查是否已安装
```powershell
reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release
```

**判断标准**：
- 如果显示版本号 **>= 528040**，说明已安装 .NET Framework 4.8
- 如果显示版本号 **< 528040** 或命令失败，需要安装

#### 安装步骤
1. **下载安装程序**
   - 官方下载地址：https://dotnet.microsoft.com/download/dotnet-framework/net48
   - 选择 "Runtime" 版本（运行时，不需要开发工具）
   - 文件大小：约 1.2 MB（在线安装器）或 60 MB（离线安装包）

2. **运行安装程序**
   - 双击下载的安装程序
   - 按照向导完成安装
   - 安装过程可能需要 5-10 分钟

3. **重启计算机**（如提示）
   - 安装完成后，系统可能提示重启
   - 建议立即重启以确保环境生效

4. **验证安装**
   ```powershell
   reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release
   ```
   应显示：`Release    REG_DWORD    0x000080f0`（528240）或更高

#### .NET Framework 版本说明
- **.NET Framework 4.8** 是必需的运行时环境
- 本程序基于 .NET Framework 4.8 开发，不支持更低版本
- Windows 10 1809 及以上版本通常已预装，但建议验证版本号
- Windows 11 已预装 .NET Framework 4.8

### 2. 安装 Microsoft Edge WebView2 Runtime（必需）

#### 检查是否已安装
- Windows 11 已内置 WebView2
- Windows 10 可能已通过 Windows Update 自动安装

#### 安装步骤
1. **下载安装程序**
   - 官方下载地址：https://developer.microsoft.com/microsoft-edge/webview2/
   - 选择 "Evergreen Runtime" → "x64" 版本
   - 文件大小：约 130 MB

2. **运行安装程序**
   - 双击下载的安装程序
   - 按照向导完成安装
   - 安装过程约 1-2 分钟

3. **验证安装**
   - 打开注册表编辑器
   - 查看：`HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E9C5}`
   - 如果存在该键，说明已安装

**注意**：WebView2 用于显示K线图表，如果未安装，图表功能将无法使用。

### 3. 安装 PostgreSQL（必需）

#### 版本要求
- PostgreSQL **12** 或更高版本
- 推荐版本：**PostgreSQL 14** 或 **15**

#### 安装步骤
1. **下载安装程序**
   - 官方下载地址：https://www.postgresql.org/download/windows/
   - 选择最新稳定版本（14.x 或 15.x）
   - 文件大小：约 200-300 MB

2. **运行安装程序**
   - 双击下载的安装程序
   - 选择安装路径：建议 `C:\Program Files\PostgreSQL\15`（根据版本调整）
   - **重要配置**：
     - **端口**：设置为 **8532**（默认是5432，本系统使用8532）
     - **超级用户密码**：设置强密码（**务必记住此密码**）
     - **数据目录**：使用默认或自定义（建议至少预留 30 GB 空间）
     - **地区设置**：选择 "Chinese, Simplified"（可选）

3. **安装完成后验证**
   ```powershell
   psql --version
   ```
   应显示 PostgreSQL 版本信息

4. **配置远程连接**（如需要）
   - 编辑 `postgresql.conf`（位于安装目录的 `data` 文件夹）
     - 设置 `listen_addresses = '*'`
   - 编辑 `pg_hba.conf`（同一目录）
     - 添加允许连接的规则，例如：`host all all 0.0.0.0/0 md5`
   - 重启 PostgreSQL 服务

### 4. 安装 Redis（可选，但推荐）

Redis 用于缓存数据，提升性能。如果未安装，程序将直接查询数据库。

#### 方式一：Windows 原生版本
1. **下载 Redis for Windows**
   - 下载地址：https://github.com/microsoftarchive/redis/releases
   - 选择最新版本（如 `Redis-x64-3.0.504.zip`）
   - 文件大小：约 5-10 MB

2. **解压并运行**
   - 解压到 `C:\Redis`（或自定义路径）
   - 运行 `redis-server.exe` 启动服务
   - 验证：运行 `redis-cli.exe ping`，应返回 `PONG`

3. **配置为Windows服务**（可选）
   - 使用 `redis-server --service-install` 安装为服务
   - 使用 `redis-server --service-start` 启动服务

#### 方式二：WSL2（推荐，性能更好）
1. **在 WSL2 中安装 Redis**
   ```bash
   sudo apt update
   sudo apt install redis-server
   sudo service redis-server start
   ```

2. **配置 Redis 允许从 Windows 访问**（如需要）
   - 编辑 `/etc/redis/redis.conf`
   - 设置 `bind 0.0.0.0`（允许外部连接）

### 5. 配置网络端口

程序使用 TCP Socket 接收数据，需要确保以下端口可用：

- **端口 5678**：MQ数据接收端口（可在 `App.config` 中修改 `MQPort`）
- 如果使用防火墙，需要允许该端口的入站连接

**验证端口是否被占用**：
```powershell
netstat -ano | findstr :5678
```

如果端口被占用，可以：
1. 修改 `App.config` 中的 `MQPort` 为其他端口
2. 或关闭占用该端口的其他程序

---

## 数据库配置

### 1. 创建数据库

使用 PostgreSQL 客户端（psql 或 pgAdmin）执行：

```sql
-- 连接到 PostgreSQL
psql -h localhost -p 8532 -U postgres

-- 创建数据库
CREATE DATABASE stockdb;

-- 退出
\q
```

### 2. 执行建表脚本

```powershell
# 在项目根目录执行
psql -h localhost -p 8532 -U postgres -d stockdb -f db\create_all_tables.sql
```

或使用 pgAdmin：
1. 连接到 PostgreSQL 服务器
2. 选择 `stockdb` 数据库
3. 打开查询工具
4. 执行 `db\create_all_tables.sql` 文件内容

### 3. 验证数据库结构

```sql
-- 连接到 stockdb
psql -h localhost -p 8532 -U postgres -d stockdb

-- 查看表列表
\dt

-- 应看到以下主要表：
-- stock_info
-- stock_daily_data
-- stock_realtime_data
-- stock_exrights_data
-- kline_data_weekly
-- kline_data_monthly
-- kline_data_quarterly
```

---

## 应用程序配置

### 1. 解压程序文件

将提供的程序包解压到目标目录，例如：
```
C:\Program Files\MQReceiver\
```

### 2. 修改 App.config

编辑程序目录下的 `App.config` 文件（或 `MQReceiver.exe.config`），配置以下参数：

#### 数据库配置
```xml
<add key="DatabaseHost" value="localhost" />
<add key="DatabasePort" value="8532" />
<add key="DatabaseName" value="stockdb" />
<add key="DatabaseUser" value="postgres" />
<add key="DatabasePassword" value="你的PostgreSQL密码" />
```

#### Redis 配置
```xml
<add key="RedisHost" value="localhost" />
<add key="RedisPort" value="6379" />
<add key="RedisPassword" value="" />
<add key="RedisDatabase" value="0" />
```

#### MQ 配置（TCP Socket）
```xml
<add key="MQPort" value="5678" />
<add key="MQQueueName" value="daily_data_queue" />
```
**注意**：`MQPort` 是 TCP Socket 监听端口，不是 RabbitMQ 端口。`MQQueueName` 用于标识数据类型，通过 TCP 协议传输。

#### 其他配置
- `FilterService_IntervalMinutes`：过滤服务运行间隔（分钟）
- `PreloadService_BatchSize`：预加载批次大小
- `PreloadService_MaxParallelism`：最大并行数

### 3. 配置过滤条件阈值（可选）

在 `App.config` 中修改全局阈值：
```xml
<add key="GlobalThreshold_M1" value="78" />
<add key="GlobalThreshold_M2" value="65" />
<add key="GlobalThreshold_M3" value="50" />
<add key="GlobalThreshold_M4" value="30" />
<add key="GlobalThreshold_N" value="5" />
```

---

## 安装与部署

### 部署文件清单

确保以下文件存在于程序目录：
```
MQReceiver.exe              # 主程序
App.config                  # 配置文件（或 MQReceiver.exe.config）
所有 DLL 文件               # 依赖库（如果未打包为单文件）
```

### 首次运行前检查清单

- [ ] .NET Framework 4.8 已安装并验证
- [ ] WebView2 Runtime 已安装
- [ ] PostgreSQL 已安装并运行
- [ ] Redis 已安装并运行（可选）
- [ ] 数据库 `stockdb` 已创建
- [ ] 数据库表已创建（执行了建表脚本）
- [ ] `App.config` 已正确配置
- [ ] 端口 5678 未被占用
- [ ] 防火墙已允许相关端口

---

## 启动与验证

### 1. 启动服务

#### 方式一：直接运行（推荐）
双击 `MQReceiver.exe`，程序会显示主菜单：
```
========================================
股票数据管理系统 v2.1
========================================
[0] 启动KD过滤器服务（WPF界面）
[1] 启动MQ数据接收服务
[2] 启动数据预加载服务
[3] 退出
========================================
```

#### 方式二：命令行参数
```powershell
# 直接启动过滤器服务（无菜单）
MQReceiver.exe --filter

# 启动MQ接收服务
MQReceiver.exe --mq

# 启动预加载服务
MQReceiver.exe --preload
```

### 2. 验证连接

#### 验证数据库连接
程序启动时会自动测试数据库连接，查看控制台输出：
```
[数据库连接] 正在连接 PostgreSQL...
[数据库连接] ✅ 连接成功
```

#### 验证 Redis 连接
```
[Redis连接] 正在连接 Redis...
[Redis连接] ✅ 连接成功
```
**注意**：如果 Redis 未安装或连接失败，程序会显示警告但继续运行，将直接使用数据库查询。

#### 验证 MQ 服务
```
[MQ服务] MQ接收器已启动，监听端口: 5678
[MQ服务] 等待虚拟机连接...
```
**注意**：MQ服务是TCP Socket服务器，等待数据发送端连接，不需要连接外部服务。

### 3. 功能测试

1. **启动过滤器服务**
   - 选择菜单项 `[0]`
   - 应显示主窗口
   - 点击"开始过滤"按钮
   - 查看是否有数据输出

2. **测试K线图**
   - 在过滤结果中双击股票代码
   - 应弹出K线图窗口
   - 验证图表显示正常

3. **测试MQ接收**
   - 选择菜单项 `[1]`
   - 等待数据发送端连接
   - 查看控制台日志

---

## 常见问题

### 1. .NET Framework 4.8 相关问题

#### 问题：程序无法启动，提示缺少 .NET Framework
**解决方案**：
- 下载并安装 .NET Framework 4.8 Runtime
- 安装后重启计算机
- 验证安装：运行 `reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release`

#### 问题：已安装但版本不对
**解决方案**：
- 卸载旧版本（如 4.7.2）
- 安装 .NET Framework 4.8
- 确保版本号 >= 528040

### 2. 数据库连接失败

**错误信息**：`无法连接到数据库`

**解决方案**：
- 检查 PostgreSQL 服务是否运行
- 验证 `App.config` 中的连接信息
- 检查防火墙是否阻止端口 8532
- 验证用户名和密码是否正确
- 确认数据库 `stockdb` 已创建

### 3. Redis 连接失败

**错误信息**：`无法连接到 Redis`

**解决方案**：
- Redis 是可选的，连接失败不会影响程序运行
- 如需使用 Redis，检查 Redis 服务是否运行：`redis-cli ping`
- 验证端口 6379 是否被占用
- 检查防火墙设置
- 如使用 WSL2，确保网络配置正确

### 4. MQ 端口被占用

**错误信息**：`启动MQ服务失败` 或 `端口已被占用`

**解决方案**：
- 检查端口 5678 是否被占用：`netstat -ano | findstr :5678`
- 修改 `App.config` 中的 `MQPort` 为其他端口
- 关闭占用该端口的其他程序
- 检查防火墙是否阻止该端口

### 5. WebView2 初始化失败

**错误信息**：`初始化WebView2失败`

**解决方案**：
- 安装 Microsoft Edge WebView2 Runtime
- 确保 Windows 已更新到最新版本
- 检查是否有杀毒软件阻止
- 重启计算机后重试

### 6. 图表不显示

**可能原因**：
- WebView2 未安装
- JavaScript 库加载失败
- 数据为空

**解决方案**：
- 查看控制台日志
- 验证股票数据是否已加载
- 重新安装 WebView2 Runtime

### 7. 过滤结果为空

**可能原因**：
- 数据库中没有数据
- 过滤条件太严格
- KD 值未计算

**解决方案**：
- 检查数据库是否有日线数据
- 运行数据预加载服务
- 调整过滤条件阈值
- 查看诊断日志（设置 `FilterDiagnose_56=true`）

### 8. 性能问题

**症状**：程序运行缓慢、界面卡顿

**优化建议**：
- 减少 `PreloadService_MaxParallelism` 值
- 增加 `PreloadService_BatchSize` 值
- 检查数据库索引是否创建
- 使用 SSD 存储数据库文件
- 增加系统内存
- 确保有足够的磁盘空间

### 9. 多屏显示问题

**症状**：K线图窗口不在正确的屏幕上打开

**解决方案**：
- 检查 `App.config` 中的 `ChartWindow_Left` 值
- 如果屏幕断开，程序会自动回退到主屏
- 手动调整窗口位置，程序会自动保存

### 10. 磁盘空间不足

**症状**：数据库写入失败、程序运行缓慢

**解决方案**：
- 检查磁盘剩余空间：至少保留 10 GB
- 清理数据库历史数据（如需要）
- 移动数据库数据目录到空间更大的磁盘
- 定期备份并清理旧数据

---

## 升级说明

### 从旧版本升级

1. **备份数据**
   ```sql
   pg_dump -h localhost -p 8532 -U postgres -d stockdb > backup.sql
   ```

2. **备份配置文件**
   - 复制 `App.config` 到安全位置

3. **停止旧版本**
   - 关闭所有运行中的程序实例

4. **安装新版本**
   - 替换可执行文件
   - 恢复 `App.config` 配置

5. **执行数据库迁移**（如有）
   - 查看 `db` 目录下的迁移脚本
   - 按顺序执行

6. **验证功能**
   - 启动程序并测试各项功能

---

## 技术支持

如遇到问题，请提供以下信息：
1. 错误日志（控制台输出）
2. `App.config` 配置（隐藏敏感信息）
3. 数据库版本和 PostgreSQL 版本
4. Windows 版本
5. .NET Framework 版本（运行验证命令的结果）
6. 磁盘剩余空间

---

## 附录

### 端口列表

| 服务 | 端口 | 说明 |
|------|------|------|
| PostgreSQL | 8532 | 数据库服务（默认5432，本系统使用8532） |
| Redis | 6379 | 缓存服务（可选） |
| MQ接收服务 | 5678 | TCP Socket监听端口（可在配置中修改） |

### 目录结构

```
MQReceiver/
├── MQReceiver.exe         # 主程序
├── App.config             # 配置文件
├── *.dll                   # 依赖库文件
└── db/                     # 数据库脚本（如提供）
    └── create_all_tables.sql
```

### 配置文件说明

`App.config` 主要配置项：

- **数据库配置**：PostgreSQL 连接信息
- **Redis配置**：缓存服务连接信息（可选）
- **MQ配置**：TCP Socket 监听端口和队列名称（用于标识数据类型）
- **过滤条件配置**：6个过滤面板的阈值设置
- **窗口位置配置**：K线图窗口位置和大小
- **列宽配置**：主界面表格列宽设置

### .NET Framework 版本对照表

| 版本 | Release 值 | 说明 |
|------|------------|------|
| .NET Framework 4.7.2 | 461808 | 不支持 |
| .NET Framework 4.8 | 528040 | **必需** |
| .NET Framework 4.8.1 | 533320 | 支持 |

---

**版本**：2.1  
**最后更新**：2025-01-XX  
**维护者**：开发团队
