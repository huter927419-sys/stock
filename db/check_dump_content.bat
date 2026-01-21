@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM ============================================
REM 检查 dump 文件内容
REM ============================================

set DUMP_FILE=%1
if "%DUMP_FILE%"=="" (
    set DUMP_FILE=F:\dsfr\mqq\db\backups\stockdb_full_周日022601_150452.dump
)

REM 查找 pg_restore 路径
set PGRESTORE_PATH=
for %%p in (
   "F:\dsfr\mqq\tools\bin\pg_restore.exe"
   "C:\Program Files\PostgreSQL\16\bin\pg_restore.exe"
   "C:\Program Files\PostgreSQL\15\bin\pg_restore.exe"
) do (
    if exist %%p (
        set PGRESTORE_PATH=%%p
        goto :found_pgrestore
    )
)
where pg_restore >nul 2>&1
if %errorlevel%==0 set PGRESTORE_PATH=pg_restore

:found_pgrestore
if "%PGRESTORE_PATH%"=="" (
    echo [错误] 找不到 pg_restore.exe
    pause
    exit /b 1
)

echo ============================================
echo 检查 dump 文件内容
echo Dump 文件: %DUMP_FILE%
echo ============================================
echo.

echo 列出 dump 文件中的所有内容:
"!PGRESTORE_PATH!" -l "%DUMP_FILE%" 2>&1

echo.
echo ============================================
echo 查找 stock_daily_data 相关内容:
echo ============================================
"!PGRESTORE_PATH!" -l "%DUMP_FILE%" 2>&1 | findstr /i "stock_daily_data"

echo.
echo ============================================
echo 查找包含数据的内容（TABLE DATA）:
echo ============================================
"!PGRESTORE_PATH!" -l "%DUMP_FILE%" 2>&1 | findstr /i "TABLE DATA"

pause
