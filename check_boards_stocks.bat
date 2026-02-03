@echo off
REM 检查 Boards.json 中的股票代码是否在 RocksDB 中存在
REM 使用方法: 先编译项目，然后运行此批处理文件

echo 正在编译检查工具...
cd /d "%~dp0"

REM 检查是否在 Debug 目录
if exist "bin\x64\Debug\MQReceiver.exe" (
    cd bin\x64\Debug
    echo.
    echo 运行检查工具...
    echo.
    MQReceiver.exe --check-boards-stocks
) else (
    echo 错误: 找不到编译后的程序，请先编译项目
    echo 或者手动运行: bin\x64\Debug\MQReceiver.exe --check-boards-stocks
    pause
)
