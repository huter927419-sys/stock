# 性能问题分析报告

## 🔍 已发现的性能问题

### 1. ❌ **数据库查询：GetYesterdayAmountAndTurnoverRate 重复查询**

**位置**：`src/Filters/UnifiedStockFilter.cs:194`

**问题**：
- 每只股票都要调用 `GetYesterdayAmountAndTurnoverRate` 查询数据库
- 5000只股票 = 5000次数据库查询
- 即使有缓存，首次过滤仍然很慢

**当前代码**：
```csharp
// 表格统一条件：昨天成交金额>3亿，昨天换手率>3%
var (amount, turnoverRate) = _klineRepository.GetYesterdayAmountAndTurnoverRate(stockCode, yesterdayDate);
```

**优化方案**：
- 批量查询：一次性查询所有股票的成交金额和换手率
- 或使用缓存：缓存昨天的成交金额和换手率数据

### 2. ⚠️ **排序操作：双重排序**

**位置**：
- `src/Filters/UnifiedStockFilter.cs:136` - 过滤结果排序
- `src/UI/ViewModels/FilterMainViewModel.cs:1026` - UI更新时再次排序

**问题**：
- 过滤完成后已经按涨幅排序
- `UpdateTableResults` 中又进行了一次排序
- 重复排序浪费CPU

**当前代码**：
```csharp
// UnifiedStockFilter.cs
return results.OrderByDescending(r => r.PriceChangePercent ?? decimal.MinValue).ToList();

// FilterMainViewModel.cs
var filtered = sourceResults.Where(...)
    .OrderByDescending(r => r.PriceChangePercent ?? decimal.MinValue)
    .ToList();
```

**优化方案**：
- 移除 `UpdateTableResults` 中的排序（因为数据已经排序）
- 或者移除 `UnifiedStockFilter` 中的排序，统一在UI层排序

### 3. ⚠️ **UI更新：ObservableCollection.Clear() + Add()**

**位置**：`src/UI/ViewModels/FilterMainViewModel.cs:1018-1032`

**问题**：
- `targetResults.Clear()` 会触发UI重新渲染
- 然后逐个 `Add()` 又会触发多次UI更新
- 对于大量数据（如表格2有140只股票），会导致UI卡顿

**当前代码**：
```csharp
targetResults.Clear();
foreach (var result in filtered)
{
    targetResults.Add(CreateStockResultItem(result));
}
```

**优化方案**：
- 使用批量更新：先清空，然后一次性添加所有项
- 或使用 `CollectionViewSource` 进行更高效的更新

### 4. ⚠️ **实时数据更新：节流机制可能不够优化**

**位置**：`src/UI/ViewModels/FilterMainViewModel.cs:35-38`

**问题**：
- 节流间隔是100ms，可能对于高频更新仍然不够
- 使用 `HashSet` 和 `Timer` 可能产生额外的开销

**当前代码**：
```csharp
private const int THROTTLE_INTERVAL_MS = 100;
```

**优化方案**：
- 考虑使用 `Task.Delay` 和 `CancellationToken` 替代 `Timer`
- 或者增加节流间隔到200-300ms

### 5. ⚠️ **KD计算：可能重复计算**

**位置**：`src/Filters/UnifiedStockFilter.cs:154-175`

**问题**：
- 每只股票需要计算6次KD值（周/月/季 × 今天/昨天）
- 如果 `_batchKDCalculator` 为 null，每次都要通过 `ChartService` 计算
- `ChartService` 可能没有充分利用缓存

**优化建议**：
- 确保 `BatchKDCalculator` 被正确初始化
- 或者优化 `ChartService` 的KD计算缓存

### 6. ⚠️ **数据库连接：可能没有使用连接池**

**位置**：多处创建 `NpgsqlConnection`

**问题**：
- 每次查询都创建新连接可能效率低
- 应该使用连接池

**检查**：
- 需要确认 `NpgsqlConnection` 是否自动使用连接池（默认应该会）

## 📊 性能优化建议优先级

### 🔴 **高优先级（立即优化）**

1. **批量查询成交金额和换手率**
   - 影响：减少5000次数据库查询
   - 难度：中等
   - 收益：首次过滤速度提升50-80%

2. **移除重复排序**
   - 影响：减少CPU使用
   - 难度：简单
   - 收益：UI更新速度提升10-20%

### 🟡 **中优先级（中期优化）**

3. **优化UI批量更新**
   - 影响：减少UI卡顿
   - 难度：中等
   - 收益：UI响应速度提升30-50%

4. **优化实时数据节流**
   - 影响：减少UI更新频率
   - 难度：简单
   - 收益：UI流畅度提升

### 🟢 **低优先级（长期优化）**

5. **确保BatchKDCalculator被使用**
   - 影响：KD计算速度
   - 难度：简单（检查配置）
   - 收益：过滤速度提升（如果当前未使用）

## 🔧 快速修复建议

### 修复1：批量查询成交金额和换手率

在 `UnifiedStockFilter` 中添加批量查询方法：

```csharp
// 在 FilterParallel 方法开始处
var stockCodes = realTimeDataList.Select(d => d.StockCode).ToList();
var yesterdayAmountAndTurnoverRate = _klineRepository.GetYesterdayAmountAndTurnoverRateBatch(stockCodes, yesterdayDate);

// 在 ProcessStock 中
if (!yesterdayAmountAndTurnoverRate.TryGetValue(stockCode, out var amountAndRate))
    return null;
var (amount, turnoverRate) = amountAndRate;
```

### 修复2：移除重复排序

在 `UpdateTableResults` 中移除排序（因为数据已经排序）：

```csharp
var filtered = sourceResults.Where(r =>
    r.WeeklyK >= weeklyMin &&
    r.MonthlyK >= monthlyMin &&
    r.QuarterlyK >= quarterlyMin)
    // 移除排序，因为数据已经排序
    .ToList();
```

### 修复3：优化UI批量更新

使用临时列表，然后批量替换：

```csharp
var newItems = filtered.Select(r => CreateStockResultItem(r)).ToList();
targetResults.Clear();
foreach (var item in newItems)
{
    targetResults.Add(item);
}
```
