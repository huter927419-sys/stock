# 从 Boards.json 提取所有股票代码并生成 SQL 查询文件
# 使用方法: .\extract_boards_codes.ps1

$ErrorActionPreference = "Stop"

$boardsJsonPath = "bin\x64\Debug\Config\Boards.json"
$outputSqlPath = "db\check_boards_stocks_full.sql"

if (-not (Test-Path $boardsJsonPath)) {
    Write-Host "错误: 找不到文件 $boardsJsonPath" -ForegroundColor Red
    exit 1
}

Write-Host "正在读取 $boardsJsonPath..." -ForegroundColor Cyan

# 读取并解析 JSON
$jsonContent = Get-Content $boardsJsonPath -Raw -Encoding UTF8
$boards = $jsonContent | ConvertFrom-Json

# 收集所有股票代码（规范化：移除 SH/SZ 前缀）
$allStockCodes = @{}
foreach ($board in $boards) {
    $boardName = $board.Name
    if ($board.StockCodes) {
        foreach ($code in $board.StockCodes) {
            # 规范化代码：移除 SH/SZ 前缀，只保留6位数字
            $normalizedCode = $code.Trim().ToUpper()
            if ($normalizedCode.StartsWith("SH") -or $normalizedCode.StartsWith("SZ")) {
                $normalizedCode = $normalizedCode.Substring(2)
            }
            
            # 只处理6位数字代码
            if ($normalizedCode -match '^\d{6}$') {
                if (-not $allStockCodes.ContainsKey($normalizedCode)) {
                    $allStockCodes[$normalizedCode] = @{
                        OriginalCode = $code
                        Boards = @()
                    }
                }
                $allStockCodes[$normalizedCode].Boards += $boardName
            }
        }
    }
}

$totalCodes = $allStockCodes.Count
Write-Host "提取了 $totalCodes 个唯一股票代码" -ForegroundColor Green

# 生成 SQL 文件
$sqlCodes = ($allStockCodes.Keys | Sort-Object | ForEach-Object { "'$_'" }) -join ",`n        "

$sql = @"
-- 检查 Boards.json 中的所有股票代码是否在数据库中
-- 自动生成时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
-- 总代码数: $totalCodes

WITH codes AS (
    SELECT unnest(ARRAY[
        $sqlCodes
    ]) AS stock_code
)
SELECT
    c.stock_code,
    CASE 
        WHEN si.stock_code IS NULL THEN '不存在'
        WHEN si.is_active = FALSE THEN '未激活'
        ELSE '已激活'
    END AS status,
    COALESCE(si.stock_name, '-') AS stock_name,
    COALESCE(si.market_code::text, '-') AS market_code,
    COALESCE(si.market_name, '-') AS market_name,
    CASE 
        WHEN EXISTS (SELECT 1 FROM stock_daily_data WHERE stock_code = c.stock_code LIMIT 1) THEN '有日线数据'
        ELSE '无日线数据'
    END AS has_daily_data,
    CASE 
        WHEN c.stock_code ~ '^(600|601|603|605|688)' THEN '沪市主板/科创板'
        WHEN c.stock_code ~ '^(000|001|002|003|004)' THEN '深市主板/中小板'
        WHEN c.stock_code ~ '^(300|301)' THEN '创业板'
        WHEN c.stock_code ~ '^(920|43|83|87|88)' THEN '北交所/其他'
        ELSE '未知'
    END AS stock_type
FROM codes c
LEFT JOIN stock_info si ON c.stock_code = si.stock_code
ORDER BY 
    CASE 
        WHEN si.stock_code IS NULL THEN 1
        WHEN si.is_active = FALSE THEN 2
        ELSE 3
    END,
    c.stock_code;

-- 统计摘要
WITH codes AS (
    SELECT unnest(ARRAY[
        $sqlCodes
    ]) AS stock_code
)
SELECT
    COUNT(*) AS total_codes,
    COUNT(si.stock_code) AS found_in_stock_info,
    COUNT(CASE WHEN si.is_active = TRUE THEN 1 END) AS active_codes,
    COUNT(CASE WHEN si.stock_code IS NULL THEN 1 END) AS not_found,
    COUNT(CASE WHEN EXISTS (SELECT 1 FROM stock_daily_data WHERE stock_code = c.stock_code LIMIT 1) THEN 1 END) AS has_daily_data
FROM codes c
LEFT JOIN stock_info si ON c.stock_code = si.stock_code;
"@

# 确保输出目录存在
$outputDir = Split-Path $outputSqlPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$sql | Out-File -FilePath $outputSqlPath -Encoding UTF8 -NoNewline

Write-Host "`nSQL 文件已生成: $outputSqlPath" -ForegroundColor Green
Write-Host "`n执行查询:" -ForegroundColor Cyan
Write-Host "  psql -h localhost -p 8532 -U postgres -d stockdb -f $outputSqlPath" -ForegroundColor Yellow
