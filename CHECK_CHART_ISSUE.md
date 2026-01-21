# 图表显示问题排查

## 🔍 请提供以下信息

### 1. 程序控制台输出
查看程序运行时的控制台，应该看到：
```
[WebView2] HTML路径: ...
[WebView2] HTML目录: ...
[WebView2] 文件存在: ...
[WebView2] lib目录: ...
[WebView2] JS文件: ...
[WebView2] 虚拟主机名映射: ...
[WebView2] 加载: https://appassets.local/stock-chart.html
[WebView2] 页面加载成功
```

**请截图或复制这部分输出！**

### 2. 浏览器控制台（F12）
在图表窗口按F12，查看Console标签，应该看到：
- 有红色错误？
- 有 `LightweightCharts is not defined` 错误？
- 有其他错误？

**请截图或复制错误信息！**

### 3. 图表窗口显示
- 完全空白（黑色背景）？
- 有标题和图例但没有数据？
- 有加载提示？

---

## 🔧 快速修复步骤

### 步骤1：重新编译（必须！）
```
1. Visual Studio → 清理解决方案 (Clean Solution)
2. 重新生成 (Rebuild Solution)
3. 运行程序
```

**为什么要重新编译？**
因为我们修改了HTML文件（内嵌了JS），需要重新复制到输出目录。

### 步骤2：检查文件
运行程序后，检查这个文件是否存在并且很大（约1.6MB）：
```
F:\dsfr\mqq\bin\Release\src\UI\WebChart\stock-chart.html
```

如果文件很小（几十KB），说明没有内嵌成功。

### 步骤3：查看控制台
打开图表后，立即查看：
1. 程序控制台（黑色窗口）
2. 浏览器控制台（F12）

---

## 📋 可能的原因

### 原因1：HTML文件没有更新
**症状**：
- 还是显示 `Failed to load resource: net::ERR_FILE_NOT_FOUND`
- 错误指向 `lightweight-charts.js:1`

**解决**：
必须重新编译（Rebuild Solution）

### 原因2：WebView2初始化失败
**症状**：
- 控制台没有 `[WebView2]` 开头的输出
- 或显示 `[WebView2] 页面加载失败`

**解决**：
检查WebView2运行时是否正确安装

### 原因3：数据为空
**症状**：
- 页面加载成功
- 但控制台显示 `K线数据量: 0 条`

**解决**：
检查数据库连接和股票代码

### 原因4：JavaScript执行错误
**症状**：
- 浏览器Console有红色错误
- 例如 `Uncaught TypeError: ...`

**解决**：
提供具体错误信息，我来修复

---

## 🎯 临时测试方案

如果内嵌版本还是不行，我们可以尝试：

### 方案A：使用CDN（需要网络）
临时改回使用在线CDN，看看是否是JS库的问题

### 方案B：简化HTML
创建一个最简单的测试页面，只显示一个基本图表

### 方案C：检查WebView2版本
可能需要更新WebView2运行时

---

## 📞 下一步

请先：
1. **Rebuild Solution（重新生成解决方案）**
2. **运行程序**
3. **打开图表**
4. **截图或复制**：
   - 程序控制台输出（特别是 `[WebView2]` 部分）
   - 浏览器Console（F12）的错误
   - 图表窗口的显示状态

然后告诉我结果，我会根据具体情况修复！
