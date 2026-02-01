using System;
using System.Collections.Generic;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 股票数据仓储接口
    /// 定义股票日线数据的读写方法
    /// </summary>
    public interface IStockDataRepository
    {
        /// <summary>
        /// 测试数据库连接
        /// </summary>
        bool TestConnection();

        /// <summary>
        /// 保存日线数据（批量）
        /// </summary>
        /// <param name="records">日线数据记录列表</param>
        /// <returns>保存成功的记录数</returns>
        int SaveDailyData(List<DailyDataRecord> records);

        /// <summary>
        /// 获取指定时间范围内的日线数据
        /// </summary>
        List<DailyKlineData> GetDailyData(string stockCode, DateTime startDate, DateTime endDate);

        /// <summary>
        /// 获取最新的日线数据
        /// </summary>
        List<DailyKlineData> GetLatestDailyData(string stockCode, int count);

        /// <summary>
        /// 检查股票是否存在数据
        /// </summary>
        bool HasData(string stockCode);

        /// <summary>
        /// 获取数据的日期范围
        /// </summary>
        (DateTime? StartDate, DateTime? EndDate) GetDataDateRange(string stockCode);

        /// <summary>
        /// 获取所有股票代码列表
        /// </summary>
        List<string> GetAllStockCodes();

        /// <summary>
        /// 删除指定股票的所有数据
        /// </summary>
        bool DeleteStockData(string stockCode);

        /// <summary>
        /// 保存股票信息（代码、名称、市场等）
        /// </summary>
        /// <param name="stockInfoList">股票信息列表</param>
        /// <returns>保存成功的记录数</returns>
        int SaveStockInfo(List<(string StockCode, string StockName, ushort MarketCode)> stockInfoList);

        /// <summary>
        /// 获取所有股票代码与名称映射（用于 StockInfoCache 等）
        /// </summary>
        Dictionary<string, string> GetAllStockNames();

        /// <summary>
        /// 获取所有日线数据中的最新交易日期
        /// </summary>
        DateTime? GetLatestTradeDate();
    }
}
