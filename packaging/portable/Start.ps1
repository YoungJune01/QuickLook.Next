# Windows PowerShell 5.1 startup launcher. Only this child receives the portable runtime environment.
param([switch]$SmokePortable)
$ErrorActionPreference = 'Stop'
$portableRoot = $PSScriptRoot
$runtime = Join-Path $portableRoot 'Runtime'
$app = Join-Path $portableRoot 'App'
$exe = Join-Path $app 'QuickLook.Next.App.exe'
foreach ($required in @((Join-Path $portableRoot 'portable.mode'), (Join-Path $runtime 'dotnet.exe'), $exe)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { exit 1 }
}
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $exe
$startInfo.WorkingDirectory = $app
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
if ($SmokePortable) { $startInfo.Arguments = '--smoke-portable' }
$startInfo.EnvironmentVariables['DOTNET_ROOT'] = $runtime
$startInfo.EnvironmentVariables['DOTNET_ROOT_X64'] = $runtime
$startInfo.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$startInfo.EnvironmentVariables['DOTNET_ROLL_FORWARD'] = 'LatestPatch'
$startInfo.EnvironmentVariables['TEMP'] = Join-Path $portableRoot 'Data\Temp'
$startInfo.EnvironmentVariables['TMP'] = $startInfo.EnvironmentVariables['TEMP']
$startInfo.EnvironmentVariables['PATH'] = "$runtime;$app;$env:SystemRoot\System32;$env:SystemRoot"
$process = [System.Diagnostics.Process]::Start($startInfo)
if ($SmokePortable) {
    $process.WaitForExit()
    $code = $process.ExitCode
    $process.Dispose()
    exit $code
}
$process.Dispose()
