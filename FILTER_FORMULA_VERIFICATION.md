# 过滤公式与取数验证报告

## 一、公式与代码对照

### 全域变量（与用户约定一致）

| 公式 | 含义 | 代码 |
|------|------|------|
| A1 | MAX(K2,K3) | `a1 = Math.Max(k2, k3)` ✓ |
| A2 | MIN(K2,K3) | `a2 = Math.Min(k2, k3)` ✓ |
| A3 | K1>D1 AND K2>D2 AND K3>D3 | `a3 = k1 > d1 && k2 > d2 && k3 > d3` ✓ |
| A4 | K2>D2 AND K3>D3 | `a4 = k2 > d2 && k3 > d3` ✓ |
| A5 | K1>D1 | `a5 = k1 > d1` ✓ |

### 统一条件

- **昨天成交金额 >= N×1亿**  
- 代码：`amountAndRate.amount >= condition.N * 100_000_000m`，且 `yesterdayDate = GetYesterdayDate(targetDate)` 查 `stock_daily_data.amount` ✓  

### 六个条件

| 条件 | 公式 | 代码实现 | 结论 |
|------|------|----------|------|
| 1 强多排列 | K1>A1 AND A2>=M1 AND A3 | `k1 > a1 && a2 >= M1 && a3` | ✓ |
| 2 中多排列 | K1>A1 AND A2>=M2 AND A2<M1 AND A3 | `k1 > a1 && a2 >= M2 && a2 < M1 && a3` | ✓ |
| 3 强多缠绕 | K1>M3 AND K1<A1 AND A2>=M1 AND A4 | `k1 > M3 && k1 < a1 && a2 >= M1 && a4` | ✓ |
| 4 中多缠绕 | K1>M3 AND K1<A1 AND A2>=M2 AND A2<M1 AND A4 | `k1 > M3 && k1 < a1 && a2 >= M2 && a2 < M1 && a4` | ✓ |
| 5 强多反弹 | K1<A2 AND A2>=M1 AND K1<M3 | `k1 < a2 && a2 >= M1 && k1 < M3` | ✓ |
| 6 中多反弹 | K1<A2 AND A2>=M2 AND A2<M1 AND K1<M4 | `k1 < a2 && a2 >= M2 && a2 < M1 && k1 < M4` | ✓ |

**结论：公式与实现一一对应，无误。**

---

## 二、取数逻辑

### 1. K1,K2,K3 与 D1,D2,D3

| 符号 | 含义 | 数据来源 |
|------|------|----------|
| K1 | 周K | `weeklyKD.K` |
| K2 | 月K | `monthlyKD.K` |
| K3 | 季K | `quarterlyKD.K` |
| D1 | 周D | `weeklyKD.D` |
| D2 | 月D | `monthlyKD.D` |
| D3 | 季D | `quarterlyKD.D` |

- 调用：`GetKDValue(stockCode, targetDate, "week"|"month"|"quarter")` 或 `BatchKDCalculator.GetKD(..., targetDate, ...)`，最终都走 `ChartService.GetKDValue`。
- `GetKDValue`：在 `kdData` 中取 **Date ≤ targetDate** 的**最后一个交易日**的 K、D；K、D 来自同一 `KDDataPoint`，匹配 ✓。

### 2. targetDate 的含义

- 来源：`FilterService` 使用 `dataStatus.RecommendedTargetDate`（由 `DataBoundaryManager.GetCurrentDataStatus` 给出），一般为**当前交易日**或**数据库最新交易日**。
- K、D 的含义：**targetDate 当天**在日线序列上对应的周/月/季 KD 值（详见下）。

### 3. 周/月/季 KD 的計算方式（ChartService）

1. **按周期聚合**  
   - 周：`GetCycleKey` 以**该周周一**为键，整周日线聚成一根周期 K 线。  
   - 月、季：按 `yyyyMM`、`Q{年}{季}` 聚合。

2. **在周期 K 线上算 KD**  
   - 在聚合后的周/月/季 K 线上用 9 周期 RSV + M1=3、M2=3 的 SMA 得到每个**完整周期**的 K、D。

3. **日线对齐与插值**  
   - 对**每个交易日**生成一个 `KDDataPoint`：  
     - 若该日属于某完整周期，则在**周期内**做线性插值：  
       - 周期**最后一天** = 该周期在周期 K 线上算出的真实 K、D；  
       - 周期**中间几天** = 上一周期 K、D → 当前周期 K、D 的线性插值。  
   - 因此：**同一周期内，只有周期最后一个交易日是“真实”周期 KD；其余为插值。**

