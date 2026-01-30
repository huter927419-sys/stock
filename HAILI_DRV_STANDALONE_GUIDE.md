# HaiLiDrv独立运行指南

## 概述

HaiLiDrv窗口现在支持两种运行模式：
1. **集成模式**：从主窗口启动，共享主程序的缓存和配置
2. **独立模式**：独立运行，使用独立的配置文件（HaiLiDrv.config）

## 启动方式

### 方式1：可视化启动菜单（推荐）

程序启动时会自动显示可视化启动菜单，包含以下选项：

- **[1] 主程序（计算器）** - 启动完整的主程序界面
- **[2] HaiLiDrv数据窗口（独立模式）** - 启动独立的HaiLiDrv窗口
- **[3] MQ数据接收服务（控制台）** - 仅启动数据接收服务
- **[0] 退出** - 退出程序

**操作方式**：
- 鼠标点击选项
- 或使用键盘数字键（1、2、3、0）快速选择

### 方式2：从主窗口启动

在主窗口（FilterMainWindow）中点击 **"数据窗口"** 按钮，会以集成模式启动HaiLiDrv窗口。

### 方式3：命令行启动

```bash
# 启动主程序
MQReceiver.exe

# 启动HaiLiDrv独立窗口（通过菜单选择）
MQReceiver.exe
# 然后选择选项 [2]
```

## 数据源自动切换

HaiLiDrv窗口会根据当前时间自动选择数据源：

### 交易时间（9:30-11:30, 13:00-15:00，工作日）
- **数据源**：实时数据（从`RealTimeDataCache`获取）
- **显示**：实时行情数据，包括最新价、涨跌幅、成交量、成交额等
- **更新频率**：每3秒自动刷新（可在配置文件中修改）

### 收盘后（非交易时间）
- **数据源**：日线数据（从数据库`stock_daily_data`表获取）
- **显示**：最新交易日的收盘数据
- **更新频率**：每3秒自动刷新（但数据不会变化，因为是历史数据）

## 独立配置文件

独立模式使用 `HaiLiDrv.config` 文件（与exe同目录），包含以下配置：

```xml
<appSettings>
  <!-- HaiLiDrv窗口位置与大小 -->
  <add key="HaiLiDrvWindow_Left" value="1920" />
  <add key="HaiLiDrvWindow_Top" value="0" />
  <add key="HaiLiDrvWindow_Width" value="1920" />
  <add key="HaiLiDrvWindow_Height" value="1080" />
  
  <!-- 数据刷新间隔（秒） -->
  <add key="HaiLiDrv_RefreshIntervalSeconds" value="3" />
  
  <!-- 显示数据条数限制 -->
  <add key="HaiLiDrv_MaxDisplayCount" value="500" />
  
  <!-- 数据库配置（与主程序共享） -->
  <add key="DatabaseHost" value="localhost" />
  <add key="DatabasePort" value="8532" />
  <add key="DatabaseName" value="stockdb" />
  <add key="DatabaseUser" value="postgres" />
  <add key="DatabasePassword" value="" />
  
  <!-- K线图窗口位置（独立保存） -->
  <add key="HaiLiDrv_ChartWindow_Left" value="3000" />
  <add key="HaiLiDrv_ChartWindow_Top" value="100" />
  <add key="HaiLiDrv_ChartWindow_Width" value="1400" />
  <add key="HaiLiDrv_ChartWindow_Height" value="950" />
  
  <!-- 指定显示的股票代码列表（逗号分隔，为空则显示全部） -->
  <!-- 示例：600000,600001,000001,000002 -->
  <add key="HaiLiDrv_StockCodes" value="" />
  
  <!-- 是否启用股票代码过滤（true=只显示配置的股票，false=显示全部） -->
  <add key="HaiLiDrv_EnableStockCodeFilter" value="false" />
</appSettings>
```

**注意**：首次运行会自动创建默认配置文件。

### 股票代码配置

HaiLiDrv支持配置要显示的股票代码列表，可以只显示指定的股票：

#### 方式1：通过UI配置（推荐）

1. 在HaiLiDrv窗口中点击 **"配置股票代码"** 按钮
2. 在对话框中输入股票代码：
   - 每行一个股票代码
   - 或使用逗号分隔：`600000,600001,000001`
3. 勾选 **"启用股票代码过滤"** 复选框
4. 点击 **"保存"** 按钮
5. 配置立即生效，数据会自动刷新

#### 方式2：手动编辑配置文件

编辑 `HaiLiDrv.config` 文件：

```xml
<!-- 股票代码列表（逗号分隔） -->
<add key="HaiLiDrv_StockCodes" value="600000,600001,000001,000002,300001" />

<!-- 启用过滤 -->
<add key="HaiLiDrv_EnableStockCodeFilter" value="true" />
```

**配置说明**：
- `HaiLiDrv_EnableStockCodeFilter = false`：显示全部股票（默认）
- `HaiLiDrv_EnableStockCodeFilter = true` 且 `HaiLiDrv_StockCodes` 为空：显示全部股票
- `HaiLiDrv_EnableStockCodeFilter = true` 且 `HaiLiDrv_StockCodes` 有值：只显示配置的股票代码

**股票代码格式**：
- 必须是6位数字（如：`600000`, `000001`, `300001`）
- 支持多种分隔符：逗号、分号、空格、换行
- 不区分大小写

## 功能特性

