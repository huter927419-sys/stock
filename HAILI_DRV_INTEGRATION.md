# HaiLiDrvDemo 集成说明

## 概述

已成功将 `F:\dsfr\C212\HaiLiDrvDemo` 的功能集成到当前项目中，实现了以下特性：

1. **WPF版本的海利驱动数据窗口**：替代原有的Windows Forms版本
2. **多屏支持**：HaiLiDrv窗口自动显示在附加屏（第二个屏幕）上
3. **统一数据源**：从内存缓存（`RealTimeDataCache`）获取数据，不再依赖DLL调用
4. **单例K线图管理**：确保同时只有一个K线图窗口打开
5. **共享资源**：HaiLiDrv窗口和主窗口共享同一个K线图窗口实例

## 架构设计

### 1. ChartWindowManager（单例K线图管理器）

**文件**: `src/UI/Services/ChartWindowManager.cs`

- **功能**：管理K线图窗口的生命周期，确保同时只有一个窗口打开
- **方法**：
  - `OpenChartWindow(string stockCode, Screen targetScreen = null)` - 打开股票K线图
  - `CloseChartWindow()` - 关闭当前K线图窗口
  - `SetRealTimeCache(RealTimeDataCache cache)` - 设置实时数据缓存

### 2. HaiLiDrvWindow（海利驱动数据窗口）

**文件**: 
- `src/UI/Views/HaiLiDrvWindow.xaml` - XAML界面定义
- `src/UI/Views/HaiLiDrvWindow.xaml.cs` - 代码逻辑

**功能特性**：
- **数据源自动切换**：交易时间使用实时数据，收盘后使用日线数据
- 自动每3秒刷新一次数据（可在配置文件中修改）
- 显示股票代码、名称、最新价、涨跌幅、成交量、成交额等信息
- 支持双击股票行打开K线图（唯一方式）
- 自动在附加屏（第二个屏幕）上显示并最大化
- 支持独立模式和集成模式
- **股票代码过滤**：可配置要显示的股票代码列表（独立模式）

**数据来源**：
- **交易时间**：从 `RealTimeDataCache.GetAllData()` 获取实时数据
- **收盘后**：从数据库 `stock_daily_data` 表获取最新交易日数据
- 按成交额排序，显示前500条记录（可在配置文件中修改）
- 数据格式自动转换为 `HaiLiDataItem` 用于显示

### 3. HaiLiDrvDataService（数据服务）

**文件**: `src/UI/Services/HaiLiDrvDataService.cs`

- **功能**：根据交易时间自动选择数据源
- **方法**：
  - `GetAllStockData(int maxCount)` - 获取所有股票数据（自动选择实时或日线）
  - `IsTradingTime()` - 判断当前是否为交易时间
  - `GetRealTimeData(int maxCount)` - 获取实时数据
  - `GetDailyData(int maxCount)` - 获取日线数据

**数据转换**：
- **实时数据**：`RealTimeDataRecord` → `HaiLiDataItem`
- **日线数据**：`DailyDataRecord` → `HaiLiDataItem`
- 自动计算涨跌幅：实时数据使用`(NewPrice - LastClose) / LastClose * 100`，日线数据使用前一日收盘价计算
- 格式化显示：时间、价格、成交量、成交额等

### 4. StartupMenuWindow（可视化启动菜单）

**文件**: 
- `src/UI/Views/StartupMenuWindow.xaml` - XAML界面定义
- `src/UI/Views/StartupMenuWindow.xaml.cs` - 代码逻辑

- **功能**：提供可视化的启动菜单，让用户选择启动的功能
- **特性**：
  - 支持鼠标点击和键盘快捷键
  - 美观的深色主题界面
  - 自动显示（无控制台时）

### 5. HaiLiDrvConfigProvider（独立配置提供者）

**文件**: `src/Core/Configuration/HaiLiDrvConfigProvider.cs`

- **功能**：为独立模式提供独立的配置文件管理
- **特性**：
  - 使用独立的`HaiLiDrv.config`文件
  - 首次运行自动创建默认配置
  - 与主程序配置完全分离
  - 实现`IConfigurationProvider`接口，保证API一致性

## 使用方法

### 启动方式

#### 方式1：可视化启动菜单（推荐）

程序启动时会自动显示可视化启动菜单，包含以下选项：
- **[1] 主程序（计算器）** - 启动完整的主程序界面
- **[2] HaiLiDrv数据窗口（独立模式）** - 启动独立的HaiLiDrv窗口
- **[3] MQ数据接收服务（控制台）** - 仅启动数据接收服务
- **[0] 退出** - 退出程序

**操作方式**：
- 鼠标点击选项
- 或使用键盘数字键（1、2、3、0）快速选择

#### 方式2：从主窗口启动（集成模式）

1. 在主窗口（`FilterMainWindow`）的标题栏中，点击 **"数据窗口"** 按钮
2. HaiLiDrv窗口将在附加屏（第二个屏幕）上自动打开并最大化
3. 如果只有一个屏幕，窗口会在主屏的右下角显示

### 数据源自动切换

HaiLiDrv窗口会根据当前时间自动选择数据源：

- **交易时间**（9:30-11:30, 13:00-15:00，工作日）：
  - 使用实时数据（从`RealTimeDataCache`获取）
  - 显示实时行情数据，每3秒自动刷新

- **收盘后**（非交易时间）：
  - 使用日线数据（从数据库`stock_daily_data`表获取最新交易日数据）
  - 显示历史收盘数据，每3秒自动刷新（但数据不会变化）

### 查看股票数据

- HaiLiDrv窗口会自动每3秒刷新一次数据
- 数据按成交额从高到低排序
- 可以点击 **"刷新数据"** 按钮手动刷新

