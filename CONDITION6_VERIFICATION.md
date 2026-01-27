# 条件6（金叉）逻辑验证

## 📋 公式定义

**条件6：金叉**
- **公式**：`A1=MAX(K2,K3), REF(K1) < REF(A1,1) AND K1 > A1`
- **说明**：A1取K2（月K）和K3（季K）中较大的值

## 🔍 变量说明

- **K1** = 周K（今日）
- **K2** = 月K（今日）
- **K3** = 季K（今日）
- **REF(K1,1)** = 昨日周K
- **REF(K2,1)** = 昨日月K
- **REF(K3,1)** = 昨日季K
- **A1** = MAX(K2, K3) = MAX(月K, 季K)
- **REF(A1,1)** = MAX(REF(K2,1), REF(K3,1)) = MAX(昨日月K, 昨日季K)

## ✅ 当前实现验证

### 代码实现（`NewFilterConditions.cs:74-75`）

```csharp
case 6: // 金叉：A1=MAX(K2,K3), REF(K1) < REF(A1,1) AND K1 > A1
    return refK1 < refA1 && k1 > a1;
```

### 辅助计算（`NewFilterConditions.cs:50-55`）

```csharp
// A1 = MAX(K2, K3) = MAX(月K, 季K)
decimal a1 = Math.Max(k2, k3);
// REF(A1,1) = MAX(REF(K2,1), REF(K3,1)) = MAX(昨日月K, 昨日季K)
decimal refA1 = Math.Max(refK2, refK3);
```

## 📊 逻辑验证

### 条件分解

1. **A1 = MAX(K2, K3)**
   - 取月K和季K中的较大值
   - ✓ 实现正确：`decimal a1 = Math.Max(k2, k3);`

2. **REF(A1, 1) = MAX(REF(K2,1), REF(K3,1))**
   - 取昨日月K和昨日季K中的较大值
   - ✓ 实现正确：`decimal refA1 = Math.Max(refK2, refK3);`

3. **REF(K1) < REF(A1, 1)**
   - 昨日周K < 昨日A1（昨日月K和昨日季K的较大值）
   - ✓ 实现正确：`refK1 < refA1`

4. **K1 > A1**
   - 今日周K > 今日A1（今日月K和今日季K的较大值）
   - ✓ 实现正确：`k1 > a1`

5. **最终条件**
   - ✓ 实现正确：`refK1 < refA1 && k1 > a1`

## 🧪 测试用例

### 测试用例1：月K > 季K 的情况（通过）

```
今日: 周K=70, 月K=80, 季K=60
昨日: 周K=50, 月K=75, 季K=55

计算:
  A1 = MAX(80, 60) = 80
  REF(A1,1) = MAX(75, 55) = 75
  条件1: 50 < 75 = true
  条件2: 70 > 80 = false
  结果: false AND false = false
```

**修正**：这个用例应该改为：
```
今日: 周K=85, 月K=80, 季K=60
昨日: 周K=50, 月K=75, 季K=55

计算:
  A1 = MAX(80, 60) = 80
  REF(A1,1) = MAX(75, 55) = 75
  条件1: 50 < 75 = true
  条件2: 85 > 80 = true
  结果: true AND true = true ✓
```

### 测试用例2：季K > 月K 的情况（通过）

```
今日: 周K=85, 月K=60, 季K=80
昨日: 周K=50, 月K=55, 季K=75

计算:
  A1 = MAX(60, 80) = 80
  REF(A1,1) = MAX(55, 75) = 75
  条件1: 50 < 75 = true
  条件2: 85 > 80 = true
  结果: true AND true = true ✓
```

### 测试用例3：不满足条件 - 昨日周K >= 昨日A1（不通过）

```
今日: 周K=85, 月K=80, 季K=60
昨日: 周K=80, 月K=75, 季K=55

计算:
  A1 = MAX(80, 60) = 80
  REF(A1,1) = MAX(75, 55) = 75
  条件1: 80 < 75 = false
  条件2: 85 > 80 = true
  结果: false AND true = false ✓
```

### 测试用例4：不满足条件 - 今日周K <= 今日A1（不通过）

```
今日: 周K=70, 月K=80, 季K=60
昨日: 周K=50, 月K=75, 季K=55

计算:
  A1 = MAX(80, 60) = 80
  REF(A1,1) = MAX(75, 55) = 75
  条件1: 50 < 75 = true
  条件2: 70 > 80 = false
  结果: true AND false = false ✓
```

## ✅ 结论

**当前实现完全正确！**

所有逻辑都符合公式要求：
- ✓ A1 = MAX(K2, K3) 计算正确
- ✓ REF(A1,1) = MAX(REF(K2,1), REF(K3,1)) 计算正确
- ✓ REF(K1) < REF(A1,1) 判断正确
- ✓ K1 > A1 判断正确
- ✓ 两个条件用 AND 连接正确

## 📝 金叉的含义

**金叉**：周K线从下方上穿月K和季K中的较大值
- 昨日：周K < MAX(昨日月K, 昨日季K)
- 今日：周K > MAX(今日月K, 今日季K)

这表示周K线向上突破，形成买入信号。
