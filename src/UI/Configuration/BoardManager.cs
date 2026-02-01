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
                if (!File.Exists(filePath))
                    return new List<BoardConfig>();

                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var list = JsonConvert.DeserializeObject<List<BoardConfig>>(json);
                _boards = list ?? new List<BoardConfig>();
                return new List<BoardConfig>(_boards);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BoardManager] 加载失败: {ex.Message}");
                return new List<BoardConfig>();
            }
        }
    }
}
