param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (!(Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }
$logPath = Join-Path $taskRoot 'Tests\unity-build.log'
$unityArguments = @(
    '-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $taskRoot + '"'),
    '-executeMethod', 'ArcanoidProjectBuilder.CreateProject',
    '-logFile', ('"' + $logPath + '"')
)
$buildProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -Wait -PassThru
if ($buildProcess.ExitCode -ne 0) {
    Get-Content -LiteralPath $logPath -Tail 30
    throw "Unity build failed ($($buildProcess.ExitCode)). See $logPath"
}
Write-Output (Join-Path $taskRoot 'Build\Arcanoid.exe')
