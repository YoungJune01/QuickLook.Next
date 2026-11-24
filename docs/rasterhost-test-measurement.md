# RasterHost duplex and resource measurements

The mixed-format cancellation test must keep reading the duplex pipe while it
sends close/reopen controls. The App already has an independent receive loop.
RasterHost publishes surface/ready/thumbnail output under its surface gate;
the control loop also acquires that gate for close/open. A test that waits to
read until all its writes finish can fill both pipe directions and deadlock.
Changing a dependency or extending the deadline does not fix that wait cycle.

There is also a framing requirement: RasterHost checks request cancellation
before publishing a thumbnail, then commits that frame without the request
token, as surface/ready publication already does. Host supervision bounds a
stalled transport. Cancelling a partial thumbnail write can otherwise splice
its Base64 payload into the next message. The general `PipeChannel.SendAsync`
still honors in-flight cancellation deadlines, but closes the transport on a
canceled/failed write so a partial frame cannot be reused. A controlled stream
regression pauses after a prefix and verifies prompt cancellation, transport
closure, and rejection of subsequent writes. A second regression verifies a
canceled queued frame writes no bytes and leaves the channel usable.
The thumbnail-priority guard requires the readiness ordering, gate acquisition
and release, cancellation/current-request checks, and uncancelable frame commit.
Its fixture accepts the real source and rejects six independent text mutations
that remove those checks or restore the unsafe request token on the wire write.

`RasterHostMessageReader` drains into a bounded 16-message queue, with one reader,
one writer, and an awaited cancellation lifetime. The consumer still checks the
replacement request IDs, releases stale surface transfers, verifies the source
remains pinned until close, and checks clean process exit. The additional
1-KiB-buffer cancellation case exercises backpressure without increasing the
45-second deadline. The default-buffer cases remain the latency-report inputs.

The image HANDLE test measures ongoing growth rather than assuming a fixed
preview count completes .NET/D3D worker startup. After the existing 64-preview
warmup, it samples three fixed windows of 32 previews and compares the second
and third peaks with the first peak. The budget remains 12 handles. Thread
counts are diagnostic only; they never increase or subtract from that budget.
There is no adaptive rebaseline, retry-until-pass, or idle trim between windows.

For a persistent leak of one HANDLE per preview, the first and third window
peaks differ by 64 handles before unrelated runtime noise. This is a longer
observation interval than the previous eight-sample warmup peak versus 32
measured previews. `Image_handle_growth_probe_detects_an_unreleased_surface_per_preview`
is the executable positive control: it uses the same host, decoder, cycle loop,
and detector, but withholds the 96 measured surface-release messages. That stays
below the host's 128 pending-transfer limit, and process teardown reclaims the
deliberately retained handles. The control must detect growth; the normal test
must not. Source-file overwrite checks and clean shutdown apply to both.
The performance-bounds guard requires both controls to share the detector,
the fixed 64/32/3 sampling dimensions, and the unchanged 12-HANDLE budget.

Set `QL_RASTER_RESOURCE_REPORT_DIR` to retain every cycle's HANDLE/thread counts
as JSON for both controls. Reports are written after measurement, so filesystem
logging does not pace the measured previews. Example focused run:

```powershell
$env:QL_RASTER_RESOURCE_REPORT_DIR = "$PWD/artifacts/raster-resources"
dotnet test tests/QuickLook.Next.RasterHost.IntegrationTests -c Release `
  --no-build --no-restore `
  --filter 'FullyQualifiedName~Mixed_format_reopens|FullyQualifiedName~Repeated_image_handle|FullyQualifiedName~Image_handle_growth_probe'
```

These checks cover the generated image corpus and persistent per-preview
growth. They do not establish physical GPU/HDR behavior, every optional Windows
codec, or all historical metadata-child shutdown failures.
