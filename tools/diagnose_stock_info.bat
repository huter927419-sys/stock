@echo off
chcp 65001 >nul
echo ========================================
echo 编译并运行股票信息表诊断工具
echo ========================================
echo.

echo 正在编译...
csc /r:"%~dp0..\packages\Npgsql.8.0.8\lib\net8.0\Npgsql.dll" /out:"%~dp0StockInfoDiagnostics.exe" "%~dp0StockInfoDiagnostics.cs"

if errorlevel 1 (
    echo 编译失败！
    pause
    exit /b 1
)

echo 编译成功！
echo.
echo 正在运行诊断...
echo.

"%~dp0StockInfoDiagnostics.exe"

echo.
echo ========================================
echo 诊断完成！
echo ========================================
echo.
pause
