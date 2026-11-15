#requires -Version 7.0
param([Parameter(Mandatory = $true)][string]$PackageDirectory)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$package = [IO.Path]::GetFullPath($PackageDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $package 'portable-manifest.json') -Raw | ConvertFrom-Json
$archiveHash = (Get-Content -LiteralPath "$package.zip.sha256" -Raw).Split(' ')[0]
if ((Get-FileHash -LiteralPath "$package.zip" -Algorithm SHA256).Hash -ne $archiveHash) {
    throw 'Portable archive checksum mismatch.'
}
foreach ($file in $manifest.files.PSObject.Properties) {
    if ([IO.Path]::IsPathRooted($file.Name) -or $file.Name.Split('/') -contains '..') {
        throw 'Unsafe portable manifest path.'
    }
    if ((Get-FileHash -LiteralPath (Join-Path $package $file.Name) -Algorithm SHA256).Hash -ne $file.Value) {
        throw "Portable file hash mismatch: $($file.Name)"
    }
}
if (@(Get-ChildItem -LiteralPath (Join-Path $package 'Data') -Force).Count) {
    throw 'Delivery Data must be empty.'
}
if (Test-Path (Join-Path $package 'Runtime/sdk')) { throw 'A developer SDK entered the package.' }

function Get-ProfileSnapshot {
    $snapshot = [ordered]@{}
    $localData = [Environment]::GetFolderPath('LocalApplicationData')
    foreach ($name in @('QuickLook.Next', 'QuickLookNext')) {
        $directory = Join-Path $localData $name
        if (Test-Path -LiteralPath $directory) {
            foreach ($file in Get-ChildItem -LiteralPath $directory -File -Recurse -Force | Sort-Object FullName) {
                $snapshot[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            }
        }
    }
    $runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try {
        $snapshot['Run/QuickLookNext'] = if ($runKey) { $runKey.GetValue('QuickLookNext') } else { $null }
        if ($runKey) {
            foreach ($name in @($runKey.GetValueNames() | Where-Object { $_ -like 'QuickLookNext.Portable.*' } | Sort-Object)) {
                $snapshot["Run/$name"] = $runKey.GetValue($name)
            }
        }
    }
    finally { if ($runKey) { $runKey.Dispose() } }
    $shortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'QuickLook Next.lnk'
    $snapshot['Startup/QuickLook Next.lnk'] = if (Test-Path -LiteralPath $shortcut) {
        (Get-FileHash -LiteralPath $shortcut -Algorithm SHA256).Hash
    } else { $null }
    return ($snapshot | ConvertTo-Json -Compress)
}

function Invoke-IsolatedProcess([string]$Executable, [string]$Arguments, [int]$ExpectedExit = 0,
    [bool]$ConfigurePortableEnvironment = $true) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable, $Arguments)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.WorkingDirectory = $testRoot
    # Remove inherited developer/runtime overrides; resolve everything from this copy.
    foreach ($key in @($info.Environment.Keys)) {
        if ($key -match '^(DOTNET|COREHOST|COMPlus|CORECLR|MSBUILD|NUGET)_') {
            [void]$info.Environment.Remove($key)
        }
    }
    $info.Environment['PATH'] = "$env:SystemRoot\System32;$env:SystemRoot"
    if ($ConfigurePortableEnvironment) {
        $info.Environment['DOTNET_ROOT'] = Join-Path $testRoot 'Runtime'
        $info.Environment['DOTNET_ROOT_X64'] = Join-Path $testRoot 'Runtime'
        $info.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
        $info.Environment['DOTNET_ROLL_FORWARD'] = 'LatestPatch'
        $info.Environment['PATH'] = (Join-Path $testRoot 'Runtime') + ';' + (Join-Path $testRoot 'App') + ';' + $info.Environment['PATH']
    }
    $info.Environment['TEMP'] = Join-Path $testRoot 'Data/Temp'
    $info.Environment['TMP'] = $info.Environment['TEMP']
    $process = [Diagnostics.Process]::Start($info)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) {
            $process.Kill($true)
            throw "Portable process timed out: $Executable"
        }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne $ExpectedExit) {
            throw "Portable process failed ($($process.ExitCode), expected $ExpectedExit): $output"
        }
        return $output
    }
    finally { $process.Dispose() }
}

