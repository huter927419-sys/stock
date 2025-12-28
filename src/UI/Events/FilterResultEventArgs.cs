using System;
using System.Collections.Generic;
using MQReceiver.Models;

namespace MQReceiver.Events
{
    /// <summary>
    /// 过滤结果事件参数
    /// 用于解耦 FilterService 与 UI 层
    /// </summary>
    public class FilterResultEventArgs : EventArgs
    {
        /// <summary>
        /// 条件1结果：周K、月K、季K 金叉过滤
        /// </summary>
        public List<FilterResultWithHistory> Condition1Results { get; set; }

        /// <summary>
        /// 条件2结果：周K>月K>季K（不含金叉）
        /// </summary>
        public List<FilterResultWithHistory> Condition2Results { get; set; }

        /// <summary>
        /// 条件3结果：月K>季K or 周K less than 月K
        /// </summary>
        public List<FilterResultWithHistory> Condition3Results { get; set; }

        /// <summary>
        /// 过滤执行时间
        /// </summary>
        public DateTime FilterTime { get; set; }

        /// <summary>
        /// 过滤耗时（秒）
        /// </summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>
        /// 处理的股票数量
        /// </summary>
        public int ProcessedCount { get; set; }

        public FilterResultEventArgs()
        {
            Condition1Results = new List<FilterResultWithHistory>();
            Condition2Results = new List<FilterResultWithHistory>();
            Condition3Results = new List<FilterResultWithHistory>();
            FilterTime = DateTime.Now;
        }
    }
}
