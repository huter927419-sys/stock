using MQReceiver.Configuration;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// 配置辅助类
    /// 提供配置提供者选择的统一逻辑
    /// </summary>
    public static class ConfigurationHelper
    {
        /// <summary>
        /// 根据模式获取配置提供者
        /// </summary>
        /// <param name="isStandaloneMode">是否为独立模式</param>
        /// <returns>配置提供者实例</returns>
        public static IConfigurationProvider GetConfigProvider(bool isStandaloneMode)
        {
            if (isStandaloneMode)
            {
                return HaiLiDrvConfigProvider.Instance;
            }
            else
            {
                return AppConfigProvider.Instance;
            }
        }

        /// <summary>
        /// 获取数据库连接字符串（根据配置提供者）
        /// </summary>
        public static string GetConnectionString(IConfigurationProvider configProvider)
        {
            var dbConfig = configProvider.GetDatabaseConfig();
            return DatabaseConnectionHelper.BuildConnectionString(dbConfig);
        }
    }
}

