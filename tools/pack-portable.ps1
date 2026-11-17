#requires -Version 7.0
param(
    [Parameter(Mandatory = $true)][string]$RuntimeRoot,
    [Parameter(Mandatory = $true)][string]$VCRuntimeDirectory,
    [Parameter(Mandatory = $true)][string]$VCRedistributionNotice,
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'checked-invocation.ps1')
. (Join-Path $PSScriptRoot 'release-payload.ps1')
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root 'artifacts'
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
$runtimeDirectory = Get-ChildItem (Join-Path $RuntimeRoot 'shared/Microsoft.NETCore.App') -Directory |
    Where-Object { $_.Name -match '^10\.0\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $runtimeDirectory) { throw 'A local .NET 10 runtime is required for bundling.' }
$runtimeVersion = $runtimeDirectory.Name
$fxrDirectory = Join-Path $RuntimeRoot "host/fxr/$runtimeVersion"
if (-not (Test-Path (Join-Path $fxrDirectory 'hostfxr.dll') -PathType Leaf)) {
    throw 'The matching runtime hostfxr is missing.'
}
$name = "QuickLook.Next-$version-portable-win-x64"
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $artifacts $name }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$artifactPrefix = [IO.Path]::GetFullPath($artifacts).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Portable output must be a new directory under artifacts.'
}
if (Test-Path -LiteralPath $output) { throw 'Output already exists; choose a new directory to preserve portable user data.' }
$archive = "$output.zip"
if (Test-Path -LiteralPath $archive) { throw 'The output archive already exists.' }

# Validate the existing tested-build proof before copying any application files.
$proof = Get-Content (Join-Path $artifacts '.tested-release-build.json') -Raw | ConvertFrom-Json
$commit = (git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $proof.commit -ne $commit -or
    $proof.payloadSchemaVersion -ne 1 -or $proof.versionPrefix -ne $version -or $proof.versionSuffix) {
    throw 'Portable packaging requires a matching tested release-build proof.'
}
$payload = @(Get-QuickLookReleasePayload -Root $root -ArtifactsDirectory $artifacts)
Assert-QuickLookReleasePayloadProof -Payload $payload -ProofOutputs $proof.outputs
$crtFiles = @(Get-ChildItem -LiteralPath $VCRuntimeDirectory -File -Filter '*.dll')
foreach ($required in @('vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll')) {
    if ($required -notin $crtFiles.Name) { throw "Visual C++ runtime is missing $required" }
}
if (-not (Test-Path -LiteralPath $VCRedistributionNotice -PathType Leaf)) {
    throw 'Visual C++ redistribution notice is required.'
}

$app = Join-Path $output 'App'
$runtime = Join-Path $output 'Runtime'
Copy-QuickLookReleasePayload -Payload $payload -DestinationRoot $app
Assert-QuickLookReleasePayloadProof -Payload $payload -ProofOutputs $proof.outputs -ContentRoot $app
foreach ($directory in @('host/fxr', 'shared/Microsoft.NETCore.App')) {
    New-Item -ItemType Directory -Path (Join-Path $runtime $directory) -Force | Out-Null
}
Copy-Item -LiteralPath $fxrDirectory -Destination (Join-Path $runtime 'host/fxr') -Recurse
Copy-Item -LiteralPath $runtimeDirectory.FullName -Destination (Join-Path $runtime 'shared/Microsoft.NETCore.App') -Recurse
foreach ($file in @('dotnet.exe', 'LICENSE.txt', 'ThirdPartyNotices.txt')) {
    Copy-Item -LiteralPath (Join-Path $RuntimeRoot $file) -Destination (Join-Path $runtime $file)
}
foreach ($destination in @($app, (Join-Path $app 'ParserHost'), (Join-Path $app 'RasterHost'))) {
    foreach ($crtFile in $crtFiles) { Copy-Item -LiteralPath $crtFile.FullName -Destination $destination }
}
New-Item -ItemType Directory -Path (Join-Path $output 'Licenses') | Out-Null
Copy-Item -LiteralPath $VCRedistributionNotice -Destination (Join-Path $output 'Licenses/VisualCpp-Redistribution.txt')
Copy-Item -LiteralPath (Join-Path $root 'packaging/portable/Runtime-Notices.txt') -Destination (Join-Path $output 'Licenses/README.txt')
foreach ($file in @('LICENSE', 'ACKNOWLEDGEMENTS.md', 'THIRD-PARTY-NOTICES.txt')) {
    Copy-Item -LiteralPath (Join-Path $app $file) -Destination (Join-Path $output $file)
}
foreach ($file in @('Start.cmd', 'Start.ps1', 'README.txt')) {
    Copy-Item -LiteralPath (Join-Path $root "packaging/portable/$file") -Destination $output
}
[IO.File]::WriteAllText((Join-Path $output 'portable.mode'), '')
New-Item -ItemType Directory -Path (Join-Path $output 'Data') | Out-Null
$files = @(Get-ChildItem -LiteralPath $output -File -Recurse)
$size = ($files | Measure-Object Length -Sum).Sum
if ($size -gt 350MB) { throw 'Portable payload exceeds its 350 MiB runtime-inclusive budget.' }
if (Test-Path (Join-Path $runtime 'sdk')) { throw 'Developer SDKs must not enter a portable payload.' }
$hashes = [ordered]@{}
$prefix = $output.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
foreach ($file in $files | Sort-Object FullName) {
    $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
    $hashes[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
[ordered]@{
    version = $version
    edition = 'YoungJune01'
    repository = 'YoungJune01/QuickLook.Next'
    sourceCommit = $commit
    sourceWorktreeDirty = (@(git -C $root status --porcelain).Count -gt 0)
    dotnetRuntime = $runtimeVersion
    visualCppRuntime = $crtFiles[0].VersionInfo.FileVersion
    bytes = $size
    files = $hashes
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'portable-manifest.json') -Encoding utf8

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($output, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$archiveHash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
Write-Host "Portable package: $archive ($([math]::Round($size / 1MB, 1)) MiB unpacked)"
