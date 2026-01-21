# 将 lightweight-charts.js 内嵌到 stock-chart.html 中
# 这样可以完全避免文件加载问题

$jsFile = "F:\dsfr\mqq\src\UI\WebChart\lib\lightweight-charts.js"
$htmlFile = "F:\dsfr\mqq\src\UI\WebChart\stock-chart.html"
$outputFile = "F:\dsfr\mqq\src\UI\WebChart\stock-chart-embedded.html"

Write-Host "正在读取JS文件..." -ForegroundColor Yellow
$jsContent = Get-Content $jsFile -Raw -Encoding UTF8

Write-Host "正在读取HTML文件..." -ForegroundColor Yellow
$htmlContent = Get-Content $htmlFile -Raw -Encoding UTF8

Write-Host "正在内嵌JS到HTML..." -ForegroundColor Yellow
# 替换外部脚本引用为内嵌脚本
$newHtml = $htmlContent -replace '<script src="lib/lightweight-charts.js"></script>', "<script>`n$jsContent`n</script>"

Write-Host "正在保存新文件..." -ForegroundColor Yellow
$newHtml | Out-File $outputFile -Encoding UTF8

Write-Host "✅ 完成！新文件已保存到: $outputFile" -ForegroundColor Green
Write-Host "文件大小: $((Get-Item $outputFile).Length / 1MB) MB" -ForegroundColor Cyan
