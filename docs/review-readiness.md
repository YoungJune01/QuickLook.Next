# QuickLook.Next Review Readiness

This page records the current local implementation and verification. Maintenance
commands, launch/rollback locations, and source history are in
[fork-maintenance.md](fork-maintenance.md); remaining priorities are in
[implementation-todo.md](implementation-todo.md). Detailed upstream release and
completed-task evidence remains at local Git commit `54302d0`.

## Functional baseline: 2026-11-13

Source `38271f4` contains four sequential fixes over the downloaded `1.0.4`
snapshot (`396fbbf`). The upstream source commit is not established. This local
repository has no configured remote and has not published or installed an MSIX.

| Change | Implementation and regression evidence | Owner acceptance |
|---|---|---|
| Desktop / active Explorer tab (`f574bf7`) | Rust recognizes desktop Shell views and matches the active tab browser instead of the shared frame HWND. Checks foreground/tab state and cancellation before returning selection. | Reported desktop and multi-tab scenarios passed. |
| Workbook rows / sheets (`008b0b3`) | Normal geometry with horizontal scrolling; stable XAML footer materializes one sheet at a time. Rust resolves names/order, empty sheets, and nonsequential parts. | Reported row-height and sheet-navigation scenarios passed. |
| Oversized workbook (`ac931f4`) | Rust returns typed `LimitExceeded`; ParserHost sends `preview.content.limit_exceeded`. Localized, generation-bound UI offers Open File without reopening a broader-authority input. | Reported oversized-file prompt passed. |
| Blank cells / merges (`38271f4`) | Rust `workbook/cells.rs` preserves paired/self-closing empty cells and empty merge anchors, deduplicates coordinates, clips spans, and checks arithmetic/cancellation. Eight focused regressions. | Reported missing-grid scenario passed. |

Workbook budgets remain 48 rows, 32 columns, six sheets, and 16 MiB per worksheet
or shared-string XML part after decompression. A compact 50,000-row workbook can
still yield a bounded preview; row count alone does not cause rejection.
Read-only probes of the reported files verified typed limits and retained blank
geometry without changing the files or committing their private contents.

The pre-cleanup baseline passed 390 Rust tests with one pre-existing ignored
external-corpus test and 557 managed tests (Core 420, ParserHost 52, RasterHost 72,
ShellBroker 13). Native MSBuild and managed Release builds had zero warnings or
errors. Format, all-target/all-feature Clippy, full architecture `-SkipDist`,
and three-locale parity (484 keys) passed. These are code checks, not completion
of every row in the [manual checklist](desktop-regression-checklist.md).

## Local-maintenance cleanup: 2026-11-13

Removed Store candidate workflow, Store identity/version/upload tooling, listing
and submission documents; marketing website and its npm/Pages setup; unused
reference plugins; redundant historical roadmaps and completed release narratives.
Current docs retain the active queue, baseline, ADRs, detailed HANDLE/ABI state,
format registry, visual acceptance, and focused image/diagnostic guidance.

The App, Core, Contracts, three hosts, Rust sources, tests, runtime assets,
runtime dependency graphs/locks, and toolchain pins are unchanged. Optional MSIX sideload
installation remains independent of Store submission; its certificate/signature
validation is retained. `StoreLogo.png` remains a referenced Windows package asset.
Ordinary local launch requires no certification or certificate installation.

CI retains NuGet/Cargo auditing, pinned actions/toolchains, and application checks.
Guards remove only requirements for deleted components, narrow legacy parser-API
exceptions, reject retired distribution paths, and retain production safety gates.
Historical roadmap placement assertions now inspect the active maintenance queue.

Cleanup verification passed on 2026-11-13:

| Check | Result |
|---|---|
| Rust format / all-target, all-feature locked Clippy | Passed, warnings denied |
| Rust workspace tests | 390 passed, 1 pre-existing ignored |
| Native MSBuild / managed Release build | Passed; managed build 0 warnings/errors |
| Serialized .NET solution tests | 557 passed: Core 420, ParserHost 52, RasterHost 72, ShellBroker 13 |
| Full architecture `-SkipDist` | Passed, including ABI mutation/behavior, restricted-host, payload, installer, UI, and localization checks |
| Documentation / whitespace | Relative paths and heading links passed; `git diff --check` passed |
| Accepted runtime payload | All 185 file hashes and launcher target unchanged; rebuilt native DLL matches accepted SHA-256 |