4. **对公式的含义**  
   - `targetDate` 取到的是：**targetDate 所在日**在日线序列上的 K、D。  
   - 若 targetDate 恰为某周/月/季的**最后一个交易日**，则 K、D 为该周期已收盘的**真实**周/月/季 KD。  
   - 若 targetDate 在周期**中间**，则 K、D 为**插值**，表示“从上一周期过渡到本周期”的中间状态。  
   - 若本周期尚未收盘（例如本周三），则本周期在聚合时只包含到 targetDate 为止的日线，对应的是**未完成周期**的估算 KD + 插值。

**若你希望公式严格只用「已收盘周期」的 KD**：  
需要改取数，例如：取 targetDate 所在周期的**最后一个交易日**的 K、D；若 targetDate 早于该日，则用**上一周期**最后一天的 K、D。当前实现未做该约束。

### 4. 成交金额（昨天）

- `yesterdayDate = GetYesterdayDate(targetDate)`：targetDate 往前推，遇到周末再往前，得到**上一交易日**。  
- `GetYesterdayAmountAndTurnoverRateBatch(validStockCodes, yesterdayDate)` 查 `stock_daily_data` 的 `trade_date = yesterdayDate` 的 `amount`。  
- 与「昨天成交金额 >= N×1亿」一致 ✓。

### 5. 昨日 KD（Yesterday*）

- 公式**未使用**昨日 K、D。  
- 昨日 KD 仅用于 `FilterResultWithHistory` 的 `YesterdayWeeklyK/YesterdayMonthlyK/YesterdayQuarterlyK` 展示。  
- 当前逻辑：若**今日或昨日**的周/月/季 KD 任一个为 null，则 `ProcessStock` 直接 `return null`。  
- 因此：**昨日 KD 为 null（例如新上市、停牌导致无昨日数据）会被整只股票过滤掉**。若希望公式不依赖昨日，可改为：昨日 KD 为 null 时仅不填 Yesterday*，不因此过滤。

---

## 三、可能的问题与建议

1. **周/月/季未收盘**  
   - 现象：targetDate 在周期中间或本周期未结束时，K、D 为插值或未完成周期的估算。  
   - 建议：若业务要求只用已收盘周期，需在 `GetKDValue` 或调用链中改为「取 targetDate 所在周期的最后一个交易日，或更早的完整周期」的 K、D。

2. **昨日 KD 为 null 即整只过滤**  
   - 现象：新上市、停牌等导致昨日无 KD 时，整只股票不进入 1–6。  
   - 建议：若公式不依赖昨日，可放宽：昨日 KD 为 null 时仍做条件判断，仅不填 Yesterday*。

3. **日线数据范围**  
   - `LoadDailyKlineDataReal(stockCode, 0)` 会加载全部日线（且若存在实时/当日合并逻辑，会包含到当时为止的当日）。  
   - 若库中某股缺最近几天日线，或 `targetDate` 早于该股上市日，`GetKDValue` 可能返回 null，导致该股被过滤，属于合理行为。

4. **D 与 K 同源**  
   - D1、D2、D3 与 K1、K2、K3 来自同一 `KDDataPoint`，不存在 K、D 错配 ✓。

---

## 四、如何自检取数（单股）

可用下面方式抽查一只股票在某个 `targetDate` 下的取数是否合理：

1. 在 `ProcessStock` 中对该 `stockCode`（或通过 App.config 配置一个调试码）做一次判断，若匹配则：
   - 计算 `a1=Max(K2,K3)`, `a2=Min(K2,K3)`, `a3=(K1>D1且K2>D2且K3>D3)`, `a4=(K2>D2且K3>D3)`, `a5=(K1>D1)`；
   - 打印：`K1,K2,K3,D1,D2,D3,a1,a2,A3,A4,A5` 以及 1–6 的 true/false、`yesterdayDate`、`amount`、`minAmount`。
2. 与你在图表或 Excel 中按同样规则计算的 K、D 及 A1–A5、六条件对比，即可验证取数与公式。

如需要，可以在 `UnifiedStockFilter.ProcessStock` 里加一段由配置开关控制的上述诊断输出，便于你直接对单只股票做对比。

---

## 五、表格5/6 无数据时的排查与放松

### 1. 诊断输出（FilterDiagnose_56）

在 `App.config` 中设置 `FilterDiagnose_56=true` 后，执行过滤 5、6 时会在控制台输出：

- **候选数**：有今日+昨日 KD 且 昨天成交金额≥N×1亿 的股票数  
- **条件5 各子条件**：A2>=M1、K1<M3、K1<A2、全部  
- **条件6 各子条件**：65<=A2<78、K1<M4、K1<A2、全部