$testRoot = Join-Path $root ('artifacts/便携 测试 & isolated-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory("$package.zip", $testRoot)
foreach ($file in $manifest.files.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath (Join-Path $testRoot $file.Name) -Algorithm SHA256).Hash -ne $file.Value) {
        throw "Extracted portable file hash mismatch: $($file.Name)"
    }
}
$before = Get-ProfileSnapshot
$sdks = Invoke-IsolatedProcess (Join-Path $testRoot 'Runtime/dotnet.exe') '--list-sdks'
if ($sdks.Trim()) { throw "Portable runtime unexpectedly exposes SDKs: $sdks" }
$runtimes = Invoke-IsolatedProcess (Join-Path $testRoot 'Runtime/dotnet.exe') '--list-runtimes'
if (-not $runtimes.Contains((Join-Path $testRoot 'Runtime/shared/Microsoft.NETCore.App'))) {
    throw "Runtime did not resolve inside the portable folder: $runtimes"
}
$null = Invoke-IsolatedProcess "$env:SystemRoot\System32\cmd.exe" ('/d /s /c ""' + (Join-Path $testRoot 'Start.cmd') + '" --smoke-portable"')
$null = Invoke-IsolatedProcess "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
    ('-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $testRoot 'Start.ps1') + '" -SmokePortable') `
    -ConfigurePortableEnvironment $false
$result = Get-Content -LiteralPath (Join-Path $testRoot 'Data/portable-check.json') -Raw | ConvertFrom-Json
foreach ($name in @('SettingsDirectory', 'LogDirectory', 'PdfCacheDirectory', 'TemporaryDirectory', 'RuntimeDirectory')) {
    if (-not $result.$name.StartsWith($testRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Storage/runtime escaped portable copy: $name"
    }
}
if (-not ($result.ParserHandlePreview -and $result.RasterHandshake -and $result.UiResources -and $result.AutoStartAvailable)) {
    throw 'A portable host or startup check did not pass.'
}

# Existing thumbnail smoke exercises ShellBroker and its per-host local TEMP as well.
$shell = Join-Path $testRoot 'App/QuickLook.Next.ShellBroker.exe'
$image = Join-Path $testRoot 'App/Assets/StoreLogo.png'
$null = Invoke-IsolatedProcess (Join-Path $testRoot 'App/QuickLook.Next.App.exe') `
    ('--smoke-shell-broker "' + $shell + '" "' + $image + '"')

# A redirected Data must fail before writing to the outside directory.
Move-Item -LiteralPath (Join-Path $testRoot 'Data') -Destination (Join-Path $testRoot 'Data.checked')
$outside = Join-Path $root ('artifacts/portable-no-write-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($outside) | Out-Null
$junction = Join-Path $testRoot 'Data'
New-Item -ItemType Junction -Path $junction -Value $outside | Out-Null
try {
    $null = Invoke-IsolatedProcess "$env:SystemRoot\System32\cmd.exe" `
        ('/d /s /c ""' + (Join-Path $testRoot 'Start.cmd') + '" --smoke-portable"') -ExpectedExit 32
    if (@(Get-ChildItem -LiteralPath $outside -Force).Count) { throw 'Startup wrote through redirected Data.' }
}
finally {
    # Remove only the junction itself, never recurse through the target.
    [IO.Directory]::Delete($junction)
    Move-Item -LiteralPath (Join-Path $testRoot 'Data.checked') -Destination (Join-Path $testRoot 'Data')
}
if ((Get-ProfileSnapshot) -cne $before) { throw 'Existing profile/startup data changed during the portable checks.' }
Write-Host "Portable package checks passed: manifest, clean runtime, three hosts, local storage, redirected-Data rejection, unchanged profile/startup."
Write-Host "Evidence retained in $testRoot"
