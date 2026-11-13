# Verification Baseline

Current implementation evidence is in [review-readiness.md](review-readiness.md).
This page defines how to reproduce local checks; historical release narratives
and measurements remain available at local Git commit `54302d0`.

## Pinned Local Toolchain

Use Windows x64, .NET SDK `10.0.302`, Rust `1.96.0` with the
`x86_64-pc-windows-msvc` target, Desktop C++/MSVC, Windows SDK, and PowerShell 7+.
The repository pins remain in `global.json` and `rust-toolchain.toml`.

On the current computer, from the repository root:

```powershell
. .\artifacts\tools\Enter-Dev.ps1
pwsh -NoProfile -File tools/doctor.ps1
```

The ignored environment script sets tool paths for the current process only.
On another computer, follow [CONTRIBUTING.md](../CONTRIBUTING.md). Missing tools
are environment failures; do not relax pins or guards to work around them.

## Code and boundary checks

The authoritative commands are listed in [AGENTS.md](../AGENTS.md).
`tools/release.ps1 -SkipPackage` runs the full release-oriented sequence without
signing or installing. The native DLL must build through
`native/QuickLook.Next.Native.proj`; do not add a raw Cargo build path.
Serialize .NET integration projects with `--maxcpucount:1` to avoid measuring
cross-project load against independent host deadlines.

The architecture gate covers ABI, ownership, cancellation/stale work,
restricted-host launch, UI contracts, localization, parser/module/resource
bounds, payload proofs, and retained build/distribution workflows. Removed
Store/website checks apply to no retained component; product safety checks stay.

## Evidence and interpretation

Record the source commit, dirty changes, toolchain, commands, first failures,
and final results. Store local logs in ignored `artifacts/`; never commit private
samples, signing secrets, or identifying cell contents.

Before cleanup, code `38271f4` passed Rust 390 tests (one existing ignored),
.NET 557 tests, zero-warning Release builds, and format/Clippy/architecture gates.
Cleanup verification is recorded separately in the current review status.

Owner acceptance covers the reported desktop, Explorer-tab, and Excel scenarios.
[Manual boundary combinations](desktop-regression-checklist.md) remain explicit.
No claim is made for a new MSIX installation, physical HDR, all optional system
codecs, or controlled performance measurements unless separately executed.

Historical `E26-04` HANDLE-budget fluctuations and metadata-child pipe closure
remain follow-up work: passing unchanged reruns do not explain a prior failure.
New failures must retain their first logs and exit codes rather than be hidden by
retries, broader privileges, or increased budgets.
