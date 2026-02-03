# 检查 Boards.json 中的股票代码是否在 RocksDB 中存在
# 使用方法: .\check_boards_in_rocksdb.ps1

$ErrorActionPreference = "Stop"

# 配置路径
$boardsJsonPath = "bin\x64\Debug\Config\Boards.json"
$rocksDBPath = "bin\x64\Debug\data\rocksdb\kline"

if (-not (Test-Path $boardsJsonPath)) {
    Write-Host "错误: 找不到文件 $boardsJsonPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $rocksDBPath)) {
    Write-Host "错误: 找不到 RocksDB 目录 $rocksDBPath" -ForegroundColor Red
    Write-Host "请确认 RocksDBPath 配置正确" -ForegroundColor Yellow
    exit 1
}

Write-Host "========== 检查 Boards.json 中的股票代码 ==========" -ForegroundColor Cyan
Write-Host ""

# 1. 读取 Boards.json
Write-Host "正在读取 Boards.json..." -ForegroundColor Cyan
$jsonContent = Get-Content $boardsJsonPath -Raw -Encoding UTF8
$boards = $jsonContent | ConvertFrom-Json

# 2. 提取所有股票代码（规范化）
$allStockCodes = @{}
foreach ($board in $boards) {
    if ($board.StockCodes) {
        foreach ($code in $board.StockCodes) {
            # 规范化代码：移除 SH/SZ 前缀
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
                $allStockCodes[$normalizedCode].Boards += $board.Name
            }
        }
    }
}

$totalCodes = $allStockCodes.Count
Write-Host "从 Boards.json 提取了 $totalCodes 个唯一股票代码" -ForegroundColor Green
Write-Host ""

# 3. 读取 RocksDB 中的股票代码（从 kline 目录的 JSON 文件）
Write-Host "正在扫描 RocksDB 目录..." -ForegroundColor Cyan
$rocksDBCodes = @{}
if (Test-Path $rocksDBPath) {
    $jsonFiles = Get-ChildItem -Path $rocksDBPath -Filter "*.json" -ErrorAction SilentlyContinue
    foreach ($file in $jsonFiles) {
        $code = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        if ($code -match '^\d{6}$') {
            $rocksDBCodes[$code] = $true
        }
    }
}

$rocksDBCount = $rocksDBCodes.Count
Write-Host "RocksDB 中找到 $rocksDBCount 个股票代码文件" -ForegroundColor Green
Write-Host ""

# 4. 检查每个代码
$foundCodes = @()
$notFoundCodes = @()

foreach ($code in $allStockCodes.Keys) {
    if ($rocksDBCodes.ContainsKey($code)) {
        $foundCodes += @{
            Code = $code
            OriginalCode = $allStockCodes[$code].OriginalCode
            Boards = $allStockCodes[$code].Boards -join ", "
        }
    } else {
        $notFoundCodes += @{
            Code = $code
            OriginalCode = $allStockCodes[$code].OriginalCode
            Boards = $allStockCodes[$code].Boards -join ", "
        }
    }
}

# 5. 显示结果
Write-Host "========== 检查结果 ==========" -ForegroundColor Cyan
Write-Host ""
Write-Host "已找到: $($foundCodes.Count) 个 ($([math]::Round($foundCodes.Count / $totalCodes * 100, 2))%)" -ForegroundColor Green
Write-Host "不存在: $($notFoundCodes.Count) 个 ($([math]::Round($notFoundCodes.Count / $totalCodes * 100, 2))%)" -ForegroundColor $(if ($notFoundCodes.Count -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($notFoundCodes.Count -gt 0) {
    Write-Host "========== 不存在的股票代码（前50个） ==========" -ForegroundColor Yellow
    Write-Host "代码       | 原始代码      | 板块"
    Write-Host ("-" * 80)
    $displayCount = [Math]::Min(50, $notFoundCodes.Count)
    for ($i = 0; $i -lt $displayCount; $i++) {
        $item = $notFoundCodes[$i]
        Write-Host "$($item.Code.PadRight(10)) | $($item.OriginalCode.PadRight(14)) | $($item.Boards)"
    }
    if ($notFoundCodes.Count -gt 50) {
        Write-Host "... 还有 $($notFoundCodes.Count - 50) 个"
    }
    Write-Host ""
}

# 6. 显示前20个找到的代码作为示例
if ($foundCodes.Count -gt 0) {
    Write-Host "========== 已找到的股票代码（前20个） ==========" -ForegroundColor Green
    Write-Host "代码       | 原始代码      | 板块"
    Write-Host ("-" * 80)
    $displayCount = [Math]::Min(20, $foundCodes.Count)
    for ($i = 0; $i -lt $displayCount; $i++) {
        $item = $foundCodes[$i]
        Write-Host "$($item.Code.PadRight(10)) | $($item.OriginalCode.PadRight(14)) | $($item.Boards)"
    }
    if ($foundCodes.Count -gt 20) {
        Write-Host "... 还有 $($foundCodes.Count - 20) 个"
    }
    Write-Host ""
}

Write-Host "检查完成！" -ForegroundColor Green
