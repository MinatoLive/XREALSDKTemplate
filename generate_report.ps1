# 一键生成今日工作总结报告快捷命令
param(
    [string]$TargetDate = (Get-Date -Format 'yyyy-MM-dd'),
    [switch]$SkipBuildCheck
)

& "$PSScriptRoot\scripts\Generate-DailyReport.ps1" -TargetDate $TargetDate -SkipBuildCheck:$SkipBuildCheck
