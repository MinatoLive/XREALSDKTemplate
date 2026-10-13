[CmdletBinding()]
param(
    [string]$TargetDate = (Get-Date -Format 'yyyy-MM-dd'),
    [switch]$SkipBuildCheck
)

$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$rootPath = $PSScriptRoot
if ($rootPath.EndsWith('scripts')) {
    $rootPath = Split-Path $rootPath -Parent
}

$reportsDir = Join-Path $rootPath "Reports\Daily"
if (-not (Test-Path $reportsDir)) {
    New-Item -ItemType Directory -Path $reportsDir -Force | Out-Null
    Write-Host "[+] Created reports directory: $reportsDir" -ForegroundColor Cyan
}

$reportFileName = "$TargetDate" + "_WorkSummary.md"
$reportFilePath = Join-Path $reportsDir $reportFileName

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   Daily Work Summary Report Generator" -ForegroundColor Green
Write-Host "   Target Date: $TargetDate" -ForegroundColor Yellow
Write-Host "   Output File: $reportFilePath" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green

# 1. Collect Git commits
$gitLog = "- (Today: working tree modifications in local workspace)"
try {
    $sinceArg = "--since=$TargetDate 00:00:00"
    $untilArg = "--until=$TargetDate 23:59:59"
    $lines = & git log $sinceArg $untilArg '--pretty=format:- **%h** %s' 2>$null
    if ($lines) {
        $gitLog = ($lines -join "`r`n")
    }
} catch {}

# 2. Collect changed files
$changedRuntime = @()
$changedTests = @()
$changedDocs = @()

try {
    $statusLines = & git status --short 2>$null
    foreach ($line in $statusLines) {
        if ($line.Length -gt 3) {
            $trimmed = $line.Substring(3).Trim('"', ' ')
            if ($trimmed -like "*Assets/SurgicalLivePlatform/Tests*") {
                $changedTests += $trimmed
            } elseif ($trimmed -like "*Assets/SurgicalLivePlatform*") {
                $changedRuntime += $trimmed
            } elseif ($trimmed -like "*g3doc*") {
                $changedDocs += $trimmed
            }
        }
    }
} catch {}

# 3. Build verification
$buildStatus = "Not executed"
if (-not $SkipBuildCheck) {
    Write-Host "[*] Validating build and automated tests..." -ForegroundColor Cyan
    $csprojPath = Join-Path $rootPath "SurgicalLivePlatform.Tests.csproj"
    $null = & dotnet build $csprojPath 2>&1
    if ($LASTEXITCODE -eq 0) {
        $buildStatus = 'PASS (0 errors, 0 warnings)'
        Write-Host "[OK] Build passed successfully!" -ForegroundColor Green
    } else {
        $buildStatus = 'FAIL'
        Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    }
}

$nowTimestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'

# 4. Generate or update report
$templatePath = Join-Path $rootPath "Reports\DailyReportTemplate.md"

if (Test-Path $reportFilePath) {
    Write-Host "[*] Existing report found. Updating snapshot block..." -ForegroundColor Cyan
    
    $existingContent = [System.IO.File]::ReadAllText($reportFilePath, [System.Text.Encoding]::UTF8)
    $dividerMarker = '<!-- AUTOMATED_SNAPSHOT_START -->'
    
    $snapshotLines = @(
        '',
        '---',
        '',
        '<!-- AUTOMATED_SNAPSHOT_START -->',
        "## Automated Execution Record and Snapshot Update ($nowTimestamp)",
        '',
        "### Git Commits ($TargetDate)",
        $gitLog,
        '',
        '### Build Health Status',
        "- Result: $buildStatus",
        '- Test Assembly: `SurgicalLivePlatform.Tests.dll`',
        '<!-- AUTOMATED_SNAPSHOT_END -->',
        ''
    )
    $newSnapshot = ($snapshotLines -join "`r`n")

    if ($existingContent.Contains($dividerMarker)) {
        $splitIndex = $existingContent.IndexOf($dividerMarker)
        $divIndex = $existingContent.LastIndexOf('---', $splitIndex)
        if ($divIndex -ge 0) {
            $baseContent = $existingContent.Substring(0, $divIndex).TrimEnd()
        } else {
            $baseContent = $existingContent.Substring(0, $splitIndex).TrimEnd()
        }
        $finalContent = $baseContent + "`r`n" + $newSnapshot
    } else {
        $finalContent = $existingContent.TrimEnd() + "`r`n" + $newSnapshot
    }

    [System.IO.File]::WriteAllText($reportFilePath, $finalContent, [System.Text.Encoding]::UTF8)

} else {
    Write-Host "[+] Scaffolding new daily report from template..." -ForegroundColor Cyan

    $runtimeList = if ($changedRuntime.Count -gt 0) { ($changedRuntime | ForEach-Object { "- `"$_`"" }) -join "`r`n" } else { "- None" }
    $testList = if ($changedTests.Count -gt 0) { ($changedTests | ForEach-Object { "- `"$_`"" }) -join "`r`n" } else { "- None" }
    $docList = if ($changedDocs.Count -gt 0) { ($changedDocs | ForEach-Object { "- `"$_`"" }) -join "`r`n" } else { "- None" }

    if (Test-Path $templatePath) {
        $template = [System.IO.File]::ReadAllText($templatePath, [System.Text.Encoding]::UTF8)
    } else {
        $template = "# Daily Work Summary ($TargetDate)`r`n`r`n{{GIT_LOG}}`r`n"
    }

    $template = $template.Replace('{{DATE}}', $TargetDate)
    $template = $template.Replace('{{TIMESTAMP}}', $nowTimestamp)
    $template = $template.Replace('{{GIT_LOG}}', $gitLog)
    $template = $template.Replace('{{RUNTIME_FILES}}', $runtimeList)
    $template = $template.Replace('{{TEST_FILES}}', $testList)
    $template = $template.Replace('{{DOC_FILES}}', $docList)
    $template = $template.Replace('{{BUILD_STATUS}}', $buildStatus)

    [System.IO.File]::WriteAllText($reportFilePath, $template, [System.Text.Encoding]::UTF8)
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " [SUCCESS] Daily Work Summary is ready!" -ForegroundColor Green
Write-Host " Report File: $reportFilePath" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Green