### 打开K线图

**唯一方式**：双击HaiLiDrv窗口中的股票行

**注意**：由于单例模式，新打开的K线图会关闭之前打开的K线图窗口。

### 关闭HaiLiDrv窗口

再次点击主窗口的 **"数据窗口"** 按钮（按钮文本会变为"关闭数据窗口"）

## 技术实现细节

### 多屏支持

```csharp
// 自动检测屏幕数量
var screens = Screen.AllScreens;
if (screens.Length > 1)
{
    // 使用第二个屏幕（索引1）
    var secondaryScreen = screens[1];
    // 设置窗口位置和大小
}
```

### 单例K线图管理

```csharp
// 使用ChartWindowManager确保单例
var chartManager = ChartWindowManager.Instance;
chartManager.SetRealTimeCache(_sharedCache);
chartManager.OpenChartWindow(stockCode);
```

### 数据获取（自动切换）

```csharp
// 使用HaiLiDrvDataService自动选择数据源
var dataService = new HaiLiDrvDataService(_realTimeCache);
var items = dataService.GetAllStockData(maxCount);
// 交易时间：从RealTimeDataCache获取实时数据
// 收盘后：从数据库获取日线数据
```

## 与原HaiLiDrvDemo的对比

| 特性 | 原HaiLiDrvDemo | 集成后 |
|------|---------------|--------|
| UI框架 | Windows Forms | WPF |
| 数据来源 | DLL调用（StockDrv.dll） | 内存缓存（RealTimeDataCache） |
| 多屏支持 | 不支持 | 自动在附加屏显示 |
| K线图管理 | 独立窗口 | 单例模式，共享资源 |
| 数据刷新 | 手动请求 | 自动每3秒刷新 |

## 配置说明

### 独立模式配置文件

独立模式使用 `HaiLiDrv.config` 文件（与exe同目录），包含以下配置：

- **窗口位置**：`HaiLiDrvWindow_Left`, `HaiLiDrvWindow_Top`, `HaiLiDrvWindow_Width`, `HaiLiDrvWindow_Height`
- **数据刷新间隔**：`HaiLiDrv_RefreshIntervalSeconds`（默认3秒）
- **显示数据条数**：`HaiLiDrv_MaxDisplayCount`（默认500条）
- **数据库配置**：`DatabaseHost`, `DatabasePort`, `DatabaseName`, `DatabaseUser`, `DatabasePassword`
- **K线图窗口位置**：`HaiLiDrv_ChartWindow_Left`, `HaiLiDrv_ChartWindow_Top`, `HaiLiDrv_ChartWindow_Width`, `HaiLiDrv_ChartWindow_Height`

### 集成模式配置

集成模式使用主程序的 `App.config`，窗口位置保存在：
- `HaiLiDrvWindow_Left`
- `HaiLiDrvWindow_Top`
- `HaiLiDrvWindow_Width`
- `HaiLiDrvWindow_Height`

### 刷新间隔

默认每3秒自动刷新一次数据，可在 `HaiLiDrvWindow.xaml.cs` 中修改：
```csharp
_refreshTimer.Interval = TimeSpan.FromSeconds(3); // 修改此值
```

## 注意事项

1. **数据源自动切换**：
   - 交易时间：需要数据服务（MQ服务）已启动，才能显示实时数据
   - 收盘后：自动从数据库加载日线数据，无需启动数据服务
2. **单例K线图**：同时只能打开一个K线图窗口，新窗口会关闭旧窗口
3. **多屏检测**：如果没有第二个屏幕，窗口会在主屏显示
4. **数据量限制**：默认显示成交额前500的股票，避免界面卡顿
5. **独立配置文件**：独立模式首次运行会自动创建`HaiLiDrv.config`文件
6. **资源复用**：独立模式共享数据库连接配置，但使用独立的缓存实例和窗口位置配置

## 文件清单

新增文件：
- `src/UI/Services/ChartWindowManager.cs` - K线图管理器（单例）
- `src/UI/Services/HaiLiDrvDataService.cs` - HaiLiDrv数据服务（自动切换实时/日线数据）
- `src/UI/Views/HaiLiDrvWindow.xaml` - HaiLiDrv窗口XAML
- `src/UI/Views/HaiLiDrvWindow.xaml.cs` - HaiLiDrv窗口逻辑
- `src/UI/Views/StartupMenuWindow.xaml` - 可视化启动菜单XAML
- `src/UI/Views/StartupMenuWindow.xaml.cs` - 可视化启动菜单逻辑
- `src/Core/Configuration/HaiLiDrvConfigProvider.cs` - 独立配置提供者

修改文件：
- `src/UI/Views/FilterMainWindow.xaml` - 添加"数据窗口"按钮
- `src/UI/Views/FilterMainWindow.xaml.cs` - 使用ChartWindowManager，添加HaiLiDrv窗口控制
- `src/UI/Views/FilterResultWindow.xaml.cs` - 使用ChartWindowManager
- `src/UI/Views/App.xaml.cs` - 添加启动菜单显示逻辑
- `Program.cs` - 添加HaiLiDrv独立启动选项
- `MQReceiver.csproj` - 添加新文件到项目

## 后续优化建议

1. **数据过滤**：添加按股票代码、名称搜索功能
2. **排序选项**：支持按涨幅、成交量、成交额等多种排序方式
3. **数据导出**：支持将数据导出为CSV或Excel
4. **实时更新优化**：使用事件驱动而非定时刷新，提高性能
5. **K线图位置记忆**：为HaiLiDrv窗口打开的K线图单独保存位置
