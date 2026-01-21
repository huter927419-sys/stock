# 除权数据和复权价格检查脚本
# PowerShell版本 - 使用Npgsql.dll直接查询数据库

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  除权数据和复权价格检查工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 加载Npgsql.dll
try {
    Add-Type -Path ".\bin\Debug\Npgsql.dll"
    Write-Host "✓ 加载Npgsql.dll成功" -ForegroundColor Green
} catch {
    Write-Host "✗ 加载Npgsql.dll失败: $_" -ForegroundColor Red
    Read-Host "按回车键退出"
    exit
}

# 构建连接字符串
$connString = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321;Timeout=30"

Write-Host "正在连接数据库: localhost:8532/stockdb" -ForegroundColor Yellow
Write-Host ""

try {
    $conn = New-Object Npgsql.NpgsqlConnection($connString)
    $conn.Open()
    Write-Host "✓ 数据库连接成功" -ForegroundColor Green
    Write-Host ""

    # ========================================
    # 1. 检查除权数据表
    # ========================================
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "1. 检查除权数据表 (stock_exrights_data)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    # 检查表是否存在
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'stock_exrights_data')"
    $tableExists = $cmd.ExecuteScalar()

    if ($tableExists) {
        Write-Host "✓ 除权数据表存在" -ForegroundColor Green

        # 统计除权数据
        $cmd.CommandText = @"
SELECT
    COUNT(*) as total_count,
    COUNT(DISTINCT stock_code) as stock_count,
    MIN(ex_rights_date) as earliest_date,
    MAX(ex_rights_date) as latest_date
FROM stock_exrights_data
"@
        $reader = $cmd.ExecuteReader()
        if ($reader.Read()) {
            $totalCount = $reader["total_count"]
            $stockCount = if ($reader.IsDBNull(1)) { 0 } else { $reader["stock_count"] }

            Write-Host "  总记录数: $($totalCount.ToString('N0'))" -ForegroundColor White
            Write-Host "  股票数量: $($stockCount.ToString('N0'))" -ForegroundColor White

            if (-not $reader.IsDBNull(2)) {
                $earliest = $reader["earliest_date"]
                $latest = $reader["latest_date"]
                Write-Host "  日期范围: $($earliest.ToString('yyyy-MM-dd')) ~ $($latest.ToString('yyyy-MM-dd'))" -ForegroundColor White
            }

            if ($totalCount -eq 0) {
                Write-Host ""
                Write-Host "⚠ 警告: 除权数据表为空！" -ForegroundColor Yellow
                Write-Host "  需要先导入除权数据才能计算复权价格。" -ForegroundColor Yellow
            } else {
                Write-Host "✓ 除权数据已填充 ($($totalCount.ToString('N0')) 条记录)" -ForegroundColor Green
            }
        }
        $reader.Close()

        # 显示最近的除权数据
        Write-Host ""
        Write-Host "最近5条除权数据:" -ForegroundColor Cyan
        $cmd.CommandText = @"
SELECT stock_code, ex_rights_date, give_per_10_shares, pei_per_10_shares, pei_price, profit_per_share
FROM stock_exrights_data
ORDER BY ex_rights_date DESC
LIMIT 5
"@
        $reader = $cmd.ExecuteReader()
        $count = 0
        while ($reader.Read()) {
            $count++
            $stockCode = $reader["stock_code"]
            $exDate = $reader["ex_rights_date"]
            $give = $reader["give_per_10_shares"]
            $pei = $reader["pei_per_10_shares"]
            $peiPrice = $reader["pei_price"]
            $profit = $reader["profit_per_share"]

            $scheme = ""
            if ($give -gt 0) { $scheme += "10送$give" }
            if ($pei -gt 0) { $scheme += " 10配$pei@$peiPrice" }
            if ($profit -gt 0) { $scheme += " 派$profit" }

            Write-Host "  $stockCode $($exDate.ToString('yyyy-MM-dd')) $scheme" -ForegroundColor White
        }
        if ($count -eq 0) {
            Write-Host "  (无数据)" -ForegroundColor Gray
        }
        $reader.Close()
    } else {
        Write-Host "✗ 除权数据表不存在" -ForegroundColor Red
        Write-Host "  请运行: db/create_exrights_data_table_postgresql.sql" -ForegroundColor Yellow
    }

    Write-Host ""

    # ========================================
    # 2. 检查复权价格字段
    # ========================================
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "2. 检查复权价格字段 (stock_daily_data)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $cmd.CommandText = @"
SELECT EXISTS (
    SELECT FROM information_schema.columns
    WHERE table_name = 'stock_daily_data'
    AND column_name = 'adjusted_close_price'
)
"@
    $hasColumn = $cmd.ExecuteScalar()

    if ($hasColumn) {
        Write-Host "✓ 复权价格字段存在" -ForegroundColor Green

        # 统计复权价格数据
        $cmd.CommandText = @"
SELECT
    COUNT(*) as total_count,
    COUNT(adjusted_close_price) as adjusted_count,
    COUNT(*) - COUNT(adjusted_close_price) as unadjusted_count
FROM stock_daily_data
"@
        $reader = $cmd.ExecuteReader()
        if ($reader.Read()) {
            $totalCount = $reader["total_count"]
            $adjustedCount = $reader["adjusted_count"]
            $unadjustedCount = $reader["unadjusted_count"]
            $coverage = if ($totalCount -gt 0) { ($adjustedCount / $totalCount * 100) } else { 0 }

            Write-Host "  总记录数: $($totalCount.ToString('N0'))" -ForegroundColor White
            Write-Host "  有复权价: $($adjustedCount.ToString('N0'))" -ForegroundColor White
            Write-Host "  无复权价: $($unadjustedCount.ToString('N0'))" -ForegroundColor White
            Write-Host "  覆盖率: $($coverage.ToString('F2'))%" -ForegroundColor White

            if ($adjustedCount -eq 0) {
                Write-Host ""
                Write-Host "⚠ 警告: 尚未计算复权价格！" -ForegroundColor Yellow
                Write-Host "  需要运行复权价格计算程序。" -ForegroundColor Yellow
            } elseif ($coverage -lt 100) {
                Write-Host ""
                Write-Host "⚠ 提示: 复权价格未完全覆盖 ($($coverage.ToString('F2'))%)" -ForegroundColor Yellow
            } else {
                Write-Host "✓ 复权价格已完全计算" -ForegroundColor Green
            }
        }
        $reader.Close()

        # 显示复权价格示例
        Write-Host ""
        Write-Host "复权价格示例（前3条）:" -ForegroundColor Cyan
        $cmd.CommandText = @"
SELECT stock_code, trade_date, close_price, adjusted_close_price
FROM stock_daily_data
WHERE adjusted_close_price IS NOT NULL
ORDER BY trade_date DESC
LIMIT 3
"@
        $reader = $cmd.ExecuteReader()
        $count = 0
        while ($reader.Read()) {
            $count++
            $stockCode = $reader["stock_code"]
            $tradeDate = $reader["trade_date"]
            $closePrice = $reader["close_price"]
            $adjPrice = $reader["adjusted_close_price"]
            $diff = if ($closePrice -ne 0) { (($adjPrice - $closePrice) / $closePrice * 100) } else { 0 }

            Write-Host "  $stockCode $($tradeDate.ToString('yyyy-MM-dd')) 原价:$($closePrice.ToString('F2')) 复权:$($adjPrice.ToString('F2')) 差异:$($diff.ToString('+0.00;-0.00'))%" -ForegroundColor White
        }
        if ($count -eq 0) {
            Write-Host "  (无复权数据)" -ForegroundColor Gray
        }
        $reader.Close()
    } else {
        Write-Host "✗ 复权价格字段不存在" -ForegroundColor Red
        Write-Host "  请运行: db/add_adjusted_price_columns.sql" -ForegroundColor Yellow
    }

    Write-Host ""

    # ========================================
    # 3. 建议
    # ========================================
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "3. 建议" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $cmd.CommandText = @"
SELECT
    (SELECT COUNT(*) FROM stock_exrights_data) as exrights_count,
    (SELECT COUNT(adjusted_close_price) FROM stock_daily_data) as adjusted_count
