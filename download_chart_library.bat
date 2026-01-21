@echo off
chcp 65001 >nul
echo ========================================
echo   下载 LightweightCharts 库到本地
echo ========================================
echo.

set OUTPUT_DIR=src\UI\WebChart\lib
set URL=https://unpkg.com/lightweight-charts@4.2.0/dist/lightweight-charts.standalone.production.js

echo 创建目录: %OUTPUT_DIR%
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo.
echo 正在下载 LightweightCharts 库...
echo 来源: %URL%
echo 目标: %OUTPUT_DIR%\lightweight-charts.js
echo.

powershell -Command "try { Invoke-WebRequest -Uri '%URL%' -OutFile '%OUTPUT_DIR%\lightweight-charts.js' -UseBasicParsing; Write-Host '✅ 下载成功！' -ForegroundColor Green } catch { Write-Host '❌ 下载失败: ' $_.Exception.Message -ForegroundColor Red; exit 1 }"

if errorlevel 1 (
    echo.
    echo ========================================
    echo   下载失败！
    echo ========================================
    echo.
    echo 请尝试以下方法：
    echo 1. 检查网络连接
    echo 2. 手动下载：
    echo    访问: %URL%
    echo    另存为: %OUTPUT_DIR%\lightweight-charts.js
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================
echo   ✅ 下载完成！
echo ========================================
echo.
echo 文件位置: %OUTPUT_DIR%\lightweight-charts.js
echo.
echo 下一步：修改 stock-chart.html，将 CDN 引用改为本地引用
echo.
pause
