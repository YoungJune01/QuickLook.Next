#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('quicklook-release-storage-' + [guid]::NewGuid().ToString('N'))
$packageName = 'QuickLook.Next-1.1.0-portable-win-x64'
$package = Join-Path $fixture $packageName
$archive = "$package.zip"
$output = Join-Path $fixture 'release & output'
$helper = Join-Path $PSScriptRoot 'save-portable-release.ps1'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-FixtureArchive {
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
    [IO.Compression.ZipFile]::CreateFromDirectory($package, $archive)
    Write-FixtureChecksum
}
function Write-FixtureChecksum {
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $packageName.zip" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
}
function Assert-Rejected([scriptblock]$Action, [string]$Reason) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw "Release storage accepted $Reason" }
}
try {
    $hashes = [ordered]@{}
    foreach ($relative in @('Start.cmd', 'portable.mode', 'App/QuickLook.Next.App.exe', 'Runtime/dotnet.exe', 'LICENSE', 'ACKNOWLEDGEMENTS.md')) {
        $path = Join-Path $package $relative
        New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
        [IO.File]::WriteAllText($path, 'synthetic test data; never executed')
        $hashes[$relative] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
    $manifest = [ordered]@{ version = '1.1.0'; edition = 'YoungJune01'; repository = 'YoungJune01/QuickLook.Next'; sourceCommit = ('a' * 40); sourceWorktreeDirty = $false; files = $hashes }
    $manifestPath = Join-Path $package 'portable-manifest.json'
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Write-FixtureArchive
    & $helper -ArchivePath $archive -OutputDirectory $output
    $current = Get-Content -LiteralPath (Join-Path $output 'current.json') -Raw | ConvertFrom-Json
    $storedPackage = Join-Path $output $current.package
    $data = Join-Path $storedPackage 'Data/settings.json'
    New-Item -ItemType Directory -Path (Split-Path $data -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($data, 'keep my settings')
    Assert-Rejected { & $helper -ArchivePath $archive -OutputDirectory $output } 'overwriting an existing version'
    if ([IO.File]::ReadAllText($data) -cne 'keep my settings') { throw 'Existing Data changed.' }
    if ((Get-Content -LiteralPath (Join-Path $output 'Start.cmd') -Raw) -notlike '*v1.1.0\QuickLook.Next-1.1.0-portable-win-x64\Start.cmd*') { throw 'The fixed launcher does not resolve the stored version.' }

    [IO.File]::AppendAllText($archive, 'tampered')
    Assert-Rejected { & $helper -ArchivePath $archive -OutputDirectory (Join-Path $fixture 'bad-checksum') } 'a ZIP with a mismatched checksum'
    $manifest.edition = 'Upstream'
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Write-FixtureArchive
    Assert-Rejected { & $helper -ArchivePath $archive -OutputDirectory (Join-Path $fixture 'bad-edition') } 'another edition'
    if (Test-Path -LiteralPath (Join-Path $fixture 'bad-edition/v1.1.0')) { throw 'An invalid edition was promoted.' }

    Remove-Item -LiteralPath $archive
    $zip = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Create)
    try { $zip.CreateEntry('../outside.txt') | Out-Null } finally { $zip.Dispose() }
    Write-FixtureChecksum
    Assert-Rejected { & $helper -ArchivePath $archive -OutputDirectory (Join-Path $fixture 'bad-path') } 'ZIP path traversal'
    if (Get-ChildItem -LiteralPath $fixture -Recurse -File -Filter 'outside.txt') { throw 'ZIP traversal wrote outside its package.' }
    Write-Host 'Portable release storage checks passed: fixed launcher, settings preservation, checksum, edition and ZIP paths.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    $prefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notmatch '^quicklook-release-storage-[0-9a-f]{32}$') { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
