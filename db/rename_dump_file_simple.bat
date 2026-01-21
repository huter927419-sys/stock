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

REM 使用文件修改时间生成新文件名
REM 格式：stockdb_full_YYYYMMDD_HHMMSS.dump
for %%F in ("%BACKUP_DIR%\%OLD_FILE%") do (
    REM 获取文件修改时间
    for /f "tokens=1-3 delims=/ " %%a in ("%%~tF") do (
        set FILE_DATE=%%c%%a%%b
    )
    for /f "tokens=1-3 delims=: " %%a in ("%%~tF") do (
        set FILE_TIME=%%a%%b%%c
    )
    REM 移除空格
    set FILE_DATE=!FILE_DATE: =0!
    set FILE_TIME=!FILE_TIME: =0!
    
    REM 生成新文件名
    set NEW_FILE=stockdb_full_!FILE_DATE!_!FILE_TIME!.dump
)

REM 如果无法从文件时间获取，使用固定格式（从原文件名提取：022601_150452）
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
