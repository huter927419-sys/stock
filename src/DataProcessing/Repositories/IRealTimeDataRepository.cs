using System;
using System.Collections.Generic;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 实时数据仓储接口
    /// </summary>
    public interface IRealTimeDataRepository
    {
        /// <summary>
        /// 测试数据库连接
        /// </summary>
        bool TestConnection();

        /// <summary>
        /// 保存实时数据（批量）
        /// </summary>
        int SaveRealTimeData(List<RealTimeDataRecord> records);

        /// <summary>
        /// 获取指定股票的实时数据
        /// </summary>
        RealTimeDataRecord GetRealTimeData(string stockCode);

        /// <summary>
        /// 获取所有实时数据
        /// </summary>
        List<RealTimeDataRecord> GetAllRealTimeData();
    }
}
