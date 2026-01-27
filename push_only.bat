@echo off
chcp 65001 >nul
cd /d F:\dsfr\mqq
echo 正在推送到 origin master ...
git push origin master
if errorlevel 1 (
    echo.
    echo 推送失败，请检查网络或远程仓库配置。
    pause
    exit /b 1
)
echo.
echo 推送完成。
pause
