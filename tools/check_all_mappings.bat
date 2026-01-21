@echo off
chcp 65001 >nul
echo 正在编译全面映射检查工具...
echo.

cd /d "%~dp0"

csc /out:bin\ComprehensiveStockMappingChecker.exe ^
    /r:"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\6.0.0\System.Runtime.dll" ^
    /r:"..\packages\Npgsql.8.0.5\lib\net8.0\Npgsql.dll" ^
    ComprehensiveStockMappingChecker.cs

if %errorlevel% neq 0 (
    echo 编译失败！
    pause
    exit /b 1
)

echo 编译成功！正在运行...
echo.
bin\ComprehensiveStockMappingChecker.exe

pause