Logs are retained under `artifacts/cleanup-*.log`; payload/source checks are in
`artifacts/cleanup-payload-verification.json`. The working tree retains 519 of
561 tracked files, with tracked-file bytes reduced by approximately 36%.
This measures source-tree contents, excluding Git history, build caches, tools,
and preserved local payloads. Cleanup does not claim a new installation or UI run.

## Portable deployment: 2026-11-13

`portable.mode` opts into package-local `Data/` for settings, welcome state, logs,
PDF cache, temporary files, and per-host work. Installed/development storage
locations are preserved. Startup refuses unwritable or redirected portable data
instead of falling back to the profile. Portable startup controls are disabled;
no startup registry or shortcut operations run. Host restrictions, local write
grants, HANDLE ownership, cancellation, and parser budgets are retained.

`tools/pack-portable.ps1` uses the tested release-payload proof and adds one shared
.NET 10.0.10 x64 runtime plus app-local Visual C++ libraries and their notices.
Windows App SDK was already self-contained in the App output. The portable
runtime-inclusive budget is 350 MiB; the ordinary release budget is unchanged.
The delivery root has an empty Data directory, a file-hash manifest, ZIP, and
SHA-256 sidecar. No developer SDK, certificate, or private workbook is bundled.

Verification passed on 2026-11-13:

| Check | Result |
|---|---|
| Rust format / locked all-target, all-feature Clippy / tests | Passed; 390 tests, one pre-existing ignored |
| Native MSBuild / managed Release | Passed; no managed warnings or errors |
| Serialized .NET tests | 567 passed: Core 430 (10 new storage cases), ParserHost 52, RasterHost 72, ShellBroker 13 |
| Full architecture `-SkipDist` | Passed, including compiled mutations, restricted-host launch, image corpus and UI contracts |
| Localization | Three locales, 485 keys, placeholders in parity |
| Extracted portable package | Hashes/checksum passed; Chinese/space/ampersand path; developer PATH removed; runtime resolves inside package; zero SDKs exposed |
| Portable runtime behavior | WinUI resources, actual ParserHost HANDLE preview, RasterHost authentication, ShellBroker thumbnail, settings/state/logs/cache/temp paths passed |
| Storage boundaries | Redirected Data rejected before writes; existing profile files and startup configuration unchanged during smoke checks |

Logs are retained in `artifacts/portable-*.log`; reproducible package checks use
`tools/test-portable-package.ps1`. This verifies isolation on the development
computer, not a run on another physical computer. GUI acceptance on the final
portable build remains with the owner. See [portable.md](portable.md) and
[ADR-0005](adr/0005-portable-storage.md). Windows-managed history and optional
system codecs remain outside application storage ownership.

## Runtime and delivery boundaries

Rust owns probing and bounded structured parsing. WinUI owns presentation and
lifecycle; ParserHost owns transport/HANDLE adoption/FFI, RasterHost owns Windows
raster surfaces, and ShellBroker owns explicit Shell compatibility. See
[AGENTS.md](../AGENTS.md), [ADRs](adr/README.md), and
[HANDLE input details](handle-based-preview-inputs.md).

The retained owner-accepted payload is
`artifacts/local-preview-20261113-workbook-blanks/`, with 185 files and a verified
copy manifest. Launcher: `artifacts/Start-MyQuickLook.cmd`. Native DLL SHA-256:
`3B2FDA3A7E2C4BFAD201F2717F8789521837E9F085AFBAD87B908D6DCABD0DFA`.
Previous local payloads and logs are retained for rollback. Future build output
must be reassembled and verified before it replaces an accepted payload.

Manual acceptance belongs to the owner. Repeated selection/sheet switching,
empty-selection, theme/accessibility, cancellation/error recovery, physical HDR,
and controlled resource/performance runs remain separate evidence. Historical
RasterHost resource-test fluctuations remain open, without relaxed limits.
