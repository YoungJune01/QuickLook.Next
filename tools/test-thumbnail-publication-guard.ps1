param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'

$fixture = Join-Path $Root ('artifacts/thumbnail-publication-guard/' + [Guid]::NewGuid().ToString('N'))
$files = @(
    'src/QuickLook.Next.App/MainWindow.xaml.cs',
    'src/QuickLook.Next.App/ImageSidecarController.cs',
    'src/QuickLook.Next.App/NativeThumbnailScheduler.cs',
    'src/QuickLook.Next.App/ImageThumbnailCache.cs',
    'src/QuickLook.Next.Core/Protocol.cs',
    'src/QuickLook.Next.RasterHost/ImageThumbnailBuilder.cs',
    'src/QuickLook.Next.RasterHost/Program.cs',
    'native/quicklook_next_native/src/ffi/thumbnail.rs',
    'native/quicklook_next_native/src/win32/shell_thumbnail.rs')
foreach ($file in $files) {
    $destination = Join-Path $fixture $file
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $Root $file) -Destination $destination
}
$programPath = Join-Path $fixture 'src/QuickLook.Next.RasterHost/Program.cs'
$original = [IO.File]::ReadAllText($programPath)
$blockMatch = [regex]::Match($original, '(?s)PreviewImageThumbnail\? thumbnail =.*?(?=\r?\n\s*return await PublishImageWaveformAsync)')
if (-not $blockMatch.Success) { throw 'Could not locate the thumbnail publication fixture.' }
$block = $blockMatch.Value
$variants = [Collections.Generic.List[object]]::new()
$variants.Add([pscustomobject]@{ Name = 'positive'; Text = $original; Exit = 0 })
$mutations = [ordered]@{
    'missing-cancellation-check' = $block.Replace('cancellationToken.ThrowIfCancellationRequested();', '')
    'missing-current-request' = $block.Replace('open.RequestId, activeRequestId', 'open.RequestId, open.RequestId')
    'missing-gate-acquisition' = $block.Replace('await surfacePublishGate.WaitAsync(cancellationToken);', 'await Task.CompletedTask;')
    'missing-gate-release' = $block.Replace('surfacePublishGate.Release();', '')
    'cancelable-wire-frame' = $block.Replace('SendAsync(thumbnail)', 'SendAsync(thumbnail, cancellationToken)')
}
foreach ($entry in $mutations.GetEnumerator()) {
    if ($entry.Value -eq $block) { throw "Mutation did not change the fixture: $($entry.Key)" }
    $text = $original.Substring(0, $blockMatch.Index) + $entry.Value +
        $original.Substring($blockMatch.Index + $blockMatch.Length)
    $variants.Add([pscustomobject]@{ Name = $entry.Key; Text = $text; Exit = 1 })
}
$noReady = [regex]::new('(?s)await channel\.SendAsync\(new PreviewReady\(.*?\)\);').Replace(
    $original, 'await Task.CompletedTask;', 1)
if ($noReady -eq $original) { throw 'Readiness mutation did not change the fixture.' }
$variants.Add([pscustomobject]@{ Name = 'missing-readiness'; Text = $noReady; Exit = 1 })

$guard = Join-Path $Root 'tools/guard-thumbnail-priority.ps1'
foreach ($variant in $variants) {
    [IO.File]::WriteAllText($programPath, $variant.Text)
    & pwsh -NoProfile -File $guard -Root $fixture *> (Join-Path $fixture ($variant.Name + '.log'))
    if ($LASTEXITCODE -ne $variant.Exit) {
        throw "Thumbnail publication fixture $($variant.Name): expected exit $($variant.Exit), got $LASTEXITCODE. Logs: $fixture"
    }
}
Write-Host 'Thumbnail publication guard fixtures passed: 1 positive, 6 mutation rejections.'
exit 0
