@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM ============================================
REM 重命名 dump 文件，去掉中文
REM 格式：stockdb_full_YYYYMMDD_HHMMSS.dump
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

REM 从原文件名提取日期时间：周日022601_150452
REM 根据文件名中的022601推断：2026年1月26日
REM 新格式：stockdb_full_20260126_150452.dump
REM 或者使用文件修改时间
for %%F in ("%BACKUP_DIR%\%OLD_FILE%") do (
    REM 获取文件修改时间（格式：2026/01/26 15:04:52）
    for /f "tokens=1-3 delims=/ " %%a in ("%%~tF") do (
        set YEAR=%%c
        set MONTH=%%a
        set DAY=%%b
    )
    for /f "tokens=1-3 delims=: " %%a in ("%%~tF") do (
        set HOUR=%%a
        set MINUTE=%%b
        set SECOND=%%c
    )
    REM 移除空格并补齐
    set MONTH=0!MONTH!
    set MONTH=!MONTH:~-2!
    set DAY=0!DAY!
    set DAY=!DAY:~-2!
    set HOUR=0!HOUR!
    set HOUR=!HOUR:~-2!
    set MINUTE=0!MINUTE!
    set MINUTE=!MINUTE:~-2!
    set SECOND=0!SECOND!
    set SECOND=!SECOND:~-2!
    
    REM 生成新文件名
    set NEW_FILE=stockdb_full_!YEAR!!MONTH!!DAY!_!HOUR!!MINUTE!!SECOND!.dump
)

REM 如果无法从文件时间获取，使用固定格式
if not defined NEW_FILE (
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
