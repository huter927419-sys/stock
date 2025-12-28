using System;
using System.Collections.Generic;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 除权数据仓储接口
    /// 定义除权数据的读写方法
    /// </summary>
    public interface IExRightsDataRepository
    {
        /// <summary>
        /// 测试数据库连接
        /// </summary>
        bool TestConnection();

        /// <summary>
        /// 保存除权数据（批量）
        /// </summary>
        /// <param name="records">除权数据记录列表</param>
        /// <returns>保存成功的记录数</returns>
        int SaveExRightsData(List<ExRightsDataRecord> records);

        /// <summary>
        /// 获取指定股票的除权数据
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <returns>除权数据列表</returns>
        List<ExRightsDataRecord> GetExRightsData(string stockCode);

        /// <summary>
        /// 获取指定时间范围内的除权数据
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>除权数据列表</returns>
        List<ExRightsDataRecord> GetExRightsData(string stockCode, DateTime startDate, DateTime endDate);

        /// <summary>
        /// 检查股票是否存在除权数据
        /// </summary>
        bool HasExRightsData(string stockCode);

        /// <summary>
        /// 删除指定股票的除权数据
        /// </summary>
        bool DeleteExRightsData(string stockCode);

        /// <summary>
        /// 获取指定日期之后的除权数据（用于前复权计算）
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="targetDate">目标日期</param>
        /// <returns>除权数据列表</returns>
        List<ExRightsDataRecord> GetExRightsDataAfterDate(string stockCode, DateTime targetDate);

        /// <summary>
        /// 获取指定日期之前的除权数据（用于后复权计算）
        /// </summary>
        /// <param name="stockCode">股票代码</param>
        /// <param name="targetDate">目标日期</param>
        /// <returns>除权数据列表</returns>
        List<ExRightsDataRecord> GetExRightsDataBeforeDate(string stockCode, DateTime targetDate);
    }
}
