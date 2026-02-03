# 检查 Boards.json 中的股票代码是否在数据库中
# 使用方法: .\check_boards_stocks.ps1

$ErrorActionPreference = "Stop"

# 数据库连接配置（从 App.config 读取）
$dbHost = "localhost"
$dbPort = "8532"
$dbName = "stockdb"
$dbUser = "postgres"
$dbPassword = "cd123321"

# Boards.json 路径
$boardsJsonPath = "bin\x64\Debug\Config\Boards.json"

if (-not (Test-Path $boardsJsonPath)) {
    Write-Host "错误: 找不到文件 $boardsJsonPath" -ForegroundColor Red
    exit 1
}

Write-Host "正在读取 $boardsJsonPath..." -ForegroundColor Cyan

# 读取并解析 JSON
$boardsJson = Get-Content $boardsJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

# 收集所有股票代码（规范化：移除 SH/SZ 前缀）
$allStockCodes = @{}
foreach ($board in $boardsJson) {
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
Write-Host "`n从 Boards.json 中提取了 $totalCodes 个唯一股票代码" -ForegroundColor Green

# 构建 PostgreSQL 连接字符串
$connectionString = "Host=$dbHost;Port=$dbPort;Database=$dbName;Username=$dbUser;Password=$dbPassword"

# 检查是否安装了 Npgsql（PostgreSQL .NET 驱动）
try {
    Add-Type -Path "packages\Npgsql.*\lib\net*\Npgsql.dll" -ErrorAction SilentlyContinue
    if (-not ([System.ManagedIpsum.ManagedIpsum]::GetType().Assembly.GetTypes() | Where-Object { $_.FullName -eq "Npgsql.NpgsqlConnection" })) {
        # 尝试从 bin 目录加载
        $npgsqlPath = Get-ChildItem -Path "bin\x64\Debug" -Filter "Npgsql.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($npgsqlPath) {
            Add-Type -Path $npgsqlPath.FullName
        } else {
            Write-Host "`n警告: 无法加载 Npgsql.dll，将使用 psql 命令行工具" -ForegroundColor Yellow
            $usePsql = $true
        }
    }
} catch {
    Write-Host "`n警告: 无法加载 Npgsql.dll，将使用 psql 命令行工具" -ForegroundColor Yellow
    $usePsql = $true
}

if ($usePsql) {
    # 使用 psql 命令行工具
    Write-Host "`n使用 psql 查询数据库..." -ForegroundColor Cyan
    
    # 创建临时 SQL 文件
    $tempSqlFile = [System.IO.Path]::GetTempFileName() + ".sql"
    
    # 构建 SQL 查询
    $sqlCodes = ($allStockCodes.Keys | ForEach-Object { "'$_'" }) -join ","
    $sql = @"
-- 检查 Boards.json 中的股票代码是否在数据库中
WITH codes AS (
    SELECT unnest(ARRAY[$sqlCodes]) AS stock_code
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
ORDER BY c.stock_code;
"@
    
    $sql | Out-File -FilePath $tempSqlFile -Encoding UTF8
    
    # 执行查询
    $env:PGPASSWORD = $dbPassword
    $psqlCmd = "psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -f `"$tempSqlFile`""
    
    Write-Host "执行查询..." -ForegroundColor Cyan
    & cmd /c $psqlCmd
    
    # 清理临时文件
    Remove-Item $tempSqlFile -ErrorAction SilentlyContinue
    Remove-Item env:PGPASSWORD
    
} else {
    # 使用 .NET Npgsql
    Write-Host "`n使用 .NET Npgsql 查询数据库..." -ForegroundColor Cyan
    
    try {
        $conn = New-Object Npgsql.NpgsqlConnection($connectionString)
        $conn.Open()
        
        # 构建 SQL 查询
        $sqlCodes = ($allStockCodes.Keys | ForEach-Object { "'$_'" }) -join ","
        $sql = @"
WITH codes AS (
    SELECT unnest(ARRAY[$sqlCodes]) AS stock_code
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
ORDER BY c.stock_code;
"@
        
        $cmd = New-Object Npgsql.NpgsqlCommand($sql, $conn)
        $reader = $cmd.ExecuteReader()
        
        $results = @()
        while ($reader.Read()) {
            $results += [PSCustomObject]@{
                StockCode = $reader["stock_code"].ToString()
                Status = $reader["status"].ToString()
                StockName = $reader["stock_name"].ToString()
                MarketCode = $reader["market_code"].ToString()
                MarketName = $reader["market_name"].ToString()
                HasDailyData = $reader["has_daily_data"].ToString()
                StockType = $reader["stock_type"].ToString()
                OriginalCode = $allStockCodes[$reader["stock_code"].ToString()].OriginalCode
                Boards = ($allStockCodes[$reader["stock_code"].ToString()].Boards -join ", ")
            }
        }
        
        $reader.Close()
        $conn.Close()
        
        # 显示结果
        Write-Host "`n查询结果:" -ForegroundColor Green
        Write-Host "=" * 120
        
        $notFound = @()
        $inactive = @()
        $found = @()
        
        foreach ($result in $results) {
            if ($result.Status -eq "不存在") {
                $notFound += $result
            } elseif ($result.Status -eq "未激活") {
                $inactive += $result
            } else {
                $found += $result
            }
        }
        
        Write-Host "`n已找到并激活: $($found.Count) 个" -ForegroundColor Green
        if ($found.Count -gt 0) {
            $found | Format-Table -AutoSize
        }
        
        Write-Host "`n未激活: $($inactive.Count) 个" -ForegroundColor Yellow
        if ($inactive.Count -gt 0) {
            $inactive | Format-Table -AutoSize
        }
        
        Write-Host "`n不存在: $($notFound.Count) 个" -ForegroundColor Red
        if ($notFound.Count -gt 0) {
            Write-Host "这些代码在数据库中不存在:" -ForegroundColor Red
            foreach ($item in $notFound) {
                Write-Host "  $($item.OriginalCode) -> $($item.StockCode) (出现在板块: $($item.Boards))" -ForegroundColor Red
            }
        }
        
        # 统计摘要
        Write-Host "`n统计摘要:" -ForegroundColor Cyan
        Write-Host "  总代码数: $totalCodes"
        Write-Host "  已找到: $($found.Count) ($([math]::Round($found.Count / $totalCodes * 100, 2))%)"
        Write-Host "  未激活: $($inactive.Count) ($([math]::Round($inactive.Count / $totalCodes * 100, 2))%)"
        Write-Host "  不存在: $($notFound.Count) ($([math]::Round($notFound.Count / $totalCodes * 100, 2))%)"
        
    } catch {
        Write-Host "`n错误: 无法查询数据库" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n检查完成!" -ForegroundColor Green
