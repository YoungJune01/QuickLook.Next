#requires -Version 7.0
param(
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [string]$OutputDirectory = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'QuickLook.Next-Releases')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$archive = Get-Item -LiteralPath $ArchivePath
if ($archive.Name -cnotmatch '^QuickLook\.Next-((?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))-portable-win-x64\.zip$') {
    throw 'Select a versioned QuickLook Next portable ZIP, not a source ZIP.'
}
$version = $Matches[1]
$name = [IO.Path]::GetFileNameWithoutExtension($archive.Name)
$checksumPath = $archive.FullName + '.sha256'
$checksum = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
$hash = (Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if ($checksum -cne "$hash  $($archive.Name)") { throw 'ZIP checksum does not match.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$destination = Join-Path $output "v$version"
if (Test-Path -LiteralPath $destination) { throw 'Version directory already exists; existing packages and Data will not be overwritten.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
for ($ancestor = Get-Item -LiteralPath $output; $null -ne $ancestor; $ancestor = $ancestor.Parent) {
    if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Release storage must not traverse a reparse point.' }
}
$stage = Join-Path $output ('.staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$package = Join-Path $stage $name

function Assert-PackagePath([string]$Relative) {
    $normalized = $Relative.Replace('\', '/')
    if ([string]::IsNullOrEmpty($normalized) -or $normalized.StartsWith('/') -or $normalized.Contains(':')) {
        throw 'Invalid archive path.'
    }
    foreach ($part in $normalized.TrimEnd('/').Split('/')) {
        if ($part -in @('', '.', '..') -or $part.EndsWith('.') -or $part.EndsWith(' ')) { throw 'Unsafe archive path.' }
    }
    return $normalized
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive.FullName)
    try {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $bytes = [long]0
        if ($zip.Entries.Count -gt 1500) { throw 'Too many files in portable ZIP.' }
        foreach ($entry in $zip.Entries) {
            $relative = Assert-PackagePath $entry.FullName
            if (-not $seen.Add($relative.TrimEnd('/'))) { throw 'Duplicate archive path.' }
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'Archive links are not allowed.' }
            $bytes += $entry.Length
            if ($bytes -gt 350MB) { throw 'Portable ZIP exceeds the uncompressed size budget.' }
        }
    } finally { $zip.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($archive.FullName, $package)
    $manifest = Get-Content -LiteralPath (Join-Path $package 'portable-manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.version -cne $version -or $manifest.edition -cne 'YoungJune01' -or
        $manifest.repository -cne 'YoungJune01/QuickLook.Next' -or
        $manifest.sourceWorktreeDirty -isnot [bool] -or $manifest.sourceWorktreeDirty -or
        $manifest.sourceCommit -cnotmatch '^[0-9a-f]{40}$') {
        throw 'Package identity, version, or source provenance does not match this maintained edition.'
    }
    $payload = @($manifest.files.PSObject.Properties)
    $payloadNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $payload) {
        $relative = Assert-PackagePath $file.Name
        if (-not $payloadNames.Add($relative) -or $relative -like 'Data/*' -or $file.Value -notmatch '^[0-9A-Fa-f]{64}$') {
            throw 'Invalid portable file manifest.'
        }
        if ((Get-FileHash -LiteralPath (Join-Path $package $relative) -Algorithm SHA256).Hash -ine $file.Value) {
            throw "Portable payload checksum mismatch: $relative"
        }
    }
    foreach ($required in @('Start.cmd', 'portable.mode', 'App/QuickLook.Next.App.exe', 'Runtime/dotnet.exe', 'LICENSE', 'ACKNOWLEDGEMENTS.md')) {
        if (-not $payloadNames.Contains($required)) { throw "Portable package is missing $required" }
    }
    $actualFiles = @(Get-ChildItem -LiteralPath $package -Recurse -File -Force)
    if ($actualFiles.Count -ne $payload.Count + 1) { throw 'Portable ZIP has unlisted files or nonempty Data.' }
    Copy-Item -LiteralPath $archive.FullName -Destination (Join-Path $stage $archive.Name)
    Copy-Item -LiteralPath $checksumPath -Destination (Join-Path $stage ($archive.Name + '.sha256'))
    if ((Get-FileHash -LiteralPath (Join-Path $stage $archive.Name) -Algorithm SHA256).Hash -ine $hash) { throw 'Stored ZIP checksum mismatch.' }

    $stageFull = [IO.Path]::GetFullPath($stage)
    $outputPrefix = $output.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $stageFull.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetDirectoryName($destination) -ine $output) { throw 'Invalid release storage destination.' }
    Move-Item -LiteralPath $stageFull -Destination $destination
    $relativePackage = "v$version\$name"
    $launcher = "@echo off`r`nsetlocal DisableDelayedExpansion`r`ncall `"%~dp0$relativePackage\Start.cmd`" %*`r`n"
    [IO.File]::WriteAllText((Join-Path $output 'Start.cmd'), $launcher, [Text.Encoding]::ASCII)
    [ordered]@{
        version = $version; edition = 'YoungJune01'; repository = $manifest.repository
        sourceCommit = $manifest.sourceCommit; package = $relativePackage
        archive = "v$version\$($archive.Name)"; sha256 = $hash
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'current.json') -Encoding utf8
    @"
QuickLook Next $version - YoungJune01 维护版

双击此目录的 Start.cmd 启动当前便携版。先从托盘退出其他 QuickLook 实例。
此目录集中保存最终 ZIP、校验文件和解压后的软件；v$version 是当前版本目录。
设置、日志位于 $relativePackage\Data；升级不覆盖旧版 Data。
原作者：SherlockChiang，感谢原作者及上游贡献者。
下载与更新：https://github.com/YoungJune01/QuickLook.Next/releases

Run Start.cmd to launch the current YoungJune01 edition. Exit other copies first.
Each version keeps its ZIP, checksum and extracted package in its own directory.
Existing versions and their Data are never overwritten by this storage helper.
"@ | Set-Content -LiteralPath (Join-Path $output 'README.txt') -Encoding utf8
    Write-Host "Portable release saved: $destination"
    Write-Host "Fixed launcher: $(Join-Path $output 'Start.cmd')"
}
finally {
    if (Test-Path -LiteralPath $stage) {
        $stageFull = [IO.Path]::GetFullPath($stage)
        $prefix = $output.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        if (-not $stageFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($stageFull) -notmatch '^\.staging-[0-9a-f]{32}$' -or
            ((Get-Item -LiteralPath $stageFull).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Refusing cleanup outside the temporary release staging directory.'
        }
        Remove-Item -LiteralPath $stageFull -Recurse -Force
    }
}