"@
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        $exRightsCount = $reader["exrights_count"]
        $adjustedCount = $reader["adjusted_count"]

        if ($exRightsCount -gt 0 -and $adjustedCount -eq 0) {
            Write-Host "📋 下一步操作:" -ForegroundColor Yellow
            Write-Host "  1. 除权数据已有，但复权价格未计算" -ForegroundColor White
            Write-Host "  2. 建议运行复权价格计算程序" -ForegroundColor White
            Write-Host "  3. 计算完成后，修改 PostgresKlineDataRepository.cs" -ForegroundColor White
            Write-Host "     将 open_price 改为 COALESCE(adjusted_open_price, open_price)" -ForegroundColor Gray
        } elseif ($exRightsCount -eq 0) {
            Write-Host "📋 下一步操作:" -ForegroundColor Yellow
            Write-Host "  1. 需要先导入除权数据到 stock_exrights_data 表" -ForegroundColor White
            Write-Host "  2. 然后运行复权价格计算程序" -ForegroundColor White
            Write-Host "  3. 最后修改代码启用复权数据" -ForegroundColor White
        } elseif ($adjustedCount -gt 0) {
            Write-Host "✓ 复权数据完备，可以启用复权价格用于KD计算" -ForegroundColor Green
            Write-Host ""
            Write-Host "📋 启用方法:" -ForegroundColor Yellow
            Write-Host "  修改 PostgresKlineDataRepository.cs:" -ForegroundColor White
            Write-Host "  FROM:" -ForegroundColor Gray
            Write-Host "    SELECT trade_date, open_price, high_price, ..." -ForegroundColor Gray
            Write-Host "  TO:" -ForegroundColor Gray
            Write-Host "    SELECT trade_date," -ForegroundColor Gray
            Write-Host "           COALESCE(adjusted_open_price, open_price) as open_price," -ForegroundColor Gray
            Write-Host "           COALESCE(adjusted_high_price, high_price) as high_price," -ForegroundColor Gray
            Write-Host "           ..." -ForegroundColor Gray
        }
    }
    $reader.Close()

    $conn.Close()

} catch {
    Write-Host "✗ 错误: $_" -ForegroundColor Red
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "检查完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Read-Host "按回车键退出"
