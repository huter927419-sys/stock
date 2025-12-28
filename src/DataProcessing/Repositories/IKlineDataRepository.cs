using System;
using System.Collections.Generic;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// K线数据仓储接口
    /// 定义K线数据的访问方法，用于解耦KDCalculator与数据库
    /// </summary>
    public interface IKlineDataRepository
    {
        /// <summary>
        /// 获取指定时间范围内的日线数据
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>日线数据列表</returns>
        List<DailyKlineData> GetDailyData(string stockCode, DateTime startDate, DateTime endDate);

        /// <summary>
        /// 获取最新的日线数据
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="count">获取数量</param>
        /// <returns>日线数据列表</returns>
        List<DailyKlineData> GetLatestDailyData(string stockCode, int count);

        /// <summary>
        /// 检查股票是否存在数据
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <returns>是否存在</returns>
        bool HasData(string stockCode);

        /// <summary>
        /// 获取数据的日期范围
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <returns>起始日期和结束日期</returns>
        (DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode);

        /// <summary>
        /// 批量更新日线数据（用于复权计算）
        /// </summary>
        /// <param name="dataList">日线数据列表</param>
        /// <returns>更新的记录数</returns>
        int UpdateDailyData(List<KlineData> dataList);
    }

    /// <summary>
    /// K线数据（用于写入/更新）
    /// </summary>
    public class KlineData
    {
        public string StockCode { get; set; }
        public DateTime TradeDate { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    /// <summary>
    /// 日线K线数据
    /// </summary>
    public class DailyKlineData
    {
        /// <summary>
        /// 交易日期
        /// </summary>
        public DateTime TradeDate { get; set; }

        /// <summary>
        /// 开盘价
        /// </summary>
        public decimal Open { get; set; }

        /// <summary>
        /// 最高价
        /// </summary>
        public decimal High { get; set; }

        /// <summary>
        /// 最低价
        /// </summary>
        public decimal Low { get; set; }

        /// <summary>
        /// 收盘价
        /// </summary>
        public decimal Close { get; set; }

        /// <summary>
        /// 成交量
        /// </summary>
        public decimal Volume { get; set; }
    }
}
