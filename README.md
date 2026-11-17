# QuickLook Next · YoungJune01 Edition

[简体中文](README_CN.md) · [Downloads](https://github.com/YoungJune01/QuickLook.Next/releases) · [Acknowledgements](ACKNOWLEDGEMENTS.md)

A Windows x64 file previewer. Select a file on the desktop or in the active
Explorer tab and press **Space**.

This is an independently maintained edition by **YoungJune01**, based on the
upstream `1.0.4` source snapshot. Thank you to **SherlockChiang**, the original
author of [QuickLook.Next](https://github.com/SherlockChiang/QuickLook.Next), and
all upstream contributors. This repository manages its own source and releases.
Feature development is currently paused; issues and release maintenance are
managed here.

The maintained edition starts its own version line at **1.1.0**. The tray tooltip,
preview window and About page identify the YoungJune01 edition.

## Download and use

Download `QuickLook.Next-<version>-portable-win-x64.zip` from this repository's
[Releases](https://github.com/YoungJune01/QuickLook.Next/releases), extract the
complete ZIP to a writable local folder, and run `Start.cmd`. Exit other
QuickLook instances first. The package carries its runtime and keeps settings
and cache in `Data/`; no developer SDK or signing certificate is required.

Maintainer copies are collected in **`QuickLook.Next-Releases/`** beside the source
folder. Its **`Start.cmd`** launches the current saved version. Each `vX.Y.Z/` folder
contains the ZIP, SHA-256 checksum and extracted package. See [release management](docs/github-release.md).

Settings → About → Check for updates reads this repository's release feed.
It reports available versions; download and replacement are manual. To update,
exit the old copy, extract the new ZIP to a new folder, and copy the old `Data/`
to preserve settings. Disable the old folder's startup entry before moving it,
then enable startup in the new copy if desired. See [portable use](docs/portable.md).

## Features and controls

- Images and animations, PDF, approximate DOCX/XLSX/PPTX layouts.
- Text, code, Markdown, CSV/TSV, SQLite, archives, packages, and folders.
- System-supported audio/video, fonts, certificates, executables, mail, and ebooks.
- Cloud-download consent, cancellation, bounded parsing, and isolated preview hosts.
- `Space`: toggle preview; `Esc`: close; `F5`: reload; `F11`: full screen.
- Image wheel or `+`/`-`: zoom; `Home` or `Ctrl+0`: reset; `Ctrl+F`: text search.

This edition includes desktop/Explorer selection fixes, workbook row height and
sheet controls, blank/merged cell preservation, multiple preview windows,
DOCX text/image order, GBK tables, and portable startup support.
Excel previews retain at most 48 rows, 32 columns, and six sheets. Office
previews do not execute macros or recalculate formulas. Complex Office layouts
are approximate. Some image/media formats require optional Windows codecs.

## Source and maintenance

| Path | Purpose |
|---|---|
| `src/` | WinUI shell, shared models, and isolated preview hosts |
| `native/` | Rust probing, bounded parsers, and native interop |
| `tests/`, `testdata/` | Regression tests and redistributable fixtures |
| `tools/`, `packaging/` | Build, verification, and packaging |
| `.github/` | CI, dependency updates, and release workflows |
| `docs/` | Architecture decisions, verification evidence, and maintenance guides |

Build on Windows x64 with the pinned toolchains in
[CONTRIBUTING.md](CONTRIBUTING.md):

```powershell
pwsh -NoProfile -File tools/doctor.ps1
pwsh -NoProfile -File build.ps1 -Test
```

See [GitHub release management](docs/github-release.md) for portable releases,
and [maintenance guidance](docs/fork-maintenance.md) for the repository map.
Architecture rules are in [AGENTS.md](AGENTS.md) and the [ADRs](docs/adr/README.md).
Current evidence is in [review status](docs/review-readiness.md);
remaining work is in the [maintenance queue](docs/implementation-todo.md).
Local toolchains, logs, build caches, private samples, and release ZIPs are
excluded from Git. Release binaries belong in Releases.

## License and reporting

The original copyright notice and [MIT License](LICENSE) are retained.
Dependencies retain their own licenses and notices. See
[ACKNOWLEDGEMENTS.md](ACKNOWLEDGEMENTS.md) for project attribution,
[SECURITY.md](SECURITY.md) for private vulnerability reporting, and
[CONTRIBUTING.md](CONTRIBUTING.md) for contributions.
