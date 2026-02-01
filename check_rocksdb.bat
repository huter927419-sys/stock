@echo off
chcp 65001 >nul
echo 正在查询 RocksDB 日线数据...
echo 说明：RocksDB 路径由 App.config 的 RocksDBPath 决定，相对路径相对于 exe 所在目录。
echo.
cd /d "%~dp0"
if exist "bin\x64\Debug\MQReceiver.exe" (
    "bin\x64\Debug\MQReceiver.exe" --check-rocksdb
    set "REPORT=bin\x64\Debug\rocksdb_check_report.txt"
) else if exist "bin\x64\Release\MQReceiver.exe" (
    "bin\x64\Release\MQReceiver.exe" --check-rocksdb
    set "REPORT=bin\x64\Release\rocksdb_check_report.txt"
) else if exist "bin\Debug\MQReceiver.exe" (
    "bin\Debug\MQReceiver.exe" --check-rocksdb
    set "REPORT=bin\Debug\rocksdb_check_report.txt"
) else if exist "bin\Release\MQReceiver.exe" (
    "bin\Release\MQReceiver.exe" --check-rocksdb
    set "REPORT=bin\Release\rocksdb_check_report.txt"
) else (
    echo 未找到 MQReceiver.exe，请先编译项目。
    pause
    exit /b 1
)
echo.
if exist "%REPORT%" (
    echo 报告已生成: %REPORT%
    type "%REPORT%"
) else (
    echo 报告未生成，请查看上方输出。
)
pause
