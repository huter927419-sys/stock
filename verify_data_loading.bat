@echo off
chcp 65001 >nul
echo ========================================
echo   数据加载验证工具
echo ========================================
echo.
echo 此脚本将帮您验证K线数据和KD计算是否正确
echo.

:MENU
echo 请选择验证方式:
echo.
echo [1] 运行SQL查询（推荐 - 快速查看数据库状态）
echo [2] 运行C#验证程序（完整验证 - 包括加载和计算）
echo [3] 两个都运行
echo [Q] 退出
echo.
set /p choice="请输入选项 (1/2/3/Q): "

if /i "%choice%"=="1" goto SQL_ONLY
if /i "%choice%"=="2" goto CS_ONLY
if /i "%choice%"=="3" goto BOTH
if /i "%choice%"=="Q" goto END
if /i "%choice%"=="q" goto END

echo 无效选项，请重新选择
goto MENU

:SQL_ONLY
echo.
echo ========================================
echo   执行SQL查询验证
echo ========================================
echo.

rem 查找psql.exe
set PSQL_PATH=
if exist "F:\dsfr\mqq\tools\bin\psql.exe" (
    set PSQL_PATH=F:\dsfr\mqq\tools\bin\psql.exe
) else if exist "tools\bin\psql.exe" (
    set PSQL_PATH=tools\bin\psql.exe
) else (
    echo 错误: 找不到psql.exe
    echo 请确保PostgreSQL工具在 tools\bin\ 目录下
    pause
    goto END
)

echo 使用 psql: %PSQL_PATH%
echo.

set PGPASSWORD=5JkuPVfGrDY6qqzd
"%PSQL_PATH%" -h localhost -p 8532 -U postgres -d stockdb -f verify_data_loading.sql

echo.
echo SQL查询完成！
echo.
pause
goto END

:CS_ONLY
echo.
echo ========================================
echo   编译并运行C#验证程序
echo ========================================
echo.

rem 查找csc.exe
set CSC_PATH=
for /f "delims=" %%i in ('where csc 2^>nul') do set CSC_PATH=%%i

if "%CSC_PATH%"=="" (
    echo 错误: 找不到C#编译器 (csc.exe^)
    echo 请确保已安装.NET Framework SDK
    echo.
    echo 您也可以在Visual Studio中打开项目，右键点击 tools\verify_kd_data.cs
    echo 选择"设为启动项"后运行
    pause
    goto END
)

echo 找到C#编译器: %CSC_PATH%
echo 开始编译...
echo.

rem 编译验证程序
"%CSC_PATH%" /out:verify_kd_data.exe ^
    /reference:bin\Debug\MQReceiver.exe ^
    tools\verify_kd_data.cs

if errorlevel 1 (
    echo.
    echo 编译失败！请检查错误信息
    pause
    goto END
)

echo 编译成功！
echo 运行验证程序...
echo.

verify_kd_data.exe

pause
goto END

:BOTH
echo.
echo ========================================
echo   运行完整验证
echo ========================================
echo.

call :SQL_ONLY
echo.
echo ----------------------------------------
echo.
call :CS_ONLY

goto END

:END
echo.
echo 验证结束
exit /b
