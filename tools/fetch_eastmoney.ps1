# Fetch stock names from Eastmoney API
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$wc = New-Object System.Net.WebClient
$wc.Encoding = [System.Text.Encoding]::UTF8
$wc.Headers.Add('User-Agent', 'Mozilla/5.0')
$wc.Headers.Add('Referer', 'https://quote.eastmoney.com/')

$markets = @(
    "m:0+t:6,m:0+t:13,m:0+t:80",  # Shenzhen
    "m:1+t:2,m:1+t:23"             # Shanghai
)

$stocks = @{}

foreach ($market in $markets) {
    Write-Host "Fetching $market..."
    $url = "https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=6000&po=1&np=1&ut=bd1d9ddb04089700cf9c27f6f7426281&fltt=2&invt=2&fid=f12&fs=$market&fields=f12,f14"

    try {
        $json = $wc.DownloadString($url)

        # Parse with regex
        $matches = [regex]::Matches($json, '"f12":"(\d{6})","f14":"([^"]+)"')
        Write-Host "  Found $($matches.Count) stocks"

        foreach ($m in $matches) {
            $code = $m.Groups[1].Value
            $name = $m.Groups[2].Value
            $stocks[$code] = $name
        }
    }
    catch {
        Write-Host "  Error: $_"
    }
}

Write-Host ""
Write-Host "Total: $($stocks.Count) stocks"

# Generate SQL
$lines = @()
$lines += "-- Stock names from Eastmoney API"
$lines += "-- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$lines += "-- Stock count: $($stocks.Count)"
$lines += ""
$lines += "BEGIN;"
$lines += ""

foreach ($kv in $stocks.GetEnumerator()) {
    $code = $kv.Key
    $name = $kv.Value -replace "'", "''"
    $lines += "UPDATE stock_info SET stock_name = '$name', update_time = CURRENT_TIMESTAMP WHERE stock_code = '$code' AND (stock_name IS NULL OR stock_name = '' OR stock_name = stock_code OR LENGTH(stock_name) > 4);"
}

$lines += ""
$lines += "COMMIT;"

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines("f:\dsfr\mqq\db\update_stock_names.sql", $lines, $utf8NoBom)
Write-Host "SQL file generated!"
