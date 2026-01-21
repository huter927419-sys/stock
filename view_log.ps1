# 快速查看日志工具
# 使用方法: .\view_log.ps1 [日志文件路径]

param(
    [Parameter(Mandatory=$false)]
    [string]$LogFile = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "日志查看工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($LogFile -eq "") {
    # 查找日志文件
    Write-Host "正在查找日志文件..." -ForegroundColor Yellow
    
    $logFiles = @()
    
    # 查找当前目录下的日志文件
    $logFiles += Get-ChildItem -Path . -Filter "*.log" -ErrorAction SilentlyContinue | Select-Object -First 5
    $logFiles += Get-ChildItem -Path . -Filter "kd_log*.txt" -ErrorAction SilentlyContinue | Select-Object -First 5
    $logFiles += Get-ChildItem -Path . -Filter "*log*.txt" -ErrorAction SilentlyContinue | Select-Object -First 5
    
    if ($logFiles.Count -eq 0) {
        Write-Host "未找到日志文件" -ForegroundColor Red
        Write-Host ""
        Write-Host "请提供日志文件路径，或使用以下方法创建日志：" -ForegroundColor Yellow
        Write-Host "  1. 运行程序时重定向输出:" -ForegroundColor White
        Write-Host "     .\bin\Release\MQReceiver.exe > kd_log.txt 2>&1" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  2. 或使用日志捕获脚本:" -ForegroundColor White
        Write-Host "     .\capture_kd_log.bat" -ForegroundColor Gray
        Write-Host ""
        exit 0
    }
    
    Write-Host "找到以下日志文件:" -ForegroundColor Green
    for ($i = 0; $i -lt $logFiles.Count; $i++) {
        Write-Host "  [$i] $($logFiles[$i].Name) ($([math]::Round($logFiles[$i].Length/1KB, 2)) KB)" -ForegroundColor Cyan
    }
    Write-Host ""
    
    $choice = Read-Host "请选择要查看的日志文件 (输入序号，或直接回车查看第一个)"
    
    if ($choice -eq "") {
        $LogFile = $logFiles[0].FullName
    } elseif ($choice -match '^\d+$' -and [int]$choice -lt $logFiles.Count) {
        $LogFile = $logFiles[[int]$choice].FullName
    } else {
        Write-Host "无效的选择" -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $LogFile)) {
    Write-Host "错误: 日志文件不存在: $LogFile" -ForegroundColor Red
    exit 1
}

Write-Host "正在分析日志文件: $LogFile" -ForegroundColor Green
Write-Host ""

# 读取日志文件
$log = Get-Content $LogFile -ErrorAction SilentlyContinue

if ($null -eq $log -or $log.Count -eq 0) {
    Write-Host "日志文件为空" -ForegroundColor Red
    exit 1
}

Write-Host "日志文件大小: $([math]::Round((Get-Item $LogFile).Length/1KB, 2)) KB" -ForegroundColor Cyan
Write-Host "日志行数: $($log.Count)" -ForegroundColor Cyan
Write-Host ""

# 查找关键信息
Write-Host "=== KD相关日志 ===" -ForegroundColor Yellow
Write-Host ""

$kdLogs = $log | Select-String -Pattern "\[KD|KD计算|KD调试|WebChart调试|ConvertKDData" | Select-Object -Last 50

if ($kdLogs) {
    Write-Host "找到 $($kdLogs.Count) 条KD相关日志:" -ForegroundColor Green
    Write-Host ""
    $kdLogs | ForEach-Object {
        Write-Host $_.Line -ForegroundColor White
    }
} else {
    Write-Host "未找到KD相关日志" -ForegroundColor Yellow
    Write-Host "提示: 请确保程序已运行并打开图表窗口" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== 错误信息 ===" -ForegroundColor Yellow
Write-Host ""

$errors = $log | Select-String -Pattern "错误|异常|Exception|Error|失败" -CaseSensitive:$false | Select-Object -Last 20

if ($errors) {
    Write-Host "找到 $($errors.Count) 条错误信息:" -ForegroundColor Red
    Write-Host ""
    $errors | ForEach-Object {
        Write-Host $_.Line -ForegroundColor Red
    }
} else {
    Write-Host "未发现错误" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== 最近50行日志 ===" -ForegroundColor Yellow
Write-Host ""

$log | Select-Object -Last 50 | ForEach-Object {
    Write-Host $_ -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "查看完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "提示: 要查看完整日志，可以使用:" -ForegroundColor Yellow
Write-Host "  Get-Content '$LogFile' | Select-String -Pattern 'KD'" -ForegroundColor Gray
Write-Host ""
