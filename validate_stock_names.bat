@echo off
chcp 65001 >nul
echo ========================================
echo 股票代码名称验证工具
echo ========================================
echo.
echo 正在编译验证工具...

csc /out:tools\StockNameValidator.exe ^
    /reference:packages\Npgsql.8.0.8\lib\net8.0\Npgsql.dll ^
    /reference:packages\System.Text.Json.8.0.5\lib\net8.0\System.Text.Json.dll ^
    /reference:packages\System.Text.Encodings.Web.8.0.0\lib\net8.0\System.Text.Encodings.Web.dll ^
    /reference:packages\Microsoft.Extensions.Logging.Abstractions.8.0.0\lib\net8.0\Microsoft.Extensions.Logging.Abstractions.dll ^
    tools\StockNameValidator.cs

if %errorlevel% neq 0 (
    echo.
    echo 编译失败!
    pause
    exit /b 1
)

echo.
echo 编译成功! 正在启动验证工具...
echo.

tools\StockNameValidator.exe

pause
