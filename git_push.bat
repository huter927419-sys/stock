@echo off
chcp 65001 >nul
echo ========================================
echo   提交并推送代码到 GitHub
echo ========================================
echo.

cd /d F:\dsfr\mqq

echo [1] 添加所有更改的文件...
git add -A
if errorlevel 1 (
    echo 错误: git add 失败
    pause
    exit /b 1
)
echo ✅ 文件已添加到暂存区
echo.

echo [2] 查看将要提交的文件...
git status --short
echo.

echo [3] 提交代码...
git commit -m "修复KD计算逻辑，验证全量数据加载，添加数据验证工具

主要更改:
- 修复KD计算在数据不足9个周期时的问题，支持动态周期调整
- 验证确认代码加载的是全量历史数据（8000+条）
- 添加数据验证工具集（SQL、C#、批处理脚本）
- 添加完整的验证文档和使用指南

文件变更:
- src/DataProcessing/Calculators/KDCalculator.cs: 动态周期调整
- verify_data_loading.sql: SQL数据验证脚本
- tools/verify_kd_data.cs: C#完整验证程序  
- verify_data_loading.bat, quick_check.bat: 验证批处理脚本
- VERIFICATION_SUMMARY.md: 验证总结文档
- FULL_DATA_LOADING_VERIFICATION.md: 完整验证报告
- DATA_VERIFICATION_GUIDE.md: 验证工具使用指南
- KD_CALCULATION_FIX.md: KD计算修复说明"

if errorlevel 1 (
    echo ⚠️ 提交失败或没有需要提交的更改
    echo.
    git status
    pause
    exit /b 1
)
echo ✅ 代码已提交
echo.

echo [4] 推送到远程仓库...
git push origin main
if errorlevel 1 (
    echo ⚠️ 推送失败，尝试推送到 master 分支...
    git push origin master
    if errorlevel 1 (
        echo ❌ 推送失败！
        echo.
        echo 可能的原因:
        echo 1. 网络连接问题
        echo 2. 需要先 pull 远程更改
        echo 3. SSH 密钥未配置
        echo.
        echo 尝试手动执行:
        echo   git pull origin main
        echo   git push origin main
        pause
        exit /b 1
    )
)
echo ✅ 代码已推送到 GitHub
echo.

echo ========================================
echo   ✅ 所有操作完成！
echo ========================================
echo.
echo GitHub 仓库: https://github.com/huter927419-sys/stock
echo.
pause
