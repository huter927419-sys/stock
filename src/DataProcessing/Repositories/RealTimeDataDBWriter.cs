using System;
using System.Collections.Generic;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 实时数据数据库写入器 - 保存数据到PostgreSQL
    /// 注意：此类保留用于向后兼容，新代码建议使用 IRealTimeDataRepository
    /// </summary>
    public class RealTimeDataDBWriter
    {
        private readonly IRealTimeDataRepository _repository;

        /// <summary>
        /// 使用默认仓储初始化（向后兼容）
        /// </summary>
        public RealTimeDataDBWriter()
        {
            _repository = new PostgresRealTimeDataRepository();
        }

        /// <summary>
        /// 使用指定仓储初始化（依赖注入）
        /// </summary>
        public RealTimeDataDBWriter(IRealTimeDataRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        public bool TestConnection()
        {
            return _repository.TestConnection();
        }

        /// <summary>
        /// 保存实时数据到数据库
        /// </summary>
        public int SaveRealTimeData(List<RealTimeDataRecord> records)
        {
            return _repository.SaveRealTimeData(records);
        }
    }
}
