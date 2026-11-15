#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('quicklook-portable-update-' + [guid]::NewGuid().ToString('N'))
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$commit = (git -C $root rev-parse HEAD).Trim()
$package = Join-Path $fixture "QuickLook.Next-$version-portable-win-x64"
$output = Join-Path $fixture 'update.json'
New-Item -ItemType Directory -Path $package -Force | Out-Null
$manifestPath = Join-Path $package 'portable-manifest.json'
$manifest = [ordered]@{ version = $version; sourceCommit = $commit; sourceWorktreeDirty = $false }
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
[IO.File]::WriteAllText("$package.zip", 'synthetic archive bytes')
$hash = (Get-FileHash -LiteralPath "$package.zip" -Algorithm SHA256).Hash.ToLowerInvariant()
$name = [IO.Path]::GetFileName("$package.zip")
"$hash  $name" | Set-Content -LiteralPath "$package.zip.sha256" -Encoding ascii

function Invoke-Fixture([string]$Repository = 'YoungJune01/QuickLook.Next') {
    & (Join-Path $PSScriptRoot 'new-portable-update-metadata.ps1') `
        -PackageDirectory $package -Repository $Repository -OutputPath $output
}

function Assert-Rejected([scriptblock]$Action, [string]$Reason) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw "Metadata accepted $Reason" }
    if (Test-Path -LiteralPath $output) { throw "Metadata wrote output for $Reason" }
}

try {
    Invoke-Fixture
    $result = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    if ($result.version -cne $version -or $result.sha256 -cne $hash -or
        $result.downloadUrl -cne "https://github.com/YoungJune01/QuickLook.Next/releases/download/v$version/$name") {
        throw 'Valid portable metadata lost its version, checksum, or pinned repository.'
    }
    Remove-Item -LiteralPath $output
    Assert-Rejected { Invoke-Fixture 'SherlockChiang/QuickLook.Next' } 'the upstream repository'
    Assert-Rejected { Invoke-Fixture 'YoungJune01/QuickLook.Next/../other' } 'a repository path suffix'
    foreach ($field in @('version', 'sourceCommit', 'sourceWorktreeDirty')) {
        $original = $manifest[$field]
        $manifest[$field] = if ($field -eq 'sourceWorktreeDirty') { $true } else { 'wrong' }
        $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
        Assert-Rejected { Invoke-Fixture } "an inconsistent $field"
        $manifest[$field] = $original
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $manifest.sourceWorktreeDirty = 'False'
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Assert-Rejected { Invoke-Fixture } 'a string in place of a clean-source boolean'
    $manifest.sourceWorktreeDirty = $false
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
    [IO.File]::WriteAllText("$package.zip", 'tampered archive')
    Assert-Rejected { Invoke-Fixture } 'a tampered archive'
    Write-Host 'Portable update metadata: valid feed and seven rejection scenarios passed.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'quicklook-portable-update-*') {
        throw 'Refusing fixture cleanup outside its temporary directory.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