### 1. 数据自动切换
- 交易时间：显示实时数据
- 收盘后：自动切换到日线数据
- 状态栏会显示当前数据源类型

### 2. 多屏支持
- 自动检测第二个屏幕
- 窗口自动在附加屏上最大化显示
- 窗口位置自动保存到配置文件

### 3. K线图集成
- 双击股票行打开K线图
- 使用单例模式，确保同时只有一个K线图窗口
- K线图位置独立保存（与主程序的K线图位置分开）

### 4. 数据排序
- 默认按成交额从高到低排序
- 显示前500条记录（可在配置文件中修改）

### 5. 股票代码过滤
- 支持配置要显示的股票代码列表
- 可通过UI界面或直接编辑配置文件
- 支持启用/禁用过滤功能
- 股票代码格式：6位数字（如：600000, 000001）
- 配置后立即生效，无需重启

## 资源复用

### 独立模式
- **配置文件**：使用独立的`HaiLiDrv.config`
- **数据缓存**：创建新的`RealTimeDataCache`实例
- **数据库连接**：共享主程序的数据库配置（从`HaiLiDrv.config`读取）
- **K线图管理器**：使用全局单例`ChartWindowManager`，但K线图位置独立保存

### 集成模式
- **配置文件**：使用主程序的`App.config`
- **数据缓存**：共享主程序的`RealTimeDataCache`
- **数据库连接**：完全共享
- **K线图管理器**：完全共享

## 使用场景

### 场景1：独立运行HaiLiDrv
1. 启动程序，选择 **[2] HaiLiDrv数据窗口（独立模式）**
2. 窗口在附加屏上自动打开
3. 如果收盘后，自动显示日线数据
4. 如果交易时间且有实时数据，显示实时数据

### 场景2：主程序 + HaiLiDrv同时运行
1. 启动程序，选择 **[1] 主程序（计算器）**
2. 在主窗口中点击 **"数据窗口"** 按钮
3. HaiLiDrv窗口以集成模式打开，共享主程序的实时数据缓存

### 场景3：仅查看历史数据
1. 启动程序，选择 **[2] HaiLiDrv数据窗口（独立模式）**
2. 收盘后会自动显示最新交易日的日线数据
3. 无需启动MQ服务

### 场景4：只显示指定股票
1. 启动程序，选择 **[2] HaiLiDrv数据窗口（独立模式）**
2. 点击 **"配置股票代码"** 按钮
3. 输入要显示的股票代码（如：`600000,600001,000001`）
4. 勾选 **"启用股票代码过滤"**
5. 点击 **"保存"**，窗口会自动刷新，只显示配置的股票

## 注意事项

1. **数据服务**：独立模式下，如果需要在交易时间查看实时数据，需要确保主程序的数据服务已启动，或者HaiLiDrv能够连接到数据源。

2. **数据库连接**：独立模式需要配置数据库连接信息（在`HaiLiDrv.config`中），用于加载日线数据。

3. **K线图位置**：独立模式的K线图位置与主程序的K线图位置分开保存，互不影响。

4. **配置文件位置**：`HaiLiDrv.config`必须与exe文件在同一目录。

5. **数据刷新**：收盘后数据不会变化，但窗口仍会每3秒刷新一次（可以修改配置降低刷新频率）。

6. **股票代码过滤**：
   - 配置的股票代码会同时应用于实时数据和日线数据
   - 如果配置的股票代码在数据中不存在，不会显示
   - 建议定期检查配置的股票代码是否有效

## 技术实现

### 数据服务（HaiLiDrvDataService）
- 自动判断交易时间
- 交易时间：从`RealTimeDataCache`获取实时数据
- 收盘后：从数据库`stock_daily_data`表获取最新交易日数据
- 自动计算涨跌幅（日线数据使用前一日收盘价）

### 配置管理
- 独立模式：`HaiLiDrvConfigProvider` - 读取`HaiLiDrv.config`
- 集成模式：`AppConfigProvider` - 读取`App.config`
- 两者都实现`IConfigurationProvider`接口，保证API一致性

### 窗口管理
- 使用`ChartWindowManager`单例管理K线图窗口
- 确保同时只有一个K线图窗口打开
- 窗口位置根据模式分别保存

### 股票代码过滤
- 在`HaiLiDrvDataService`中实现过滤逻辑
- 支持实时数据和日线数据的统一过滤
- 配置保存在`HaiLiDrv.config`中
- 提供UI对话框（`HaiLiDrvStockCodeConfigDialog`）方便配置

## 故障排除

### 问题1：独立模式下没有数据
- **检查**：数据库配置是否正确（`HaiLiDrv.config`）
- **检查**：数据库连接是否正常
- **检查**：是否有日线数据（收盘后模式）

### 问题2：实时数据不更新
- **检查**：是否在交易时间
- **检查**：主程序的数据服务是否已启动
- **检查**：`RealTimeDataCache`中是否有数据

### 问题3：窗口不在附加屏显示
- **检查**：是否有第二个屏幕
- **检查**：配置文件中的窗口位置是否正确
- **解决**：手动移动窗口，关闭时会自动保存位置

### 问题4：配置了股票代码但没有显示
- **检查**：`HaiLiDrv_EnableStockCodeFilter` 是否为 `true`
- **检查**：股票代码格式是否正确（6位数字）
- **检查**：配置的股票代码在数据中是否存在
- **检查**：控制台日志中是否有过滤相关的提示信息
