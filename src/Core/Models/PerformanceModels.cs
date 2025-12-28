using System;
using System.Collections.Generic;

namespace MQReceiver.Models
{
    /// <summary>
    /// 性能分析结果
    /// </summary>
    public class PerformanceEstimate
    {
        public int StockCount { get; set; }
        public TimeSpan EstimatedTime { get; set; }
        public TimeSpan EstimatedTimeWithOptimization { get; set; }
        public string Breakdown { get; set; }
        public List<string> Recommendations { get; set; }

        public PerformanceEstimate()
        {
            Recommendations = new List<string>();
        }
    }
}
