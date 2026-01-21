@echo off
chcp 65001 >nul
echo ╔════════════════════════════════════════════════╗
echo ║        查找所有非A股代码工具                   ║
echo ╚════════════════════════════════════════════════╝
echo.
echo 此工具将扫描数据库，查找：
echo   - 指数代码
echo   - 债券代码
echo   - 基金/ETF代码
echo   - B股代码
echo   - 已退市股票
echo.
pause

cd /d "%~dp0"

echo 正在编译...
csc /out:bin\FindAllNonAStockCodes.exe ^
    /r:"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\6.0.0\System.Runtime.dll" ^
    /r:"..\packages\Npgsql.8.0.5\lib\net8.0\Npgsql.dll" ^
    FindAllNonAStockCodes.cs

if %errorlevel% neq 0 (
    echo.
    echo ✗ 编译失败！
    pause
    exit /b 1
)

echo 编译成功！正在运行...
echo.
bin\FindAllNonAStockCodes.exe

echo.
echo ═══════════════════════════════════════════════
echo 提示：生成的黑名单文件位于当前目录
echo 文件名：非A股代码黑名单_自动生成.txt
echo ═══════════════════════════════════════════════
pause
