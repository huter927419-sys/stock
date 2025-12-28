using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MQReceiver.Cache;
using MQReceiver.Calculators;
using MQReceiver.Configuration;
using MQReceiver.Helpers;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 股票过滤器 - 根据KD指标和实时数据过滤股票
    /// 注意：此类保留用于向后兼容，新代码建议使用 StockFilterOrchestrator
    /// </summary>
    public class StockFilter
    {
        private readonly KDCalculator _kdCalculator;
        private readonly RealTimeDataCache _realTimeCache;
        private static readonly IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        public StockFilter(KDCalculator kdCalculator, RealTimeDataCache realTimeCache)
        {
            _kdCalculator = kdCalculator;
            _realTimeCache = realTimeCache;
        }

        #region 向后兼容的类型别名

        // 保持向后兼容的嵌套类型（重定向到 Filters 命名空间）
        public class FilterCondition
        {
            // 周KD条件
            public decimal? WeeklyKMin { get; set; }
            public decimal? WeeklyKMax { get; set; }
            public decimal? WeeklyDMin { get; set; }
            public decimal? WeeklyDMax { get; set; }

            // 月KD条件
            public decimal? MonthlyKMin { get; set; }
            public decimal? MonthlyKMax { get; set; }
            public decimal? MonthlyDMin { get; set; }
            public decimal? MonthlyDMax { get; set; }

            // 季KD条件
            public decimal? QuarterlyKMin { get; set; }
            public decimal? QuarterlyKMax { get; set; }
            public decimal? QuarterlyDMin { get; set; }
            public decimal? QuarterlyDMax { get; set; }

            // 实时数据条件
            public decimal? PriceMin { get; set; }
            public decimal? PriceMax { get; set; }
            public decimal? VolumeMin { get; set; }
            public decimal? ChangePercentMin { get; set; }
            public decimal? ChangePercentMax { get; set; }
        }

        #endregion

        #region 数据完整性检查

        /// <summary>
        /// 检查实时数据完整性
        /// </summary>
        private DataIntegrityCheckResult CheckRealTimeDataIntegrity(RealTimeDataRecord realTimeData)
        {
            var result = new DataIntegrityCheckResult
            {
                StockCode = realTimeData?.StockCode ?? "未知"
            };

            if (realTimeData == null)
            {
                result.Issues.Add("实时数据为空");
                result.IsValid = false;
                return result;
            }

            if (string.IsNullOrWhiteSpace(realTimeData.StockCode))
            {
                result.Issues.Add("股票代码为空");
                result.IsValid = false;
            }

            if (realTimeData.NewPrice <= 0)
                result.Issues.Add("最新价无效或为0");
            if (realTimeData.LastClose <= 0)
                result.Issues.Add("昨收价无效或为0");
            if (realTimeData.Open <= 0)
                result.Issues.Add("开盘价无效或为0");
            if (realTimeData.High <= 0)
                result.Issues.Add("最高价无效或为0");
            if (realTimeData.Low <= 0)
                result.Issues.Add("最低价无效或为0");

            if (realTimeData.High < realTimeData.Low)
                result.Issues.Add("最高价小于最低价");
            if (realTimeData.NewPrice < realTimeData.Low || realTimeData.NewPrice > realTimeData.High)
                result.Issues.Add("最新价不在最低价和最高价范围内");

            if (realTimeData.Volume < 0)
                result.Issues.Add("成交量为负数");

            if (realTimeData.UpdateTime != default(DateTime))
            {
                var age = DateTime.Now - realTimeData.UpdateTime;
                if (age.TotalDays > 1)
                    result.Issues.Add($"数据过期（{age.TotalDays:F1}天前）");
            }

            result.IsValid = result.Issues.Count == 0;
            return result;
        }

        /// <summary>
        /// 检查KD数据完整性
        /// </summary>
        private DataIntegrityCheckResult CheckKDDataIntegrity(string stockCode,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            var result = new DataIntegrityCheckResult
            {
                StockCode = stockCode ?? "未知"
            };

            if (weeklyKD == null)
                result.Issues.Add("周KD计算失败（可能日线数据不足）");
            if (monthlyKD == null)
                result.Issues.Add("月KD计算失败（可能日线数据不足）");
            if (quarterlyKD == null)
                result.Issues.Add("季KD计算失败（可能日线数据不足）");

            if (weeklyKD == null || monthlyKD == null || quarterlyKD == null)
            {
                result.IsValid = false;
                return result;
            }

            // 检查KD值是否在合理范围内（0-100）
            if (weeklyKD.K < 0 || weeklyKD.K > 100)
                result.Issues.Add($"周K值异常: {weeklyKD.K:F2}");
            if (weeklyKD.D < 0 || weeklyKD.D > 100)
                result.Issues.Add($"周D值异常: {weeklyKD.D:F2}");
            if (monthlyKD.K < 0 || monthlyKD.K > 100)
                result.Issues.Add($"月K值异常: {monthlyKD.K:F2}");
            if (monthlyKD.D < 0 || monthlyKD.D > 100)
                result.Issues.Add($"月D值异常: {monthlyKD.D:F2}");
            if (quarterlyKD.K < 0 || quarterlyKD.K > 100)
                result.Issues.Add($"季K值异常: {quarterlyKD.K:F2}");
            if (quarterlyKD.D < 0 || quarterlyKD.D > 100)
                result.Issues.Add($"季D值异常: {quarterlyKD.D:F2}");

            if (Math.Abs(weeklyKD.K - weeklyKD.D) > 50)
                result.Issues.Add($"周KD差异过大: K={weeklyKD.K:F2}, D={weeklyKD.D:F2}");
            if (Math.Abs(monthlyKD.K - monthlyKD.D) > 50)
                result.Issues.Add($"月KD差异过大: K={monthlyKD.K:F2}, D={monthlyKD.D:F2}");
            if (Math.Abs(quarterlyKD.K - quarterlyKD.D) > 50)
                result.Issues.Add($"季KD差异过大: K={quarterlyKD.K:F2}, D={quarterlyKD.D:F2}");

            result.IsValid = result.Issues.Count == 0;
            return result;
        }

        #endregion

        #region 通用过滤方法

        /// <summary>
        /// 根据条件过滤股票
        /// </summary>
        public List<FilterResult> FilterStocks(FilterCondition condition, DateTime targetDate)
        {
            var results = new List<FilterResult>();
            int skippedCount = 0;
            int invalidDataCount = 0;

            var realTimeDataList = _realTimeCache.GetAllData();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
            {
                Console.WriteLine("警告: 实时数据缓存为空，无法进行过滤");
                return results;
            }

            if (_realTimeCache.IsExpired(TimeSpan.FromHours(1)))
            {
                Console.WriteLine("警告: 实时数据可能已过期（超过1小时未更新）");
            }

            foreach (var realTimeData in realTimeDataList)
            {
                try
                {
                    var integrityCheck = CheckRealTimeDataIntegrity(realTimeData);
                    if (!integrityCheck.IsValid)
                    {
                        invalidDataCount++;
                        if (invalidDataCount <= 10)
                        {
                            Console.WriteLine($"跳过无效实时数据: {realTimeData.StockCode ?? "未知"} - {string.Join(", ", integrityCheck.Issues)}");
                        }
                        continue;
                    }

                    var weeklyKD = _kdCalculator.CalculateWeeklyKD(realTimeData.StockCode, targetDate);
                    var monthlyKD = _kdCalculator.CalculateMonthlyKD(realTimeData.StockCode, targetDate);
                    var quarterlyKD = _kdCalculator.CalculateQuarterlyKD(realTimeData.StockCode, targetDate);

                    var kdCheck = CheckKDDataIntegrity(realTimeData.StockCode, weeklyKD, monthlyKD, quarterlyKD);
                    if (!kdCheck.IsValid)
                    {
                        skippedCount++;
                        if (skippedCount <= 10)
                        {
                            Console.WriteLine($"跳过股票 {realTimeData.StockCode}: {string.Join(", ", kdCheck.Issues)}");
                        }
                        continue;
                    }

                    decimal changePercent = 0;
                    if (realTimeData.LastClose > 0)
                    {
                        changePercent = (realTimeData.NewPrice - realTimeData.LastClose) / realTimeData.LastClose * 100;
                    }

                    if (Math.Abs(changePercent) > 20)
                    {
                        Console.WriteLine($"警告: 股票 {realTimeData.StockCode} 涨跌幅异常: {changePercent:F2}%");
                    }

                    if (CheckCondition(condition, weeklyKD, monthlyKD, quarterlyKD, realTimeData, changePercent))
                    {
                        results.Add(new FilterResult
                        {
                            StockCode = realTimeData.StockCode,
                            StockName = realTimeData.StockName,
                            CurrentPrice = realTimeData.NewPrice,
                            ChangePercent = Math.Round(changePercent, 2),
                            WeeklyK = weeklyKD.K,
                            WeeklyD = weeklyKD.D,
                            MonthlyK = monthlyKD.K,
                            MonthlyD = monthlyKD.D,
                            QuarterlyK = quarterlyKD.K,
                            QuarterlyD = quarterlyKD.D,
                            Reason = "通过所有过滤条件"
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"过滤股票 {realTimeData.StockCode ?? "未知"} 时出错: {ex.Message}");
                }
            }

            if (invalidDataCount > 0 || skippedCount > 0)
            {
                Console.WriteLine($"数据完整性检查: 跳过 {invalidDataCount} 条无效实时数据, {skippedCount} 只股票因KD数据不完整被跳过");
            }

            return results;
        }

        /// <summary>
        /// 检查是否满足过滤条件
        /// </summary>
        private bool CheckCondition(FilterCondition condition,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD,
            RealTimeDataRecord realTimeData,
            decimal changePercent)
        {
            if (condition.WeeklyKMin.HasValue && weeklyKD.K < condition.WeeklyKMin.Value)
                return false;
            if (condition.WeeklyKMax.HasValue && weeklyKD.K > condition.WeeklyKMax.Value)
                return false;
            if (condition.WeeklyDMin.HasValue && weeklyKD.D < condition.WeeklyDMin.Value)
                return false;
            if (condition.WeeklyDMax.HasValue && weeklyKD.D > condition.WeeklyDMax.Value)
                return false;

            if (condition.MonthlyKMin.HasValue && monthlyKD.K < condition.MonthlyKMin.Value)
                return false;
            if (condition.MonthlyKMax.HasValue && monthlyKD.K > condition.MonthlyKMax.Value)
                return false;
            if (condition.MonthlyDMin.HasValue && monthlyKD.D < condition.MonthlyDMin.Value)
                return false;
            if (condition.MonthlyDMax.HasValue && monthlyKD.D > condition.MonthlyDMax.Value)
                return false;

            if (condition.QuarterlyKMin.HasValue && quarterlyKD.K < condition.QuarterlyKMin.Value)
                return false;
            if (condition.QuarterlyKMax.HasValue && quarterlyKD.K > condition.QuarterlyKMax.Value)
                return false;
            if (condition.QuarterlyDMin.HasValue && quarterlyKD.D < condition.QuarterlyDMin.Value)
                return false;
            if (condition.QuarterlyDMax.HasValue && quarterlyKD.D > condition.QuarterlyDMax.Value)
                return false;

            if (condition.PriceMin.HasValue && realTimeData.NewPrice < condition.PriceMin.Value)
                return false;
            if (condition.PriceMax.HasValue && realTimeData.NewPrice > condition.PriceMax.Value)
                return false;
            if (condition.VolumeMin.HasValue && realTimeData.Volume < condition.VolumeMin.Value)
                return false;
            if (condition.ChangePercentMin.HasValue && changePercent < condition.ChangePercentMin.Value)
                return false;
            if (condition.ChangePercentMax.HasValue && changePercent > condition.ChangePercentMax.Value)
                return false;

            return true;
        }

        /// <summary>
        /// 获取默认过滤条件（示例）
        /// </summary>
        public static FilterCondition GetDefaultCondition()
        {
            return new FilterCondition
            {
                WeeklyKMin = 20,
                WeeklyKMax = 80,
                WeeklyDMin = 20,
                WeeklyDMax = 80,
                MonthlyKMin = 20,
                MonthlyKMax = 80,
                MonthlyDMin = 20,
                MonthlyDMax = 80,
                QuarterlyKMin = 20,
                QuarterlyKMax = 80,
                QuarterlyDMin = 20,
                QuarterlyDMax = 80,
                PriceMin = 0.01m,
                VolumeMin = 0
            };
        }

        #endregion

        #region 金叉过滤（条件1）

        /// <summary>
        /// 过滤条件1：周K、月K、季K 金叉过滤
        /// </summary>
        public List<SimpleFilterResult> FilterByGoldenCross(GoldenCrossCondition condition, DateTime targetDate)
        {
            var results = new List<SimpleFilterResult>();
            var realTimeDataList = _realTimeCache.GetAllData();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
                return results;

            foreach (var realTimeData in realTimeDataList)
            {
                try
                {
                    var integrityCheck = CheckRealTimeDataIntegrity(realTimeData);
                    if (!integrityCheck.IsValid)
                        continue;

                    var weeklyKD = _kdCalculator.CalculateWeeklyKD(realTimeData.StockCode, targetDate);
                    var monthlyKD = _kdCalculator.CalculateMonthlyKD(realTimeData.StockCode, targetDate);
                    var quarterlyKD = _kdCalculator.CalculateQuarterlyKD(realTimeData.StockCode, targetDate);

                    var kdCheck = CheckKDDataIntegrity(realTimeData.StockCode, weeklyKD, monthlyKD, quarterlyKD);
                    if (!kdCheck.IsValid)
                        continue;

                    if (condition.PriceMin.HasValue && realTimeData.NewPrice < condition.PriceMin.Value)
                        continue;
                    if (condition.VolumeMin.HasValue && realTimeData.Volume < condition.VolumeMin.Value)
                        continue;

                    bool weeklyGoldenCross = !condition.RequireWeeklyGoldenCross || (weeklyKD.K > weeklyKD.D);
                    bool monthlyGoldenCross = !condition.RequireMonthlyGoldenCross || (monthlyKD.K > monthlyKD.D);
                    bool quarterlyGoldenCross = !condition.RequireQuarterlyGoldenCross || (quarterlyKD.K > quarterlyKD.D);

                    if (!weeklyGoldenCross || !monthlyGoldenCross || !quarterlyGoldenCross)
                        continue;

                    if (condition.WeeklyKMin.HasValue && weeklyKD.K < condition.WeeklyKMin.Value)
                        continue;
                    if (condition.WeeklyKMax.HasValue && weeklyKD.K > condition.WeeklyKMax.Value)
                        continue;
                    if (condition.MonthlyKMin.HasValue && monthlyKD.K < condition.MonthlyKMin.Value)
                        continue;
                    if (condition.MonthlyKMax.HasValue && monthlyKD.K > condition.MonthlyKMax.Value)
                        continue;
                    if (condition.QuarterlyKMin.HasValue && quarterlyKD.K < condition.QuarterlyKMin.Value)
                        continue;
                    if (condition.QuarterlyKMax.HasValue && quarterlyKD.K > condition.QuarterlyKMax.Value)
                        continue;

                    results.Add(new SimpleFilterResult
                    {
                        StockCode = realTimeData.StockCode,
                        StockName = realTimeData.StockName ?? realTimeData.StockCode,
                        WeeklyK = Math.Round(weeklyKD.K, 2),
                        MonthlyK = Math.Round(monthlyKD.K, 2),
                        QuarterlyK = Math.Round(quarterlyKD.K, 2)
                    });
                }
                catch (Exception)
                {
                    // 静默处理错误，继续处理下一个
                }
            }

            return results;
        }

        #endregion

        #region K值排序过滤（条件2）

        /// <summary>
        /// 过滤条件2：周K>月K>季K（不含金叉）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueOrder(KValueOrderCondition condition, DateTime targetDate)
        {
            var results = new List<SimpleFilterResult>();
            var realTimeDataList = _realTimeCache.GetAllData();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
                return results;

            foreach (var realTimeData in realTimeDataList)
            {
                try
                {
                    var integrityCheck = CheckRealTimeDataIntegrity(realTimeData);
                    if (!integrityCheck.IsValid)
                        continue;

                    var weeklyKD = _kdCalculator.CalculateWeeklyKD(realTimeData.StockCode, targetDate);
                    var monthlyKD = _kdCalculator.CalculateMonthlyKD(realTimeData.StockCode, targetDate);
                    var quarterlyKD = _kdCalculator.CalculateQuarterlyKD(realTimeData.StockCode, targetDate);

                    var kdCheck = CheckKDDataIntegrity(realTimeData.StockCode, weeklyKD, monthlyKD, quarterlyKD);
                    if (!kdCheck.IsValid)
                        continue;

                    if (condition.PriceMin.HasValue && realTimeData.NewPrice < condition.PriceMin.Value)
                        continue;
                    if (condition.VolumeMin.HasValue && realTimeData.Volume < condition.VolumeMin.Value)
                        continue;

                    if (condition.WeeklyKMin.HasValue && weeklyKD.K < condition.WeeklyKMin.Value)
                        continue;
                    if (condition.WeeklyKMax.HasValue && weeklyKD.K > condition.WeeklyKMax.Value)
                        continue;
                    if (condition.MonthlyKMin.HasValue && monthlyKD.K < condition.MonthlyKMin.Value)
                        continue;
                    if (condition.MonthlyKMax.HasValue && monthlyKD.K > condition.MonthlyKMax.Value)
                        continue;
                    if (condition.QuarterlyKMin.HasValue && quarterlyKD.K < condition.QuarterlyKMin.Value)
                        continue;
                    if (condition.QuarterlyKMax.HasValue && quarterlyKD.K > condition.QuarterlyKMax.Value)
                        continue;

                    if (weeklyKD.K > monthlyKD.K && monthlyKD.K > quarterlyKD.K)
                    {
                        results.Add(new SimpleFilterResult
                        {
                            StockCode = realTimeData.StockCode,
                            StockName = realTimeData.StockName ?? realTimeData.StockCode,
                            WeeklyK = Math.Round(weeklyKD.K, 2),
                            MonthlyK = Math.Round(monthlyKD.K, 2),
                            QuarterlyK = Math.Round(quarterlyKD.K, 2)
                        });
                    }
                }
                catch (Exception)
                {
                    // 静默处理错误，继续处理下一个
                }
            }

            return results;
        }

        #endregion

        #region K值关系过滤（条件3）

        /// <summary>
        /// 过滤条件3：月K>季K or 周K 小于 月K
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueRelation(KValueRelationCondition condition, DateTime targetDate)
        {
            var results = new List<SimpleFilterResult>();
            var realTimeDataList = _realTimeCache.GetAllData();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
                return results;

            foreach (var realTimeData in realTimeDataList)
            {
                try
                {
                    var integrityCheck = CheckRealTimeDataIntegrity(realTimeData);
                    if (!integrityCheck.IsValid)
                        continue;

                    var weeklyKD = _kdCalculator.CalculateWeeklyKD(realTimeData.StockCode, targetDate);
                    var monthlyKD = _kdCalculator.CalculateMonthlyKD(realTimeData.StockCode, targetDate);
                    var quarterlyKD = _kdCalculator.CalculateQuarterlyKD(realTimeData.StockCode, targetDate);

                    var kdCheck = CheckKDDataIntegrity(realTimeData.StockCode, weeklyKD, monthlyKD, quarterlyKD);
                    if (!kdCheck.IsValid)
                        continue;

                    if (condition.PriceMin.HasValue && realTimeData.NewPrice < condition.PriceMin.Value)
                        continue;
                    if (condition.VolumeMin.HasValue && realTimeData.Volume < condition.VolumeMin.Value)
                        continue;

                    if (condition.WeeklyKMin.HasValue && weeklyKD.K < condition.WeeklyKMin.Value)
                        continue;
                    if (condition.WeeklyKMax.HasValue && weeklyKD.K > condition.WeeklyKMax.Value)
                        continue;
                    if (condition.MonthlyKMin.HasValue && monthlyKD.K < condition.MonthlyKMin.Value)
                        continue;
                    if (condition.MonthlyKMax.HasValue && monthlyKD.K > condition.MonthlyKMax.Value)
                        continue;
                    if (condition.QuarterlyKMin.HasValue && quarterlyKD.K < condition.QuarterlyKMin.Value)
                        continue;
                    if (condition.QuarterlyKMax.HasValue && quarterlyKD.K > condition.QuarterlyKMax.Value)
                        continue;

                    if (monthlyKD.K > quarterlyKD.K || weeklyKD.K < monthlyKD.K)
                    {
                        results.Add(new SimpleFilterResult
                        {
                            StockCode = realTimeData.StockCode,
                            StockName = realTimeData.StockName ?? realTimeData.StockCode,
                            WeeklyK = Math.Round(weeklyKD.K, 2),
                            MonthlyK = Math.Round(monthlyKD.K, 2),
                            QuarterlyK = Math.Round(quarterlyKD.K, 2)
                        });
                    }
                }
                catch (Exception)
                {
                    // 静默处理错误，继续处理下一个
                }
            }

            return results;
        }

        #endregion

        #region 配置文件读取（使用配置提供者）

        /// <summary>
        /// 从配置文件读取过滤条件1：周K、月K、季K 金叉过滤
        /// </summary>
        public static GoldenCrossCondition GetGoldenCrossConditionFromConfig()
        {
            return FilterConditionBuilder.BuildGoldenCrossCondition();
        }

        /// <summary>
        /// 从配置文件读取过滤条件2：周K>月K>季K（不含金叉）
        /// </summary>
        public static KValueOrderCondition GetKValueOrderConditionFromConfig()
        {
            return FilterConditionBuilder.BuildKValueOrderCondition();
        }

        /// <summary>
        /// 从配置文件读取过滤条件3：月K>季K or 周K 小于 月K
        /// </summary>
        public static KValueRelationCondition GetKValueRelationConditionFromConfig()
        {
            return FilterConditionBuilder.BuildKValueRelationCondition();
        }

        /// <summary>
        /// 获取默认的金叉过滤条件（兼容旧代码）
        /// </summary>
        public static GoldenCrossCondition GetDefaultGoldenCrossCondition()
        {
            return GetGoldenCrossConditionFromConfig();
        }

        /// <summary>
        /// 获取默认的K值排序过滤条件（兼容旧代码）
        /// </summary>
        public static KValueOrderCondition GetDefaultKValueOrderCondition()
        {
            return GetKValueOrderConditionFromConfig();
        }

        /// <summary>
        /// 获取默认的K值关系过滤条件（兼容旧代码）
        /// </summary>
        public static KValueRelationCondition GetDefaultKValueRelationCondition()
        {
            return GetKValueRelationConditionFromConfig();
        }

        #endregion

        #region 并行过滤方法

        /// <summary>
        /// 获取最优并行度（使用统一的计算逻辑）
        /// </summary>
        private int GetOptimalParallelism()
        {
            return DatabaseConnectionHelper.GetOptimalParallelism();
        }

        /// <summary>
        /// 并行处理股票过滤（通用方法）
        /// </summary>
        private List<SimpleFilterResult> FilterStocksParallel<T>(
            List<RealTimeDataRecord> realTimeDataList,
            T condition,
            DateTime targetDate,
            Func<RealTimeDataRecord, T, DateTime, KDResult, KDResult, KDResult, bool> conditionChecker)
        {
            var results = new ConcurrentBag<SimpleFilterResult>();

            if (realTimeDataList == null || realTimeDataList.Count == 0)
                return new List<SimpleFilterResult>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = GetOptimalParallelism()
            };

            int processedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            Parallel.ForEach(realTimeDataList, parallelOptions, realTimeData =>
            {
                try
                {
                    var integrityCheck = CheckRealTimeDataIntegrity(realTimeData);
                    if (!integrityCheck.IsValid)
                    {
                        Interlocked.Increment(ref skippedCount);
                        return;
                    }

                    var weeklyKD = _kdCalculator.CalculateWeeklyKD(realTimeData.StockCode, targetDate);
                    var monthlyKD = _kdCalculator.CalculateMonthlyKD(realTimeData.StockCode, targetDate);
                    var quarterlyKD = _kdCalculator.CalculateQuarterlyKD(realTimeData.StockCode, targetDate);

                    var kdCheck = CheckKDDataIntegrity(realTimeData.StockCode, weeklyKD, monthlyKD, quarterlyKD);
                    if (!kdCheck.IsValid)
                    {
                        Interlocked.Increment(ref skippedCount);
                        return;
                    }

                    if (conditionChecker(realTimeData, condition, targetDate, weeklyKD, monthlyKD, quarterlyKD))
                    {
                        results.Add(new SimpleFilterResult
                        {
                            StockCode = realTimeData.StockCode,
                            StockName = realTimeData.StockName ?? realTimeData.StockCode,
                            WeeklyK = Math.Round(weeklyKD.K, 2),
                            MonthlyK = Math.Round(monthlyKD.K, 2),
                            QuarterlyK = Math.Round(quarterlyKD.K, 2)
                        });
                        Interlocked.Increment(ref processedCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref skippedCount);
                    }
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref errorCount);
                }
            });

            if (processedCount > 0 || skippedCount > 0 || errorCount > 0)
            {
                Console.WriteLine($"并行处理统计: 通过={processedCount}, 跳过={skippedCount}, 错误={errorCount}, 并行度={GetOptimalParallelism()}");
            }

            return results.ToList();
        }

        /// <summary>
        /// 过滤条件1：周K、月K、季K 金叉过滤（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByGoldenCrossParallel(GoldenCrossCondition condition, DateTime targetDate)
        {
            var realTimeDataList = _realTimeCache.GetAllData();

            return FilterStocksParallel(realTimeDataList, condition, targetDate,
                (realTimeData, cond, date, weeklyKD, monthlyKD, quarterlyKD) =>
                {
                    if (weeklyKD.K <= cond.WeeklyKDefaultMin)
                        return false;
                    if (monthlyKD.K <= cond.MonthlyKDefaultMin)
                        return false;
                    if (quarterlyKD.K <= cond.QuarterlyKDefaultMin)
                        return false;

                    if (cond.PriceMin.HasValue && realTimeData.NewPrice < cond.PriceMin.Value)
                        return false;
                    if (cond.VolumeMin.HasValue && realTimeData.Volume < cond.VolumeMin.Value)
                        return false;

                    bool weeklyGoldenCross = !cond.RequireWeeklyGoldenCross || (weeklyKD.K > weeklyKD.D);
                    bool monthlyGoldenCross = !cond.RequireMonthlyGoldenCross || (monthlyKD.K > monthlyKD.D);
                    bool quarterlyGoldenCross = !cond.RequireQuarterlyGoldenCross || (quarterlyKD.K > quarterlyKD.D);

                    if (!weeklyGoldenCross || !monthlyGoldenCross || !quarterlyGoldenCross)
                        return false;

                    if (cond.WeeklyKMin.HasValue && weeklyKD.K < cond.WeeklyKMin.Value)
                        return false;
                    if (cond.WeeklyKMax.HasValue && weeklyKD.K > cond.WeeklyKMax.Value)
                        return false;
                    if (cond.MonthlyKMin.HasValue && monthlyKD.K < cond.MonthlyKMin.Value)
                        return false;
                    if (cond.MonthlyKMax.HasValue && monthlyKD.K > cond.MonthlyKMax.Value)
                        return false;
                    if (cond.QuarterlyKMin.HasValue && quarterlyKD.K < cond.QuarterlyKMin.Value)
                        return false;
                    if (cond.QuarterlyKMax.HasValue && quarterlyKD.K > cond.QuarterlyKMax.Value)
                        return false;

                    return true;
                });
        }

        /// <summary>
        /// 过滤条件2：周K>月K>季K（不含金叉）（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueOrderParallel(KValueOrderCondition condition, DateTime targetDate)
        {
            var realTimeDataList = _realTimeCache.GetAllData();

            return FilterStocksParallel(realTimeDataList, condition, targetDate,
                (realTimeData, cond, date, weeklyKD, monthlyKD, quarterlyKD) =>
                {
                    if (weeklyKD.K <= cond.WeeklyKDefaultMin)
                        return false;
                    if (monthlyKD.K <= cond.MonthlyKDefaultMin)
                        return false;
                    if (quarterlyKD.K <= cond.QuarterlyKDefaultMin)
                        return false;

                    if (cond.PriceMin.HasValue && realTimeData.NewPrice < cond.PriceMin.Value)
                        return false;
                    if (cond.VolumeMin.HasValue && realTimeData.Volume < cond.VolumeMin.Value)
                        return false;

                    if (cond.WeeklyKMin.HasValue && weeklyKD.K < cond.WeeklyKMin.Value)
                        return false;
                    if (cond.WeeklyKMax.HasValue && weeklyKD.K > cond.WeeklyKMax.Value)
                        return false;
                    if (cond.MonthlyKMin.HasValue && monthlyKD.K < cond.MonthlyKMin.Value)
                        return false;
                    if (cond.MonthlyKMax.HasValue && monthlyKD.K > cond.MonthlyKMax.Value)
                        return false;
                    if (cond.QuarterlyKMin.HasValue && quarterlyKD.K < cond.QuarterlyKMin.Value)
                        return false;
                    if (cond.QuarterlyKMax.HasValue && quarterlyKD.K > cond.QuarterlyKMax.Value)
                        return false;

                    if (!(weeklyKD.K > monthlyKD.K && monthlyKD.K > quarterlyKD.K))
                        return false;

                    bool isNoGoldenCross = CheckNoGoldenCrossAfterBreak(realTimeData.StockCode, date, weeklyKD, monthlyKD, quarterlyKD);
                    return isNoGoldenCross;
                });
        }

        /// <summary>
        /// 过滤条件3：月K>季K or 周K 小于 月K（并行版本）
        /// </summary>
        public List<SimpleFilterResult> FilterByKValueRelationParallel(KValueRelationCondition condition, DateTime targetDate)
        {
            var realTimeDataList = _realTimeCache.GetAllData();

            return FilterStocksParallel(realTimeDataList, condition, targetDate,
                (realTimeData, cond, date, weeklyKD, monthlyKD, quarterlyKD) =>
                {
                    if (weeklyKD.K <= cond.WeeklyKDefaultMin)
                        return false;
                    if (monthlyKD.K <= cond.MonthlyKDefaultMin)
                        return false;
                    if (quarterlyKD.K <= cond.QuarterlyKDefaultMin)
                        return false;

                    if (cond.PriceMin.HasValue && realTimeData.NewPrice < cond.PriceMin.Value)
                        return false;
                    if (cond.VolumeMin.HasValue && realTimeData.Volume < cond.VolumeMin.Value)
                        return false;

                    if (cond.WeeklyKMin.HasValue && weeklyKD.K < cond.WeeklyKMin.Value)
                        return false;
                    if (cond.WeeklyKMax.HasValue && weeklyKD.K > cond.WeeklyKMax.Value)
                        return false;
                    if (cond.MonthlyKMin.HasValue && monthlyKD.K < cond.MonthlyKMin.Value)
                        return false;
                    if (cond.MonthlyKMax.HasValue && monthlyKD.K > cond.MonthlyKMax.Value)
                        return false;
                    if (cond.QuarterlyKMin.HasValue && quarterlyKD.K < cond.QuarterlyKMin.Value)
                        return false;
                    if (cond.QuarterlyKMax.HasValue && quarterlyKD.K > cond.QuarterlyKMax.Value)
                        return false;

                    return monthlyKD.K > quarterlyKD.K || weeklyKD.K < monthlyKD.K;
                });
        }

        #endregion

        #region 金叉历史检查

        /// <summary>
        /// 检查"不含金叉"条件：从破了金叉后没有再次突破金叉
        /// </summary>
        private bool CheckNoGoldenCrossAfterBreak(string stockCode, DateTime targetDate,
            KDResult weeklyKD, KDResult monthlyKD, KDResult quarterlyKD)
        {
            try
            {
                bool currentNotGoldenCross = weeklyKD.K <= weeklyKD.D &&
                                            monthlyKD.K <= monthlyKD.D &&
                                            quarterlyKD.K <= quarterlyKD.D;

                if (!currentNotGoldenCross)
                    return false;

                var weeklyHistory = _kdCalculator.GetWeeklyKDSequence(stockCode, targetDate, 10);
                var monthlyHistory = _kdCalculator.GetMonthlyKDSequence(stockCode, targetDate, 10);
                var quarterlyHistory = _kdCalculator.GetQuarterlyKDSequence(stockCode, targetDate, 10);

                bool weeklyNoGoldenCross = CheckNoGoldenCrossInHistory(weeklyHistory, weeklyKD);
                bool monthlyNoGoldenCross = CheckNoGoldenCrossInHistory(monthlyHistory, monthlyKD);
                bool quarterlyNoGoldenCross = CheckNoGoldenCrossInHistory(quarterlyHistory, quarterlyKD);

                return weeklyNoGoldenCross && monthlyNoGoldenCross && quarterlyNoGoldenCross;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查不含金叉条件失败: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// 检查历史KD序列中是否满足"不含金叉"条件
        /// </summary>
        private bool CheckNoGoldenCrossInHistory(List<KDResult> history, KDResult current)
        {
            if (history == null || history.Count == 0)
                return true;

            bool foundGoldenCross = false;
            bool foundBreak = false;

            for (int i = history.Count - 1; i >= 0; i--)
            {
                var kd = history[i];

                if (kd.K > kd.D)
                {
                    if (foundBreak)
                        return false;
                    foundGoldenCross = true;
                }
                else if (kd.K <= kd.D)
                {
                    if (foundGoldenCross)
                        foundBreak = true;
                }
            }

            if (current.K <= current.D)
            {
                if (foundGoldenCross && foundBreak)
                    return true;
            }

            return true;
        }

        #endregion
    }
}
