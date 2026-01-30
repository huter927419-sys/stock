using System;
using System.Configuration;
using System.IO;
using Config = System.Configuration.Configuration;

namespace MQReceiver.Configuration
{
    /// <summary>
    /// HaiLiDrv独立配置文件提供者
    /// 使用独立的HaiLiDrv.config文件，与主程序配置分离
    /// </summary>
    public class HaiLiDrvConfigProvider : IConfigurationProvider
    {
        private static HaiLiDrvConfigProvider _instance;
        private static readonly object _lock = new object();
        private Config _config;
        private string _configFilePath;

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static HaiLiDrvConfigProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HaiLiDrvConfigProvider();
                        }
                    }
                }
                return _instance;
            }
        }

        private HaiLiDrvConfigProvider()
        {
            InitializeConfig();
        }

        /// <summary>
        /// 初始化配置文件
        /// </summary>
        private void InitializeConfig()
        {
            try
            {
                // 获取HaiLiDrv.config文件路径（与主程序exe同目录）
                // 优先使用EntryAssembly（主程序），如果为空则使用ExecutingAssembly
                string exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location 
                    ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                
                // 如果路径为空（例如在单元测试中），使用当前工作目录
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = System.IO.Directory.GetCurrentDirectory();
                }
                
                string exeDir = Path.GetDirectoryName(exePath);
                if (string.IsNullOrEmpty(exeDir))
                {
                    exeDir = System.IO.Directory.GetCurrentDirectory();
                }
                
                _configFilePath = Path.Combine(exeDir, "HaiLiDrv.config");

                Console.WriteLine($"[HaiLiDrvConfigProvider] 配置文件路径: {_configFilePath}");

                // 如果配置文件不存在，创建默认配置
                if (!File.Exists(_configFilePath))
                {
                    Console.WriteLine($"[HaiLiDrvConfigProvider] 配置文件不存在，正在创建...");
                    CreateDefaultConfig(_configFilePath);
                    
                    // 验证文件是否成功创建
                    if (!File.Exists(_configFilePath))
                    {
                        throw new IOException($"配置文件创建失败: {_configFilePath}");
                    }
                    Console.WriteLine($"[HaiLiDrvConfigProvider] 配置文件创建成功");
                }
                else
                {
                    Console.WriteLine($"[HaiLiDrvConfigProvider] 配置文件已存在");
                }

                // 加载配置文件
                var fileMap = new ExeConfigurationFileMap
                {
                    ExeConfigFilename = _configFilePath
                };
                _config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                Console.WriteLine($"[HaiLiDrvConfigProvider] 配置文件加载成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvConfigProvider] 初始化配置失败: {ex.Message}");
                Console.WriteLine($"[HaiLiDrvConfigProvider] 堆栈跟踪: {ex.StackTrace}");
                // 如果加载失败，使用主程序的App.config作为后备
                try
                {
                    _config = System.Configuration.ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    Console.WriteLine($"[HaiLiDrvConfigProvider] 已回退到主程序配置");
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"[HaiLiDrvConfigProvider] 回退配置也失败: {fallbackEx.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 创建默认配置文件
        /// </summary>
        private void CreateDefaultConfig(string configPath)
        {
            try
            {
                // 确保目录存在
                string configDir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    Console.WriteLine($"[HaiLiDrvConfigProvider] 已创建配置目录: {configDir}");
                }

                string defaultConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <!-- HaiLiDrv窗口位置与大小 -->
    <add key=""HaiLiDrvWindow_Left"" value=""1920"" />
    <add key=""HaiLiDrvWindow_Top"" value=""0"" />
    <add key=""HaiLiDrvWindow_Width"" value=""1920"" />
    <add key=""HaiLiDrvWindow_Height"" value=""1080"" />
    
    <!-- 数据刷新间隔（秒） -->
    <add key=""HaiLiDrv_RefreshIntervalSeconds"" value=""3"" />
    
    <!-- 显示数据条数限制 -->
    <add key=""HaiLiDrv_MaxDisplayCount"" value=""500"" />
    
    <!-- 数据库配置（与主程序共享） -->
    <add key=""DatabaseHost"" value=""localhost"" />
    <add key=""DatabasePort"" value=""8532"" />
    <add key=""DatabaseName"" value=""stockdb"" />
    <add key=""DatabaseUser"" value=""postgres"" />
    <add key=""DatabasePassword"" value="""" />
    
    <!-- MQ服务配置（用于连接主程序的数据服务） -->
    <add key=""MQPort"" value=""5678"" />
    <add key=""MQHost"" value=""localhost"" />
    
    <!-- K线图窗口位置（独立保存） -->
    <add key=""HaiLiDrv_ChartWindow_Left"" value=""3000"" />
    <add key=""HaiLiDrv_ChartWindow_Top"" value=""100"" />
    <add key=""HaiLiDrv_ChartWindow_Width"" value=""1400"" />
    <add key=""HaiLiDrv_ChartWindow_Height"" value=""950"" />
    
    <!-- 指定显示的股票代码列表（逗号分隔，为空则显示全部） -->
    <!-- 示例：600000,600001,000001,000002 -->
    <add key=""HaiLiDrv_StockCodes"" value="""" />
    
    <!-- 是否启用股票代码过滤（true=只显示配置的股票，false=显示全部） -->
    <add key=""HaiLiDrv_EnableStockCodeFilter"" value=""false"" />
    
    <!-- 面板数量配置（1-200，默认1） -->
    <add key=""HaiLiDrv_PanelCount"" value=""1"" />
    
    <!-- 面板配置示例（面板1的股票代码列表，其他面板类似） -->
    <!-- <add key=""HaiLiDrv_Panel1_StockCodes"" value=""600000,600001,000001"" /> -->
    <!-- <add key=""HaiLiDrv_Panel1_Width"" value=""400"" /> -->
    <!-- <add key=""HaiLiDrv_Panel1_Height"" value=""300"" /> -->
  </appSettings>
</configuration>";

                File.WriteAllText(configPath, defaultConfig, System.Text.Encoding.UTF8);
                Console.WriteLine($"[HaiLiDrvConfigProvider] 已创建默认配置文件: {configPath}");
                
                // 验证文件是否成功写入
                if (!File.Exists(configPath))
                {
                    throw new IOException($"配置文件写入后验证失败: {configPath}");
                }
                
                // 验证文件内容
                string content = File.ReadAllText(configPath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new IOException($"配置文件内容为空: {configPath}");
                }
                Console.WriteLine($"[HaiLiDrvConfigProvider] 配置文件验证成功，文件大小: {new FileInfo(configPath).Length} 字节");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HaiLiDrvConfigProvider] 创建默认配置失败: {ex.Message}");
                Console.WriteLine($"[HaiLiDrvConfigProvider] 堆栈跟踪: {ex.StackTrace}");
                throw; // 重新抛出异常，让调用者知道创建失败
            }
        }

        /// <summary>
        /// 获取字符串配置
        /// </summary>
        public string GetString(string key, string defaultValue = null)
        {
            try
            {
                var value = _config.AppSettings.Settings[key]?.Value;
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 获取整数配置
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            var value = GetString(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取十进制数配置
        /// </summary>
        public decimal GetDecimal(string key, decimal defaultValue = 0)
        {
            var value = GetString(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return decimal.TryParse(value, out decimal result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取布尔配置
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            var value = GetString(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取可空十进制数配置
        /// </summary>
        public decimal? GetNullableDecimal(string key)
        {
            var value = GetString(key);
            if (string.IsNullOrEmpty(value))
                return null;

            return decimal.TryParse(value, out decimal result) ? result : (decimal?)null;
        }

        /// <summary>
        /// 配置是否存在
        /// </summary>
        public bool HasKey(string key)
        {
            return _config.AppSettings.Settings[key] != null;
        }

        /// <summary>
        /// 获取数据库配置
        /// </summary>
        public DatabaseConfig GetDatabaseConfig()
        {
            return new DatabaseConfig
            {
                Host = GetString("DatabaseHost", "localhost"),
                Port = GetInt("DatabasePort", 8532),
                Database = GetString("DatabaseName", "stockdb"),
                Username = GetString("DatabaseUser", "postgres"),
                Password = GetString("DatabasePassword", "password"),
                MinPoolSize = GetInt("DatabaseMinPoolSize", 5),
                MaxPoolSize = GetInt("DatabaseMaxPoolSize", 20),
                ConnectionLifetime = GetInt("DatabaseConnectionLifetime", 300),
                CommandTimeout = GetInt("DatabaseCommandTimeout", 30)
            };
        }

        /// <summary>
        /// 获取Redis配置
        /// </summary>
        public RedisConfig GetRedisConfig()
        {
            return new RedisConfig
            {
                Host = GetString("RedisHost", "localhost"),
                Port = GetInt("RedisPort", 6379),
                Password = GetString("RedisPassword"),
                Database = GetInt("RedisDatabase", 0),
                ConnectTimeout = GetInt("RedisConnectTimeout", 5000),
                Enabled = GetBool("RedisEnabled", true)
            };
        }

        /// <summary>
        /// 获取MQ配置（用于连接主程序的数据服务）
        /// </summary>
        public MQConfig GetMQConfig()
        {
            return new MQConfig
            {
                Host = GetString("MQHost", "localhost"),
                Port = GetInt("MQPort", 5678),
                Username = GetString("MQUsername", ""),
                Password = GetString("MQPassword", ""),
                VirtualHost = GetString("MQVirtualHost", "/"),
                QueueName = GetString("MQQueueName", "daily_data_queue"),
                ExchangeName = GetString("MQExchangeName", ""),
                RoutingKey = GetString("MQRoutingKey", "")
            };
        }

        /// <summary>
        /// 保存配置值（线程安全）
        /// </summary>
        public void SetValue(string key, string value)
        {
            lock (_lock)
            {
                try
                {
                    if (_config.AppSettings.Settings[key] != null)
                    {
                        _config.AppSettings.Settings[key].Value = value;
                    }
                    else
                    {
                        _config.AppSettings.Settings.Add(key, value);
                    }
                    _config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HaiLiDrvConfigProvider] 保存配置失败: {ex.Message}");
                    throw; // 重新抛出异常，让调用者知道保存失败
                }
            }
        }

        /// <summary>
        /// 保存十进制数配置
        /// </summary>
        public void SetDecimal(string key, decimal value)
        {
            SetValue(key, value.ToString());
        }

        /// <summary>
        /// 获取过滤器配置（HaiLiDrv不需要，但实现接口）
        /// </summary>
        public FilterConfig GetFilterConfig()
        {
            // HaiLiDrv不执行过滤，返回默认配置
            return new FilterConfig();
        }
    }
}
