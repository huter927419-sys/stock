using System;
using System.Collections.Generic;
using MQReceiver.Models;

namespace MQReceiver.Events
{
    /// <summary>
    /// 计算结果事件参数
    /// 用于解耦 FilterService 与 UI 层
    /// </summary>
    public class FilterResultEventArgs : EventArgs
    {
        /// <summary>
        /// 表格一结果：强多金叉
        /// </summary>
        public List<FilterResultWithHistory> Table1Results { get; set; }

        /// <summary>
        /// 表格二结果：中多金叉
        /// </summary>
        public List<FilterResultWithHistory> Table2Results { get; set; }

        /// <summary>
        /// 表格三结果：强多排列
        /// </summary>
        public List<FilterResultWithHistory> Table3Results { get; set; }

        /// <summary>
        /// 表格四结果：中多排列
        /// </summary>
        public List<FilterResultWithHistory> Table4Results { get; set; }

        /// <summary>
        /// 表格五结果：强多缠绕
        /// </summary>
        public List<FilterResultWithHistory> Table5Results { get; set; }

        /// <summary>
        /// 表格六结果：中多缠绕
        /// </summary>
        public List<FilterResultWithHistory> Table6Results { get; set; }

        /// <summary>
        /// 表格七结果：强多反弹
        /// </summary>
        public List<FilterResultWithHistory> Table7Results { get; set; }

        /// <summary>
        /// 表格八结果：中多反弹
        /// </summary>
        public List<FilterResultWithHistory> Table8Results { get; set; }

        /// <summary>
        /// 计算执行时间
        /// </summary>
        public DateTime FilterTime { get; set; }

        /// <summary>
        /// 计算耗时（秒）
        /// </summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>
        /// 处理的股票数量
        /// </summary>
        public int ProcessedCount { get; set; }

        public FilterResultEventArgs()
        {
            Table1Results = new List<FilterResultWithHistory>();
            Table2Results = new List<FilterResultWithHistory>();
            Table3Results = new List<FilterResultWithHistory>();
            Table4Results = new List<FilterResultWithHistory>();
            Table5Results = new List<FilterResultWithHistory>();
            Table6Results = new List<FilterResultWithHistory>();
            Table7Results = new List<FilterResultWithHistory>();
            Table8Results = new List<FilterResultWithHistory>();
            FilterTime = DateTime.Now;
        }
    }
}
