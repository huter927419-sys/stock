using System;
using System.Collections.Generic;
using MQReceiver.Models;

namespace MQReceiver.Filters
{
    /// <summary>
    /// 数据验证器
    /// 负责检查实时数据和KD数据的完整性
    /// </summary>
    public class DataValidator
    {
        /// <summary>
        /// 检查实时数据完整性
        /// </summary>
        public DataIntegrityCheckResult CheckRealTimeDataIntegrity(RealTimeDataRecord realTimeData)
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

            // 检查股票代码
            if (string.IsNullOrWhiteSpace(realTimeData.StockCode))
            {
                result.Issues.Add("股票代码为空");
                result.IsValid = false;
            }

            // 检查价格数据
            if (realTimeData.NewPrice <= 0)
            {
                result.Issues.Add("最新价无效或为0");
            }
            if (realTimeData.LastClose <= 0)
            {
                result.Issues.Add("昨收价无效或为0");
            }
            if (realTimeData.Open <= 0)
            {
                result.Issues.Add("开盘价无效或为0");
            }
            if (realTimeData.High <= 0)
            {
                result.Issues.Add("最高价无效或为0");
            }
            if (realTimeData.Low <= 0)
            {
                result.Issues.Add("最低价无效或为0");
            }

            // 检查价格逻辑性
            if (realTimeData.High < realTimeData.Low)
            {
                result.Issues.Add("最高价小于最低价");
            }
            if (realTimeData.NewPrice < realTimeData.Low || realTimeData.NewPrice > realTimeData.High)
            {
                result.Issues.Add("最新价不在最低价和最高价范围内");
            }

            // 检查成交量
            if (realTimeData.Volume < 0)
            {
                result.Issues.Add("成交量为负数");
            }

            // 检查更新时间（数据不应超过1天）
            if (realTimeData.UpdateTime != default(DateTime))
            {
                var age = DateTime.Now - realTimeData.UpdateTime;
                if (age.TotalDays > 1)
                {
                    result.Issues.Add($"数据过期（{age.TotalDays:F1}天前）");
                }
            }

            result.IsValid = result.Issues.Count == 0;
            return result;
        }

        /// <summary>
        /// 检查KD数据完整性
        /// </summary>
        public DataIntegrityCheckResult CheckKDDataIntegrity(
            string stockCode,
            KDResult weeklyKD,
            KDResult monthlyKD,
            KDResult quarterlyKD)
        {
            var result = new DataIntegrityCheckResult
            {
                StockCode = stockCode ?? "未知"
            };

            // 检查KD计算结果是否存在
            if (weeklyKD == null)
            {
                result.Issues.Add("周KD计算失败（可能日线数据不足）");
            }
            if (monthlyKD == null)
            {
                result.Issues.Add("月KD计算失败（可能日线数据不足）");
            }
            if (quarterlyKD == null)
            {
                result.Issues.Add("季KD计算失败（可能日线数据不足）");
            }

            // 如果任何一个KD计算失败，则认为数据不完整
            if (weeklyKD == null || monthlyKD == null || quarterlyKD == null)
            {
                result.IsValid = false;
                return result;
            }

            // 检查KD值是否在合理范围内（0-100）
            ValidateKDRange(result, "周", weeklyKD);
            ValidateKDRange(result, "月", monthlyKD);
            ValidateKDRange(result, "季", quarterlyKD);

            // 检查KD值的合理性（D值通常应该接近K值，差异不应过大）
            ValidateKDDifference(result, "周", weeklyKD);
            ValidateKDDifference(result, "月", monthlyKD);
            ValidateKDDifference(result, "季", quarterlyKD);

            result.IsValid = result.Issues.Count == 0;
            return result;
        }

        private void ValidateKDRange(DataIntegrityCheckResult result, string period, KDResult kd)
        {
            if (kd.K < 0 || kd.K > 100)
            {
                result.Issues.Add($"{period}K值异常: {kd.K:F2}");
            }
            if (kd.D < 0 || kd.D > 100)
            {
                result.Issues.Add($"{period}D值异常: {kd.D:F2}");
            }
        }

        private void ValidateKDDifference(DataIntegrityCheckResult result, string period, KDResult kd)
        {
            if (Math.Abs(kd.K - kd.D) > 50)
            {
                result.Issues.Add($"{period}KD差异过大: K={kd.K:F2}, D={kd.D:F2}");
            }
        }
    }
}
