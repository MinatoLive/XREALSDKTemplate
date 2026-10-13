@echo off
chcp 65001 >nul
echo 正在执行今日工作总结报告生成...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Generate-DailyReport.ps1" %*
pause
