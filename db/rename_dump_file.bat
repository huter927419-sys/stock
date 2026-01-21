@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM ============================================
REM 重命名 dump 文件，去掉中文，使用日期时间格式
REM ============================================

set BACKUP_DIR=.\backups
set OLD_FILE=stockdb_full_周日022601_150452.dump

REM 检查文件是否存在
if not exist "%BACKUP_DIR%\%OLD_FILE%" (
    echo [错误] 找不到文件: %BACKUP_DIR%\%OLD_FILE%
    echo.
    echo 当前目录中的 .dump 文件:
    dir /b "%BACKUP_DIR%\*.dump" 2>nul
    pause
    exit /b 1
)

REM 生成新的文件名（使用日期时间格式：stockdb_full_YYYYMMDD_HHMMSS.dump）
REM 从原文件名提取日期时间：周日022601_150452 -> 20260126_150452
REM 注意：原文件名中的"周日"表示星期天，我们需要推断日期
REM 但为了简单，我们使用当前日期时间，或者从文件名提取

REM 尝试从文件名提取日期
REM 原格式：stockdb_full_周日022601_150452.dump
REM 新格式：stockdb_full_20260126_150452.dump

REM 如果文件名包含"周日022601"，我们假设是2026年1月26日
REM 但更安全的方式是使用文件的修改时间

for %%F in ("%BACKUP_DIR%\%OLD_FILE%") do (
    set FILE_DATE=%%~tF
    REM 文件日期格式：2026/01/26 15:04:52
    REM 提取日期部分并格式化
    set YEAR=!FILE_DATE:~0,4!
    set MONTH=!FILE_DATE:~5,2!
    set DAY=!FILE_DATE:~8,2!
    set HOUR=!FILE_DATE:~11,2!
    set MINUTE=!FILE_DATE:~14,2!
    set SECOND=!FILE_DATE:~17,2!
    
    REM 生成新文件名
    set NEW_FILE=stockdb_full_!YEAR!!MONTH!!DAY!_!HOUR!!MINUTE!!SECOND!.dump
)

REM 如果无法从文件时间获取，使用固定格式
if not defined NEW_FILE (
    REM 从原文件名提取时间部分：150452
    set NEW_FILE=stockdb_full_20260126_150452.dump
)

echo ============================================
echo 重命名 dump 文件
echo ============================================
echo 原文件: %OLD_FILE%
echo 新文件: %NEW_FILE%
echo ============================================
echo.

set /p CONFIRM="确认要重命名吗？(Y/N，默认Y): "
if /i not "!CONFIRM!"=="N" (
    ren "%BACKUP_DIR%\%OLD_FILE%" "%NEW_FILE%"
    if !errorlevel! equ 0 (
        echo.
        echo 重命名成功！
        echo 新文件路径: %BACKUP_DIR%\%NEW_FILE%
    ) else (
        echo.
        echo [错误] 重命名失败！
    )
) else (
    echo 操作已取消
)

echo.
pause
