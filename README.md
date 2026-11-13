# QuickLook Next

[简体中文](README_CN.md) · [Local maintenance guide (中文)](docs/fork-maintenance.md)

A Windows x64 file previewer maintained locally from the upstream `1.0.4`
source snapshot. Select a file on the desktop or in the active Explorer tab and
press **Space**. The reported desktop, Explorer-tab, and Excel preview fixes
have passed owner acceptance.

## Run and build

For another Windows x64 computer, extract the complete portable ZIP and run
`Start.cmd`. It carries its runtime and keeps application data in `Data/`.
See [portable use and packaging](docs/portable.md). Exit other QuickLook instances
first. The previous local launcher, `artifacts/Start-MyQuickLook.cmd`, is retained
for rollback; rebuilding source does not automatically update existing packages.

On Windows, prepare the pinned .NET SDK, Rust MSVC toolchain, Desktop C++ tools,
and Windows SDK described in [CONTRIBUTING.md](CONTRIBUTING.md), then run:

```powershell
pwsh -NoProfile -File tools/doctor.ps1
pwsh -NoProfile -File build.ps1 -Test
```

On this computer, first load `. .\artifacts\tools\Enter-Dev.ps1` in PowerShell.
For full release checks without signing or installation:

```powershell
pwsh -NoProfile -File tools/release.ps1 -SkipPackage
```

`tools/pack-portable.ps1` builds the complete portable ZIP from tested outputs;
`tools/pack-release.ps1` assembles the base release payload. Both retain licenses
and notices. Optional signed MSIX sideload tooling remains in
`packaging/` and `tools/`; it needs your own signing setup. No Store submission,
certification, or Partner Center configuration is required for local use.

## Features and controls

- Images and animations, PDF, approximate DOCX/XLSX/PPTX layouts.
- Text, code, Markdown, CSV/TSV, SQLite, archives, packages, and folders.
- System-supported audio/video, fonts, certificates, executables, mail, and ebooks.
- Cloud-download consent, cancellation, bounded parsing, and isolated preview hosts.
- `Space`: toggle preview; `Esc`: close; `F5`: reload; `F11`: full screen.
- Image wheel or `+`/`-`: zoom; `Home` or `Ctrl+0`: reset; `Ctrl+F`: text search.

Excel uses a fixed footer for sheet selection and preserves normal row height,
blank cells, and merged regions. Previews retain at most 48 rows, 32 columns,
and six sheets. Oversized worksheet XML shows an explanatory error and an Open
File action. Office previews do not execute macros or recalculate formulas.
Some image/media formats require optional Windows codecs.

## Maintenance and architecture

`src/` contains the WinUI shell, shared models, and ParserHost, RasterHost, and
ShellBroker. `native/quicklook_next_native/` owns probing and bounded structured
parsing; `tests/`, `testdata/`, and `tools/` provide verification and fixtures.
The retired website, legacy plugins, and publication history remain in Git.

[AGENTS.md](AGENTS.md) and the [ADRs](docs/adr/README.md) define architecture
boundaries. See [review status](docs/review-readiness.md),
[manual acceptance](docs/desktop-regression-checklist.md), and
[remaining work](docs/implementation-todo.md). Report bugs with the commit,
file type/size, reproduction steps, and expected/actual behavior; use synthetic
samples and keep private documents out of the repository.

## Security and license

Follow [SECURITY.md](SECURITY.md) for private vulnerability reporting and
[CONTRIBUTING.md](CONTRIBUTING.md) for contribution requirements.
Original source and assets use the [MIT License](LICENSE); bundled dependencies
retain their own licenses and notices. Upstream project: `SherlockChiang/QuickLook.Next`.
