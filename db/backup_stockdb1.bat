@echo off
chcp 65001 >nul
REM ============================================
REM 备份 stockdb1 数据库（特定表）
REM ============================================
REM 用法: backup_stockdb1.bat [表名]
REM 如果不指定表名，则备份所有表

REM 数据库连接配置
set DB_HOST=localhost
set DB_PORT=8532
set DB_NAME=stockdb1
set DB_USER=postgres
set DB_PASSWORD=cd123321

REM 备份文件配置
set BACKUP_DIR=.\backups
set TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%

REM 创建备份目录
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

REM 获取要备份的表名（如果指定）
set TABLE_NAME=%1
if "%TABLE_NAME%"=="" set TABLE_NAME=all

REM 查找工具路径
set PGDUMP_PATH=
for %%p in (
   "F:\dsfr\mqq\tools\bin\pg_dump.exe"
   "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe"
   "C:\Program Files\PostgreSQL\15\bin\pg_dump.exe"
) do (
    if exist %%p (
        set PGDUMP_PATH=%%p
        goto :found_pgdump
    )
)
where pg_dump >nul 2>&1
if %errorlevel%==0 set PGDUMP_PATH=pg_dump

:found_pgdump
if "%PGDUMP_PATH%"=="" (
    echo [错误] 找不到 pg_dump.exe
    exit /b 1
)

REM 设置密码环境变量
set PGPASSWORD=%DB_PASSWORD%

if "%TABLE_NAME%"=="all" (
    REM 备份整个数据库
    set BACKUP_FILE=%BACKUP_DIR%\stockdb1_full_%TIMESTAMP%.dump
    echo 备份整个 stockdb1 数据库...
    "%PGDUMP_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -Fc -f "%BACKUP_FILE%"
) else (
    REM 备份指定表
    set BACKUP_FILE=%BACKUP_DIR%\stockdb1_%TABLE_NAME%_%TIMESTAMP%.dump
    echo 备份表: %TABLE_NAME%
    "%PGDUMP_PATH%" -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -t public.%TABLE_NAME% -Fc -f "%BACKUP_FILE%"
)

if %ERRORLEVEL% EQU 0 (
    if exist "%BACKUP_FILE%" (
        for %%A in ("%BACKUP_FILE%") do (
            set SIZE=%%~zA
            set /a SIZE_MB=!SIZE!/1048576
            set /a SIZE_GB=!SIZE!/1073741824
            if !SIZE_GB! GTR 0 (
                echo 备份成功！文件大小: !SIZE_GB! GB (!SIZE_MB! MB)
            ) else (
                echo 备份成功！文件大小: !SIZE_MB! MB
            )
        )
    )
    exit /b 0
) else (
    echo [错误] 备份失败
    exit /b 1
)
