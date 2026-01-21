# 图表加载日志查看器
# 用法: .\view_chart_log.ps1 [chart_log.txt]

param(
    [string]$LogFile = "chart_log.txt"
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "图表加载日志分析器" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $LogFile)) {
    Write-Host "错误: 找不到日志文件 $LogFile" -ForegroundColor Red
    Write-Host ""
    Write-Host "请先运行 capture_chart_log.bat 生成日志" -ForegroundColor Yellow
    exit 1
}

$content = Get-Content $LogFile -Encoding UTF8

Write-Host "1. 图表数据加载" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────" -ForegroundColor Gray
$content | Select-String "图表数据加载" | ForEach-Object {
    $line = $_.Line
    if ($line -match "❌") {
        Write-Host $line -ForegroundColor Red
    } elseif ($line -match "✅") {
        Write-Host $line -ForegroundColor Green
    } elseif ($line -match "⚠️") {
        Write-Host $line -ForegroundColor Yellow
    } else {
        Write-Host $line
    }
}
Write-Host ""

Write-Host "2. WebView初始化" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────" -ForegroundColor Gray
$content | Select-String "WebView初始化" | ForEach-Object {
    $line = $_.Line
    if ($line -match "❌") {
        Write-Host $line -ForegroundColor Red
    } elseif ($line -match "✅") {
        Write-Host $line -ForegroundColor Green
    } elseif ($line -match "⚠️") {
        Write-Host $line -ForegroundColor Yellow
    } else {
        Write-Host $line
    }
}
Write-Host ""

Write-Host "3. 嵌入式资源加载" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────" -ForegroundColor Gray
$content | Select-String "资源加载" | ForEach-Object {
    $line = $_.Line
    if ($line -match "❌") {
        Write-Host $line -ForegroundColor Red
    } elseif ($line -match "✅") {
        Write-Host $line -ForegroundColor Green
    } elseif ($line -match "⚠️") {
        Write-Host $line -ForegroundColor Yellow
    } else {
        Write-Host $line
    }
}
Write-Host ""

Write-Host "4. 设置图表数据" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────" -ForegroundColor Gray
$content | Select-String "设置图表数据" | ForEach-Object {
    $line = $_.Line
    if ($line -match "❌") {
        Write-Host $line -ForegroundColor Red
    } elseif ($line -match "✅") {
        Write-Host $line -ForegroundColor Green
    } elseif ($line -match "⚠️") {
        Write-Host $line -ForegroundColor Yellow
    } else {
        Write-Host $line
    }
}
Write-Host ""

Write-Host "5. JavaScript执行" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────" -ForegroundColor Gray
$content | Select-String "Chart\]" | ForEach-Object {
    $line = $_.Line
    if ($line -match "❌|FAIL|Error") {
        Write-Host $line -ForegroundColor Red
    } elseif ($line -match "✅|OK|success") {
        Write-Host $line -ForegroundColor Green
    } else {
        Write-Host $line
    }
}
Write-Host ""

Write-Host "6. 错误信息" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────" -ForegroundColor Gray
$errors = $content | Select-String "错误|异常|失败|Exception"
if ($errors.Count -eq 0) {
    Write-Host "✅ 没有发现错误！" -ForegroundColor Green
} else {
    $errors | ForEach-Object {
        Write-Host $_.Line -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "7. 数据统计" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────" -ForegroundColor Gray
$content | Select-String "日K线数量:|周KD数量:|月KD数量:|季KD数量:" | ForEach-Object {
    Write-Host $_.Line -ForegroundColor Cyan
}
Write-Host ""

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "日志文件: $LogFile" -ForegroundColor Gray
Write-Host "总行数: $($content.Count)" -ForegroundColor Gray
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
