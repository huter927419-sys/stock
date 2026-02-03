using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using MQReceiver.DataProcessing.Factories;
using MQReceiver.Repositories;
using MQReceiver.Models;

namespace MQReceiver.Tools
{
    /// <summary>
    /// 检查 Boards.json 中的股票代码是否在 RocksDB 中存在
    /// </summary>
    class CheckBoardsStocksInRocksDB
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("========== 检查 Boards.json 中的股票代码 ==========\n");

                // 1. 读取 Boards.json
                string boardsJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Boards.json");
                if (!File.Exists(boardsJsonPath))
                {
                    Console.WriteLine($"错误: 找不到文件 {boardsJsonPath}");
                    return;
                }

                Console.WriteLine($"正在读取: {boardsJsonPath}");
                string jsonContent = File.ReadAllText(boardsJsonPath, System.Text.Encoding.UTF8);
                var boards = JsonSerializer.Deserialize<List<BoardConfig>>(jsonContent);

                // 2. 提取所有股票代码（规范化）
                var allStockCodes = new Dictionary<string, StockCodeInfo>();
                foreach (var board in boards)
                {
                    if (board.StockCodes != null)
                    {
                        foreach (var code in board.StockCodes)
                        {
                            string normalizedCode = NormalizeStockCode(code);
                            if (!string.IsNullOrEmpty(normalizedCode) && normalizedCode.Length == 6)
                            {
                                if (!allStockCodes.ContainsKey(normalizedCode))
                                {
                                    allStockCodes[normalizedCode] = new StockCodeInfo
                                    {
                                        OriginalCode = code,
                                        Boards = new List<string>()
                                    };
                                }
                                allStockCodes[normalizedCode].Boards.Add(board.Name);
                            }
                        }
                    }
                }

                Console.WriteLine($"从 Boards.json 提取了 {allStockCodes.Count} 个唯一股票代码\n");

                // 3. 初始化 RocksDB 仓库
                Console.WriteLine("正在初始化 RocksDB 仓库...");
                var repository = RepositoryFactory.GetStockDataRepository();
                var allStockNames = repository.GetAllStockNames();
                var allCodesInRocksDB = repository.GetAllStockCodes();

                Console.WriteLine($"RocksDB 中有 {allCodesInRocksDB.Count} 个股票代码");
                Console.WriteLine($"RocksDB 中有 {allStockNames.Count} 个股票名称\n");

                // 4. 检查每个代码
                var foundCodes = new List<StockCheckResult>();
                var notFoundCodes = new List<StockCheckResult>();
                var hasDataCodes = new List<StockCheckResult>();
                var noDataCodes = new List<StockCheckResult>();

                foreach (var kvp in allStockCodes)
                {
                    string code = kvp.Key;
                    var info = kvp.Value;

                    bool exists = allCodesInRocksDB.Contains(code);
                    bool hasName = allStockNames.ContainsKey(code);
                    string stockName = hasName ? allStockNames[code] : "-";
                    bool hasData = repository.HasData(code);

                    var result = new StockCheckResult
                    {
                        StockCode = code,
                        OriginalCode = info.OriginalCode,
                        Boards = string.Join(", ", info.Boards),
                        Exists = exists,
                        HasName = hasName,
                        StockName = stockName,
                        HasData = hasData
                    };

                    if (exists)
                    {
                        foundCodes.Add(result);
                        if (hasData)
                            hasDataCodes.Add(result);
                        else
                            noDataCodes.Add(result);
                    }
                    else
                    {
                        notFoundCodes.Add(result);
                    }
                }

                // 5. 显示结果
                Console.WriteLine("========== 检查结果 ==========\n");

                Console.WriteLine($"已找到: {foundCodes.Count} 个 ({foundCodes.Count * 100.0 / allStockCodes.Count:F2}%)");
                Console.WriteLine($"  其中 {hasDataCodes.Count} 个有日线数据");
                Console.WriteLine($"  其中 {noDataCodes.Count} 个无日线数据");
                Console.WriteLine($"不存在: {notFoundCodes.Count} 个 ({notFoundCodes.Count * 100.0 / allStockCodes.Count:F2}%)\n");

                if (notFoundCodes.Count > 0)
                {
                    Console.WriteLine("========== 不存在的股票代码 ==========");
                    Console.WriteLine("代码       | 原始代码      | 板块");
                    Console.WriteLine(new string('-', 80));
                    foreach (var item in notFoundCodes.OrderBy(x => x.StockCode))
                    {
                        Console.WriteLine($"{item.StockCode,-10} | {item.OriginalCode,-14} | {item.Boards}");
                    }
                    Console.WriteLine();
                }

                if (noDataCodes.Count > 0)
                {
                    Console.WriteLine("========== 存在但无日线数据的股票代码 ==========");
                    Console.WriteLine("代码       | 原始代码      | 股票名称      | 板块");
                    Console.WriteLine(new string('-', 100));
                    foreach (var item in noDataCodes.OrderBy(x => x.StockCode))
                    {
                        Console.WriteLine($"{item.StockCode,-10} | {item.OriginalCode,-14} | {item.StockName,-14} | {item.Boards}");
                    }
                    Console.WriteLine();
                }

                // 6. 显示前20个找到的代码作为示例
                if (hasDataCodes.Count > 0)
                {
                    Console.WriteLine("========== 已找到且有数据的股票代码（前20个） ==========");
                    Console.WriteLine("代码       | 原始代码      | 股票名称      | 板块");
                    Console.WriteLine(new string('-', 100));
                    foreach (var item in hasDataCodes.OrderBy(x => x.StockCode).Take(20))
                    {
                        Console.WriteLine($"{item.StockCode,-10} | {item.OriginalCode,-14} | {item.StockName,-14} | {item.Boards}");
                    }
                    if (hasDataCodes.Count > 20)
                    {
                        Console.WriteLine($"... 还有 {hasDataCodes.Count - 20} 个");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine("检查完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// 规范化股票代码：移除市场前缀（SH/SZ），只保留6位数字
        /// </summary>
        static string NormalizeStockCode(string stockCode)
        {
            if (string.IsNullOrEmpty(stockCode))
                return stockCode;

            string normalized = stockCode.Trim().ToUpper();
            if (normalized.StartsWith("SH") || normalized.StartsWith("SZ"))
            {
                normalized = normalized.Substring(2);
            }

            // 验证是否为6位数字
            if (normalized.Length == 6 && Regex.IsMatch(normalized, @"^\d{6}$"))
            {
                return normalized;
            }

            return stockCode;
        }
    }

    class BoardConfig
    {
        public string Name { get; set; }
        public List<string> StockCodes { get; set; }
        public Dictionary<string, string> StockNames { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    class StockCodeInfo
    {
        public string OriginalCode { get; set; }
        public List<string> Boards { get; set; }
    }

    class StockCheckResult
    {
        public string StockCode { get; set; }
        public string OriginalCode { get; set; }
        public string Boards { get; set; }
        public bool Exists { get; set; }
        public bool HasName { get; set; }
        public string StockName { get; set; }
        public bool HasData { get; set; }
    }
}
