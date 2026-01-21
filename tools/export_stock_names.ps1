# Export stock names from Excel to SQL file
$OutputFile = "f:\dsfr\mqq\db\update_stock_names.sql"

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

$stocks = @{}

try {
    # Read GPLIST.xls (col1: code, col3: name)
    Write-Host "Reading GPLIST.xls..."
    $workbook = $excel.Workbooks.Open("f:\dsfr\mqq\GPLIST.xls")
    $sheet = $workbook.Sheets.Item(1)
    $rowCount = $sheet.UsedRange.Rows.Count
    Write-Host "  Rows: $rowCount"

    for ($row = 2; $row -le $rowCount; $row++) {
        $code = $sheet.Cells.Item($row, 1).Text
        $name = $sheet.Cells.Item($row, 3).Text

        if ($code -and $name -and $code -ne "-" -and $name -ne "-") {
            $code = $code.Trim().PadLeft(6, [char]'0')
            if ($code -match '^\d{6}$') {
                $stocks[$code] = $name.Trim()
            }
        }
    }
    $workbook.Close($false)
    Write-Host "  Got $($stocks.Count) stocks"

    # Read A股列表.xlsx (col5: code, col2: company name)
    Write-Host "Reading A-Share list..."
    $xlsxFile = Get-ChildItem "f:\dsfr\mqq\*.xlsx" | Select-Object -First 1
    Write-Host "  Found: $($xlsxFile.FullName)"
    $workbook = $excel.Workbooks.Open($xlsxFile.FullName)
    $sheet = $workbook.Sheets.Item(1)
    $rowCount = $sheet.UsedRange.Rows.Count
    Write-Host "  Rows: $rowCount"

    # A股列表的公司名太长，跳过不用
    $workbook.Close($false)
    Write-Host "  Skipped (names too long)"

    Write-Host ""
    Write-Host "Total: $($stocks.Count) stocks"

    # Generate SQL file
    Write-Host "Generating SQL file: $OutputFile"

    $lines = @()
    $lines += "-- Stock names update script from Excel"
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
    $lines += ""
    $lines += "-- Show statistics"
    $lines += "SELECT COUNT(*) AS total, SUM(CASE WHEN stock_name <> stock_code THEN 1 ELSE 0 END) AS has_name, SUM(CASE WHEN stock_name = stock_code OR stock_name IS NULL THEN 1 ELSE 0 END) AS no_name FROM stock_info;"

    # Use UTF8 without BOM
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($OutputFile, $lines, $utf8NoBom)
    Write-Host "SQL file generated!"

} finally {
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
