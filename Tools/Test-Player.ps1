$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$exePath = Join-Path $taskRoot 'Build\Arcanoid.exe'
if (!(Test-Path -LiteralPath $exePath)) { throw 'Build\Arcanoid.exe is missing. Run Tools\Build-Windows.ps1 first.' }
$logPath = Join-Path $taskRoot 'Tests\player.log'
$testProcess = Start-Process -FilePath $exePath -ArgumentList @(
    '-arcanoidSmoke', '-screen-width', '1024', '-screen-height', '768',
    '-logFile', ('"' + $logPath + '"')
) -WindowStyle Hidden -PassThru
if (!$testProcess.WaitForExit(60000)) {
    Stop-Process -Id $testProcess.Id
    throw 'The game integration check did not finish within 60 seconds. See Tests\player.log.'
}
$testProcess.Refresh()
Get-Content -LiteralPath (Join-Path $taskRoot 'Tests\runtime-report.json')
if ($testProcess.ExitCode -ne 0) { throw 'Game integration checks failed.' }
