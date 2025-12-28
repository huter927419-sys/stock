# MQReceiver - 股票数据管理系统

基于 WPF (.NET Framework 4.8) 的股票数据管理和分析系统，支持实时数据同步、KD指标计算、股票筛选和图表展示。

## 项目结构

```
MQReceiver/
├── Views/                      # 视图层（WPF窗口）
│   ├── App.xaml(.cs)          # 应用程序入口
│   ├── MainWindow.xaml(.cs)   # 主窗口
│   ├── FilterMainWindow.xaml(.cs)    # 过滤结果主面板
│   ├── FilterResultWindow.xaml(.cs)  # 过滤结果窗口
│   └── StockChartWindow.xaml(.cs)    # 股票图表窗口
│
├── ViewModels/                 # 视图模型层
│   ├── FilterMainViewModel.cs  # 过滤主面板视图模型
│   ├── FilterResultViewModel.cs # 过滤结果视图模型
│   └── StockChartViewModel.cs  # 股票图表视图模型
│
├── Models/                     # 数据模型
│   ├── DataModels.cs          # 基础数据模型
│   ├── StockDataModels.cs     # 股票数据模型
│   ├── ChartDataModels.cs     # 图表数据模型
│   ├── KDModels.cs            # KD指标模型
│   ├── FilterResultModels.cs  # 过滤结果模型
│   └── PerformanceModels.cs   # 性能监控模型
│
├── Services/                   # 服务层
│   ├── MQService.cs           # 消息队列服务
│   ├── FilterService.cs       # 过滤服务
│   ├── ChartService.cs        # 图表数据服务
│   └── DataPreloadService.cs  # 数据预加载服务
│
├── Repositories/               # 数据访问层
│   ├── IKlineDataRepository.cs / PostgresKlineDataRepository.cs
│   ├── IStockDataRepository.cs / PostgresStockDataRepository.cs
│   ├── IRealTimeDataRepository.cs / PostgresRealTimeDataRepository.cs
│   ├── IExRightsDataRepository.cs / PostgresExRightsDataRepository.cs
│   ├── DailyDataDBWriter.cs   # 日线数据写入
│   ├── RealTimeDataDBWriter.cs # 实时数据写入
│   └── ExRightsDataDBWriter.cs # 除权数据写入
│
├── Filters/                    # 股票过滤器
│   ├── IStockFilter.cs        # 过滤器接口
│   ├── BaseStockFilter.cs     # 基础过滤器
│   ├── GoldenCrossFilter.cs   # 金叉过滤器
│   ├── KValueOrderFilter.cs   # K值排序过滤器
│   ├── KValueRelationFilter.cs # K值关系过滤器
│   ├── StockFilterOrchestrator.cs # 过滤器编排器
│   ├── FilterModels.cs        # 过滤模型
│   ├── FilterConditionBuilder.cs # 条件构建器
│   └── DataValidator.cs       # 数据验证器
│
├── Calculators/                # 计算器
│   ├── KDCalculator.cs        # KD指标计算器
│   └── ExRightsAdjustmentCalculator.cs # 除权调整计算器
│
├── Helpers/                    # 辅助工具
│   ├── JsonParserHelper.cs    # JSON解析
│   ├── StockDataParser.cs     # 股票数据解析
│   ├── FilterDisplayHelper.cs # 过滤显示辅助
│   ├── PerformanceAnalyzer.cs # 性能分析器
│   ├── DatabaseConnectionHelper.cs # 数据库连接辅助
│   └── RedisHelper.cs         # Redis辅助
│
├── Cache/                      # 缓存
│   └── RealTimeDataCache.cs   # 实时数据缓存
│
├── Configuration/              # 配置
│   ├── IConfigurationProvider.cs
│   └── AppConfigProvider.cs
│
├── Events/                     # 事件
│   └── FilterResultEventArgs.cs
│
├── Properties/                 # 程序集属性
│   └── AssemblyInfo.cs
│
└── Program.cs                  # 程序入口点
```

## 功能模块

### 1. 数据同步
- 通过消息队列接收实时股票数据
- 支持日线、周线、月线、季线数据
- 除权除息数据自动调整

### 2. KD指标计算
- 支持周K、月K、季K计算
- 金叉/死叉判断
- 历史数据对比

### 3. 股票筛选
三种筛选条件：
- **条件1**：周K、月K、季K 全部金叉
- **条件2**：周K > 月K > 季K（不含金叉）
- **条件3**：月K > 季K 或 周K < 月K

### 4. 图表展示
- 日K线图（红涨绿跌，符合中国股市习惯）
- 成交量图
- KD指标K值叠加图
- KD指标带状图

## 技术栈

- **框架**：.NET Framework 4.8 / WPF
- **数据库**：PostgreSQL (Npgsql 8.0.8)
- **缓存**：Redis (StackExchange.Redis)
- **图表**：LiveCharts 0.9.7
- **JSON**：Newtonsoft.Json

## 配置说明

在 `App.config` 中配置：

```xml
<appSettings>
  <!-- 数据库连接 -->
  <add key="PostgresConnectionString" value="Host=localhost;Database=stock;Username=xxx;Password=xxx"/>

  <!-- Redis连接 -->
  <add key="RedisConnectionString" value="localhost:6379"/>

  <!-- 过滤条件默认值 -->
  <add key="Filter1_WeeklyKDefaultMin" value="0"/>
  <add key="Filter1_MonthlyKDefaultMin" value="0"/>
  <add key="Filter1_QuarterlyKDefaultMin" value="0"/>
  <!-- ... -->
</appSettings>
```

## 启动方式

运行 `MQReceiver.exe` 后显示菜单：

```
=== 股票数据管理系统 v2.1 ===
[1] MQ数据同步服务
[2] 数据预加载服务
[3] KD过滤服务
[0] 退出
```

## 编译

使用 Visual Studio 2022 或 MSBuild：

```bash
MSBuild MQReceiver.csproj /t:Build /p:Configuration=Debug
```

## 依赖包

通过 NuGet 还原：
- Great.LiveCharts 2.0.3
- Npgsql 8.0.8
- StackExchange.Redis 2.6.122
- Newtonsoft.Json 13.0.3
- System.Text.Json 8.0.5
