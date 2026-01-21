# Load Npgsql
Add-Type -Path "F:\dsfr\mqq\bin\Release\Npgsql.dll"

$connStr = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321"

try {
    $conn = New-Object Npgsql.NpgsqlConnection($connStr)
    $conn.Open()
    Write-Host "=== Database Connected ===" -ForegroundColor Green
    Write-Host ""

    # 1. Check table counts
    Write-Host "=== Table Counts ===" -ForegroundColor Cyan
    $tables = @("stock_info", "stock_daily_data", "stock_realtime_data", "stock_exrights_data")

    foreach ($t in $tables) {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM $t"
        $count = $cmd.ExecuteScalar()
        Write-Host "${t}: $count"
    }

    # 2. Check daily data date range
    Write-Host ""
    Write-Host "=== Daily Data Date Range ===" -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT MIN(trade_date), MAX(trade_date) FROM stock_daily_data"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Min Date: $($reader.GetDateTime(0).ToString('yyyy-MM-dd'))"
        Write-Host "Max Date: $($reader.GetDateTime(1).ToString('yyyy-MM-dd'))"
    }
    $reader.Close()

    # 3. Check recent 10 trading days
    Write-Host ""
    Write-Host "=== Recent 10 Trading Days ===" -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT trade_date, COUNT(DISTINCT stock_code) as stock_count FROM stock_daily_data WHERE trade_date >= CURRENT_DATE - INTERVAL '20 days' GROUP BY trade_date ORDER BY trade_date DESC LIMIT 10"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "$($reader.GetDateTime(0).ToString('yyyy-MM-dd')): $($reader.GetInt64(1)) stocks"
    }
    $reader.Close()

    # 4. Check realtime data update time
    Write-Host ""
    Write-Host "=== Realtime Data Update Time ===" -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT MIN(update_time), MAX(update_time) FROM stock_realtime_data"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        if (-not $reader.IsDBNull(0)) {
            Write-Host "Min Update: $($reader.GetDateTime(0).ToString('yyyy-MM-dd HH:mm:ss'))"
        }
        if (-not $reader.IsDBNull(1)) {
            Write-Host "Max Update: $($reader.GetDateTime(1).ToString('yyyy-MM-dd HH:mm:ss'))"
        }
    }
    $reader.Close()

    # 5. Check stock name stats
    Write-Host ""
    Write-Host "=== Stock Name Stats ===" -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) as total, SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL OR stock_name = '' THEN 1 ELSE 0 END) as missing, SUM(CASE WHEN stock_name != stock_code AND stock_name IS NOT NULL AND stock_name != '' THEN 1 ELSE 0 END) as has_name FROM stock_info"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Total: $($reader.GetInt64(0))"
        Write-Host "Missing Name: $($reader.GetInt64(1))"
        Write-Host "Has Name: $($reader.GetInt64(2))"
    }
    $reader.Close()

    # 6. Sample stock data
    Write-Host ""
    Write-Host "=== Sample Stock Data ===" -ForegroundColor Cyan
    $sampleStocks = @("000001", "600000", "300001")
    foreach ($code in $sampleStocks) {
        Write-Host ""
        Write-Host "--- $code ---" -ForegroundColor Yellow
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT trade_date, open_price, high_price, low_price, close_price, volume FROM stock_daily_data WHERE stock_code = '$code' ORDER BY trade_date DESC LIMIT 5"
        $reader = $cmd.ExecuteReader()
        while ($reader.Read()) {
            $date = $reader.GetDateTime(0).ToString('yyyy-MM-dd')
            $o = $reader.GetDecimal(1).ToString("F2")
            $h = $reader.GetDecimal(2).ToString("F2")
            $l = $reader.GetDecimal(3).ToString("F2")
            $c = $reader.GetDecimal(4).ToString("F2")
            $v = $reader.GetDecimal(5).ToString("F0")
            Write-Host "$date O:$o H:$h L:$l C:$c V:$v"
        }
        $reader.Close()
    }

    Write-Host ""
    Write-Host "=== Check Complete ===" -ForegroundColor Green

    $conn.Close()
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
