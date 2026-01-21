# 图表加载日志使用指南

## 📋 概述

为了帮助诊断图表显示问题，我添加了详细的日志输出，涵盖从数据加载到图表渲染的全过程。

## 🔍 日志内容

日志包含以下关键阶段：

### 1. 图表数据加载
- 股票代码和名称
- 日K线数据量
- 周/月/季KD数据量
- 数据日期范围
- 最新KD值

### 2. WebView初始化
- 用户数据文件夹路径
- CoreWebView2创建过程
- 页面导航状态

### 3. 嵌入式资源加载
- JS库资源名称
- 资源大小（KB）
- 可用资源列表（如果加载失败）

### 4. HTML生成
- HTML大小
- 是否使用内嵌JS或CDN

### 5. 设置图表数据
- JSON转换状态
- JSON大小
- JavaScript执行结果

### 6. JavaScript执行
- LightweightCharts库加载状态
- 图表初始化
- 数据加载结果

## 🛠️ 使用方法

### 方法1：使用批处理脚本（推荐）

```bash
# 运行程序并自动捕获日志
capture_chart_log.bat
```

步骤：
1. 运行脚本
2. 程序启动后，点击任意股票
3. 观察图表窗口
4. 关闭程序
5. 自动显示日志

日志保存在：`chart_log.txt`

### 方法2：手动运行并查看日志

```bash
# 启动程序并重定向输出
cd bin\Release
MQReceiver.exe > ..\..\chart_log.txt 2>&1

# 查看日志
type ..\..\chart_log.txt
```

### 方法3：使用PowerShell分析脚本

```powershell
# 分析日志文件（彩色输出，按类别分组）
.\view_chart_log.ps1
```

或指定日志文件：

```powershell
.\view_chart_log.ps1 -LogFile "my_custom_log.txt"
```

## 📊 日志示例

### 正常加载的日志：

```
═══════════════════════════════════════════════════════
[图表数据加载] 开始加载股票: 000001
[图表数据加载] 时间: 2026-01-19 20:30:15
[图表数据加载] ChartService 创建成功
[图表数据加载] LoadChartData 返回
[图表数据加载] ✅ 数据加载成功
  - 股票代码: 000001
  - 股票名称: 平安银行
  - 日K线数量: 5234
  - 周KD数量: 1046
  - 月KD数量: 242
  - 季KD数量: 81
  - 日K线日期范围: 2005-06-01 ~ 2026-01-19
  - 周KD日期范围: 2005-06-03 ~ 2026-01-17
  - 周KD最新值: K=85.32, D=78.45, 差值=6.87
[图表数据加载] ✅ 加载完成
═══════════════════════════════════════════════════════

───────────────────────────────────────────────────────
[WebView初始化] 开始初始化 WebView2
[WebView初始化] 用户数据文件夹: C:\Users\...\WebView2
[WebView初始化] 创建 CoreWebView2Environment...
[WebView初始化] ✅ Environment 创建成功
[WebView初始化] 确保 CoreWebView2 初始化...
[WebView初始化] ✅ CoreWebView2 初始化成功
[WebView初始化] 使用内嵌式资源加载图表，完全自包含，不依赖外部文件
[WebView初始化] 步骤1: 读取嵌入式JS库资源...
[资源加载] 尝试加载资源: MQReceiver.src.UI.WebChart.lib.lightweight-charts.js
[资源加载] 成功读取资源，大小: 589652 字节
[WebView初始化] ✅ 成功读取嵌入式JS库
[WebView初始化]    大小: 575.8 KB (589652 字节)
[WebView初始化] 步骤2: 生成完整HTML...
[WebView初始化] ✅ HTML生成成功，大小: 582.3 KB
[WebView初始化] 步骤3: 使用NavigateToString加载HTML...
[WebView初始化] ✅ NavigateToString 调用成功
[WebView初始化] 等待页面加载完成...
───────────────────────────────────────────────────────

[WebView初始化] ✅ 页面导航成功
[WebView初始化] 准备设置图表数据...

───────────────────────────────────────────────────────
[设置图表数据] 开始设置图表数据
[设置图表数据] ✅ 前置条件检查通过
[设置图表数据] 步骤1: 转换为JSON...
[设置图表数据] ✅ JSON转换成功，大小: 234.5 KB
[设置图表数据] 步骤2: 执行JavaScript脚本...
[设置图表数据] ✅ JavaScript执行完成
[设置图表数据] 执行结果: "success"
[设置图表数据] ✅✅✅ 图表数据设置成功！
───────────────────────────────────────────────────────

[Chart] LightweightCharts: OK
[Chart] Init OK
[KD计算] K数据量: 1046 | D数据量: 1046
[KD计算] 差值结果量: 1046
[KD计算] 差值范围: 最小=-15.23, 最大=18.67
[Chart] Data loaded
```

## 🚨 错误诊断

### 问题1：资源加载失败

```
[资源加载] 尝试加载资源: MQReceiver.src.UI.WebChart.lib.lightweight-charts.js
[资源加载] 可用的嵌入式资源:
  - MQReceiver.Properties.Resources.resources
  - ...
```

**原因**：JS库未正确编译为嵌入式资源

**解决**：检查 `MQReceiver.csproj` 中的 `<EmbeddedResource>` 配置

### 问题2：JSON转换失败

```
[设置图表数据] ❌ 设置失败: Object reference not set to an instance of an object
```

**原因**：图表数据为null或某个字段为null

**解决**：检查数据加载阶段的日志，确认数据量

### 问题3：JavaScript执行失败

```
[Chart] LightweightCharts: FAIL
```

**原因**：LightweightCharts库未加载

**解决**：检查嵌入式资源是否正确加载，或使用CDN后备方案

### 问题4：KD数据为空

```
[图表数据加载] ✅ 数据加载成功
  - 日K线数量: 5234
  - 周KD数量: 0
  - 月KD数量: 0
  - 季KD数量: 0
```

**原因**：KD计算失败或数据库中没有除权数据

**解决**：检查 `ChartService.LoadChartData` 和 `KDCalculator` 的日志

## 📈 关键指标

正常情况下应该看到：

- ✅ 数据加载成功
- ✅ WebView初始化成功
- ✅ 资源加载成功（JS库大小约575KB）
- ✅ HTML生成成功（大小约580KB）
- ✅ JavaScript执行成功
- ✅ LightweightCharts库加载（OK）
- ✅ 图表初始化完成
- ✅ 数据加载完成

## 🎯 下一步

运行 `capture_chart_log.bat` 并将生成的 `chart_log.txt` 提供给开发人员分析。

或使用 `view_chart_log.ps1` 快速查看关键信息。
