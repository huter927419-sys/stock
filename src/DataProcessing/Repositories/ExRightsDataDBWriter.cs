using System;
using System.Collections.Generic;
using MQReceiver.Configuration;

namespace MQReceiver.Repositories
{
    /// <summary>
    /// 除权数据数据库写入器 - 保存除权数据到PostgreSQL
    /// 注意：此类保留用于向后兼容，新代码建议使用 IExRightsDataRepository
    /// </summary>
    public class ExRightsDataDBWriter
    {
        private readonly IExRightsDataRepository _repository;
        private static readonly IConfigurationProvider _configProvider = AppConfigProvider.Instance;

        /// <summary>
        /// 使用默认仓储初始化（向后兼容）
        /// </summary>
        public ExRightsDataDBWriter()
        {
            _repository = new PostgresExRightsDataRepository();

            string host = _configProvider.GetString("DatabaseHost", "localhost");
            int port = _configProvider.GetInt("DatabasePort", 8532);
            string database = _configProvider.GetString("DatabaseName", "stockdb");
            string username = _configProvider.GetString("DatabaseUser", "postgres");

            Console.WriteLine("除权数据数据库配置: Host={0}, Port={1}, Database={2}, User={3}",
                host, port, database, username);
        }

        /// <summary>
        /// 使用指定仓储初始化（依赖注入）
        /// </summary>
        public ExRightsDataDBWriter(IExRightsDataRepository repository)
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
        /// 保存除权数据到数据库
        /// </summary>
        public int SaveExRightsData(List<ExRightsDataRecord> records)
        {
            return _repository.SaveExRightsData(records);
        }
    }
}
