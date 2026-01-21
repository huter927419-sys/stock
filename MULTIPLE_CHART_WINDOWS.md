# 多图表窗口支持说明

## ✅ 功能确认

您的程序**已经支持同时打开多个图表窗口**！

## 📋 使用方法

### 1. 打开多个图表

在主窗口或筛选结果窗口中：
1. 双击第一个股票 → 打开图表窗口1
2. 不要关闭图表窗口1
3. 双击第二个股票 → 打开图表窗口2
4. 可以继续打开更多...

### 2. 窗口标题显示

每个图表窗口的标题会显示：
```
股票图表 - 平安银行 (000001)
股票图表 - 万科A (000002)
股票图表 - 贵州茅台 (600519)
```

这样可以轻松区分不同的窗口。

## 🎯 测试步骤

1. **重新编译运行程序**
2. **打开第一个股票** (如 000001)
3. **不关闭图表窗口**，返回主窗口
4. **打开第二个股票** (如 000002)
5. **确认两个窗口都能独立显示**

## 🔍 如果仍然只能打开一个窗口

### 可能的原因：

#### 原因1：WebView2初始化限制

WebView2 可能在同一进程中有一些限制。

**解决方案**：检查 WebView2 初始化代码

#### 原因2：窗口被自动关闭

打开新窗口时，旧窗口可能被自动关闭。

**检查方法**：
在 `WebChartWindow.xaml.cs` 中查找是否有：
- `Window.Close()` 的自动调用
- 单例模式的实现
- 静态变量保存窗口引用

#### 原因3：显示模式问题

窗口可能使用了 `ShowDialog()` 而不是 `Show()`。

**当前代码**：
```csharp
var chartWindow = new WebChartWindow(stockCode, _sharedCache);
chartWindow.Show();  // ✅ 正确：使用 Show()，允许多窗口
```

如果是 `ShowDialog()`，会变成模态窗口，必须关闭才能继续操作。

## 🐛 调试方法

### 在程序控制台查看输出

已添加调试输出，每次打开窗口会显示：
```
[图表窗口] 已打开股票 000001 的图表窗口
[图表窗口] 已打开股票 000002 的图表窗口
```

如果看到两次输出，说明代码确实创建了两个窗口。

### 检查任务栏

Windows 任务栏应该显示多个程序窗口：
```
主窗口
图表窗口 - 000001
图表窗口 - 000002
图表窗口 - 600519
```

## 🔧 增强功能建议

如果需要更好的多窗口管理，可以添加：

### 1. 窗口管理器

```csharp
public static class ChartWindowManager
{
    private static Dictionary<string, WebChartWindow> _windows = 
        new Dictionary<string, WebChartWindow>();
    
    public static void OpenOrActivate(string stockCode, RealTimeDataCache cache = null)
    {
        // 如果窗口已存在，激活它
        if (_windows.ContainsKey(stockCode))
        {
            var existingWindow = _windows[stockCode];
            if (existingWindow.IsLoaded && !existingWindow.IsVisible)
            {
                existingWindow.Show();
            }
            existingWindow.Activate();
            existingWindow.Focus();
            return;
        }
        
        // 创建新窗口
        var window = new WebChartWindow(stockCode, cache);
        _windows[stockCode] = window;
        
        // 窗口关闭时从字典中移除
        window.Closed += (s, e) => _windows.Remove(stockCode);
        
        window.Show();
    }
}
```

### 2. 防止重复打开同一股票

```csharp
private void OpenStockChart(string stockCode)
{
    // 使用窗口管理器
    ChartWindowManager.OpenOrActivate(stockCode, _sharedCache);
}
```

这样：
- ✅ 同一股票只打开一个窗口
- ✅ 重复点击会激活已有窗口而不是创建新窗口
- ✅ 不同股票可以同时打开多个窗口

### 3. 窗口列表管理

添加一个菜单来显示所有打开的图表窗口：
```
窗口 (W)
  ├─ 000001 - 平安银行
  ├─ 000002 - 万科A
  └─ 600519 - 贵州茅台
```

点击菜单项可以快速切换到对应窗口。

## 📊 性能考虑

### 建议的窗口数量限制

- **推荐**: 同时打开 3-5 个图表窗口
- **最大**: 不超过 10 个图表窗口

### 原因：

1. **内存占用**: 每个窗口会缓存大量K线和KD数据
2. **WebView2资源**: 每个WebView2实例都需要一定资源
3. **渲染性能**: 多个图表同时渲染会影响性能

### 优化建议：

如果需要对比多个股票，可以考虑：
- 实现一个"多股对比"视图，在同一个窗口显示多个股票
- 使用标签页（Tab）方式管理多个图表
- 实现窗口最小化/恢复功能

## 📝 当前状态

✅ **已支持**: 同时打开多个图表窗口  
✅ **已修复**: WebView2库本地化，解决跟踪保护问题  
⏸️ **可选**: 窗口管理器（如需要可以实现）

## 测试结果

请运行程序并测试：
1. 打开 000001 的图表
2. 不关闭，返回主窗口
3. 打开 000002 的图表
4. 查看是否两个窗口都存在

如果只能看到一个窗口，请告诉我具体现象：
- 第一个窗口自动关闭了？
- 第二个窗口没有打开？
- 其他情况？

---

**文档创建时间**: 2026-01-19  
**功能状态**: 已支持多窗口
