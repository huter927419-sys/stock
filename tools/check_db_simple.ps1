# Simple database check using .NET
$connStr = "Host=localhost;Port=8532;Database=stockdb;Username=postgres;Password=cd123321"

# Load required assemblies
[System.Reflection.Assembly]::LoadFrom("F:\dsfr\mqq\bin\Release\System.Runtime.CompilerServices.Unsafe.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("F:\dsfr\mqq\bin\Release\System.Memory.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("F:\dsfr\mqq\bin\Release\System.Buffers.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("F:\dsfr\mqq\bin\Release\Microsoft.Bcl.AsyncInterfaces.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("F:\dsfr\mqq\bin\Release\System.Threading.Tasks.Extensions.dll") | Out-Null
[System.Reflection.Assembly]::LoadFrom("F:\dsfr\mqq\bin\Release\Npgsql.dll") | Out-Null

try {
    $conn = New-Object Npgsql.NpgsqlConnection($connStr)
    $conn.Open()
    Write-Host "=== Database Connected ===" -ForegroundColor Green

    # Function to run query
    function Run-Query($sql) {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        return $cmd.ExecuteScalar()
    }

    # Check counts
    Write-Host "`n=== Table Counts ===" -ForegroundColor Cyan
    Write-Host "stock_info: $(Run-Query 'SELECT COUNT(*) FROM stock_info')"
    Write-Host "stock_daily_data: $(Run-Query 'SELECT COUNT(*) FROM stock_daily_data')"
    Write-Host "stock_realtime_data: $(Run-Query 'SELECT COUNT(*) FROM stock_realtime_data')"
    Write-Host "stock_exrights_data: $(Run-Query 'SELECT COUNT(*) FROM stock_exrights_data')"

    # Check date range
    Write-Host "`n=== Daily Data Date Range ===" -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT MIN(trade_date)::text, MAX(trade_date)::text FROM stock_daily_data"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Min: $($reader.GetString(0)), Max: $($reader.GetString(1))"
    }
    $reader.Close()

    # Recent data
    Write-Host "`n=== Recent 5 Trading Days ===" -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT trade_date::text, COUNT(DISTINCT stock_code) FROM stock_daily_data WHERE trade_date >= CURRENT_DATE - 10 GROUP BY trade_date ORDER BY trade_date DESC LIMIT 5"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "$($reader.GetString(0)): $($reader.GetInt64(1)) stocks"
    }
    $reader.Close()

    $conn.Close()
    Write-Host "`n=== Done ===" -ForegroundColor Green
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
