using System.Collections.Generic;

namespace MQReceiver.UI.Configuration
{
    /// <summary>
    /// 板块配置（与 MairuiStockMonitor Boards.json 结构一致）
    /// </summary>
    public class BoardConfig
    {
        public string Name { get; set; }
        public List<string> StockCodes { get; set; }
        public Dictionary<string, string> StockNames { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public BoardConfig()
        {
            Name = "板块1";
            StockCodes = new List<string>();
            StockNames = new Dictionary<string, string>();
            Width = 0;
            Height = 0;
        }

        public BoardConfig(string name) : this()
        {
            Name = name;
        }
    }
}
