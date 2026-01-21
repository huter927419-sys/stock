# 读取Excel文件内容
param(
    [string]$FilePath
)

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    $workbook = $excel.Workbooks.Open($FilePath)
    $sheet = $workbook.Sheets.Item(1)

    Write-Host "=== 文件: $FilePath ==="
    Write-Host ""

    # 显示前10行
    Write-Host "前10行数据:"
    for($row = 1; $row -le 10; $row++) {
        $values = @()
        for($col = 1; $col -le 5; $col++) {
            $val = $sheet.Cells.Item($row, $col).Text
            if ($val) { $values += $val }
        }
        if ($values.Count -gt 0) {
            Write-Host ("  " + ($values -join " | "))
        }
    }

    Write-Host ""
    Write-Host "总行数: $($sheet.UsedRange.Rows.Count)"
    Write-Host "总列数: $($sheet.UsedRange.Columns.Count)"

    $workbook.Close($false)
}
finally {
    $excel.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
}
