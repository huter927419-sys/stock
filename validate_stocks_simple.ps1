# 简单的股票验证脚本
# 从在线API获取股票名称并显示

param(
    [string[]]$StockCodes = @()
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "股票代码名称验证工具 (简化版)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 如果没有提供代码,使用测试代码
if ($StockCodes.Count -eq 0) {
    $StockCodes = @(
        "000851", "000091", "000071", "000132", "000137",  
        "000102", "000146", "000161", "000107", "000033",
        "000073", "000077", "000052", "000053", "000847", "000854",
        "000001", "000002", "600000", "600519", "300033"
    )
}

Write-Host "正在验证 $($StockCodes.Count) 个股票代码..." -ForegroundColor Yellow
Write-Host ""

$validCount = 0
$invalidCount = 0

foreach ($code in $StockCodes) {
    # 判断市场
    $market = if ($code.StartsWith("6")) { "sh" } else { "sz" }
    
    try {
        # 尝试新浪API
        $url = "http://hq.sinajs.cn/list=$market$code"
        $response = Invoke-WebRequest -Uri $url -TimeoutSec 3 -UseBasicParsing
        $content = $response.Content
        
        if ($content -match '"([^"]+)"') {
            $data = $matches[1]
            if ($data -ne "") {
                $parts = $data.Split(',')
                $name = $parts[0]
                
                if ($name -ne "" -and $name -ne $code) {
                    Write-Host "[✓ 有效] " -NoNewline -ForegroundColor Green
                    Write-Host "$code = $name" -ForegroundColor White
                    $validCount++
                } else {
                    Write-Host "[✗ 无效] " -NoNewline -ForegroundColor Red
                    Write-Host "$code (无数据)" -ForegroundColor Gray
                    $invalidCount++
                }
            } else {
                Write-Host "[✗ 无效] " -NoNewline -ForegroundColor Red
                Write-Host "$code (空响应)" -ForegroundColor Gray
                $invalidCount++
            }
        } else {
            Write-Host "[✗ 无效] " -NoNewline -ForegroundColor Red
            Write-Host "$code (格式错误)" -ForegroundColor Gray
            $invalidCount++
        }
    }
    catch {
        Write-Host "[✗ 错误] " -NoNewline -ForegroundColor Red
        Write-Host "$code ($($_.Exception.Message))" -ForegroundColor Gray
        $invalidCount++
    }
    
    Start-Sleep -Milliseconds 100
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "统计结果:" -ForegroundColor Cyan
Write-Host "  有效代码: $validCount" -ForegroundColor Green
Write-Host "  无效代码: $invalidCount" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Cyan
