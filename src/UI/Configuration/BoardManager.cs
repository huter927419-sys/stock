using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MQReceiver.UI.Configuration
{
    /// <summary>
    /// 板块管理器 - 读写 Config/Boards.json（与 MairuiStockMonitor 一致）
    /// </summary>
    public class BoardManager
    {
        private string _configFilePath;
        private List<BoardConfig> _boards = new List<BoardConfig>();

        public string ConfigFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_configFilePath))
                {
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string configDir = Path.Combine(appDir, "Config");
                    if (!Directory.Exists(configDir))
                        Directory.CreateDirectory(configDir);
                    _configFilePath = Path.Combine(configDir, "Boards.json");
                }
                return _configFilePath;
            }
        }

        public void SaveBoards(List<BoardConfig> boardList)
        {
            try
            {
                _boards = new List<BoardConfig>(boardList);
                string json = JsonConvert.SerializeObject(_boards, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BoardManager] 保存失败: {ex.Message}");
            }
        }

        public List<BoardConfig> LoadBoards()
        {
            try
            {
                string filePath = ConfigFilePath;
                Console.WriteLine($"[BoardManager] 尝试加载配置文件: {filePath}");
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[BoardManager] 配置文件不存在: {filePath}");
                    return new List<BoardConfig>();
                }

                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var list = JsonConvert.DeserializeObject<List<BoardConfig>>(json);
                _boards = list ?? new List<BoardConfig>();
                Console.WriteLine($"[BoardManager] 成功加载 {_boards.Count} 个板块配置");
                if (_boards.Count > 0)
                {
                    Console.WriteLine($"[BoardManager] 第一个板块: {_boards[0].Name}, 包含 {_boards[0].StockCodes?.Count ?? 0} 个股票代码");
                }
                return new List<BoardConfig>(_boards);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BoardManager] 加载失败: {ex.Message}");
                Console.WriteLine($"[BoardManager] 异常堆栈: {ex.StackTrace}");
                return new List<BoardConfig>();
            }
        }
    }
}
