#requires -Version 7.0
param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [Parameter(Mandatory = $true)][string]$Repository,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$properties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$expectedUrl = [string]$properties.Project.PropertyGroup.RepositoryUrl
if ("https://github.com/$Repository" -cne $expectedUrl) {
    throw 'The publishing repository must match the configured application update source.'
}
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw 'Portable stable updates require a semantic X.Y.Z version.'
}
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $package 'portable-manifest.json') -Raw | ConvertFrom-Json
$commit = (git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $manifest.version -cne $version -or
    $manifest.sourceCommit -cne $commit -or $manifest.sourceWorktreeDirty -isnot [bool] -or
    $manifest.sourceWorktreeDirty -cne $false) {
    throw 'Portable update metadata requires a package from the current, clean source commit.'
}
$archive = "$package.zip"
$name = "QuickLook.Next-$version-portable-win-x64.zip"
if ([IO.Path]::GetFileName($archive) -cne $name) {
    throw 'Portable archive name does not match VERSION.'
}
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = (Get-Content -LiteralPath "$archive.sha256" -Raw).Trim()
if ($checksum -cne "$hash  $name") {
    throw 'Portable archive checksum is inconsistent.'
}
$metadata = [ordered]@{
    schemaVersion = 1
    version = $version
    tag = "v$version"
    channel = 'stable'
    architecture = 'x64'
    minimumWindowsBuild = 17763
    publishedAt = [DateTimeOffset]::UtcNow.ToString('o')
    downloadUrl = "$expectedUrl/releases/download/v$version/$name"
    file = $name
    sha256 = $hash
}
$output = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path $output -Parent) -Force | Out-Null
$metadata | ConvertTo-Json | Set-Content -LiteralPath $output -Encoding utf8NoBOM
Write-Host "Portable update metadata: $output"
