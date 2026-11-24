# QuickLook.Next Review Readiness

This page records the current local implementation and verification. Maintenance
commands, launch/rollback locations, and source history are in
[fork-maintenance.md](fork-maintenance.md); remaining priorities are in
[implementation-todo.md](implementation-todo.md). Detailed upstream release and
completed-task evidence remains at local Git commit `54302d0`.

## GitHub maintenance preparation: 2026-11-15

The independently maintained repository and update source are configured as
`YoungJune01/QuickLook.Next`. The App's project/download links, update feed, and
strict asset validation now share `ProjectRepository`. Update checks still only
report versions; downloading and replacing the portable package are manual.
The original MIT copyright remains unchanged. Bilingual acknowledgements and
all three About translations credit SherlockChiang and upstream contributors;
acknowledgements are included in the release payload and portable ZIP.

The manual `Portable Release` workflow builds and tests main, verifies the
runtime-inclusive portable package, then publishes its ZIP, SHA-256 and
`update.json`. It does not require the optional MSIX signing secrets. Portable
metadata rejects another repository, dirty-source package manifests, wrong
versions/commits and checksum mismatches. Existing signed-release safeguards
remain enabled.

First publication completed on 2026-11-15. The public `main` branch preserves all
nine local commits with their original hashes; all 549 source paths, modes and
blob hashes were compared with GitHub. Release `v1.0.4` targets source commit
`e25d4a74bb91267d883c4803311613805b910a08`. Hosted CI run `43676498801` and
Portable Release run `43676500538` both succeeded. Hosted checks include .NET
format, Rust fmt/Clippy, 400 Rust tests (one existing ignored), 606 .NET tests,
native MSBuild, full architecture checks and portable runtime/host/storage/
profile/startup verification. NuGet and Rust dependency audits completed.
`cargo audit` retained one allowed `rustix 1.1.4` xattr unsoundness warning
(`RUSTSEC-2026-0486`), reached through `tar -> xattr` in the all-target graph;
`cargo tree --locked --target x86_64-pc-windows-msvc -i rustix --offline`
confirms that this crate is absent from the Windows release target graph.

The stable ZIP is 92,370,583 bytes. Its GitHub asset digest, published checksum
and `update.json` hash agree:
`1b12b7e94b6deb4776e0dbf417aa0fe778943e398a9255bedd8ac3f6406f4f8e`.
The release is public, is not a prerelease, and is returned by the repository's
latest-release API. Published metadata was downloaded through GitHub CLI.
Direct access from this computer to the GitHub download hostname timed out;
the local proxy also failed its TLS handshake. These network failures do not
establish a successful end-to-end App update check on this computer.
Hosted logs and publication/hash receipts are retained in the local archive's
`github-publication/` directory. Physical UI acceptance remains separate.

Local verification passed: Rust fmt, all-target/all-feature Clippy, Rust 400
passed/one existing ignored, native MSBuild, managed Release build, .NET 606
passed, full architecture checks, and portable manifest/runtime/host/storage/
profile/startup checks. YAML and embedded workflow PowerShell parse successfully.
Focused checks verify the fork feed, portable assets, rejection of upstream and
other repositories, payload attribution, and seven metadata rejection scenarios.
The build reported seven NU1900 warnings because the local NuGet vulnerability
service was unreachable; these are not successful vulnerability audit results.
The hosted workflows retain dependency audits. Physical UI acceptance remains
the separate desktop checklist.

Local evidence: `github-preparation-checks.log`, `github-portable-package.log`
and `github-portable-checks.log` in the archived artifacts. Generated caches,
local toolchains and historical packages are moved outside the source tree;
their relocation is recorded in the local archive, preserving existing Data
and source changes. See [GitHub release management](github-release.md).

## DOCX image flow: 2026-11-15

The Rust document layout now walks `word/document.xml` in document order and
resolves embedded DrawingML/VML images through `document.xml.rels`. Text and
images interleave at each drawing/anchor, including repeated uses of one image
and images inside table cells. Unreferenced package media is no longer appended
as a gallery. DrawingML extents preserve the displayed aspect ratio and fit the
approximate page; floating drawings flow at their anchor rather than reproducing
Word's text wrapping. The final section-properties element is not visible text.

Existing limits remain: eight represented approximate pages, six attempted
image placements, 420 retained characters per text block, bounded image parts,
and the shared ZIP/decompression budgets. Failed image lookups consume the
attempt budget; external and unsafe references do not become authority to open
other files. The pinned-HANDLE/lazy-image transport and managed UI are unchanged.
No dependency, ABI, format-discovery or localization change was needed.

Synthetic regressions cover mixed runs, image ordering independent of media
filenames, repeated placements, table images, VML/alternate-content duplication,
unreferenced/missing/external/traversal targets, clipping and failed-lookup caps,
explicit page breaks, hostile dimensions, truncated XML and cancellation during
XML traversal. The existing HANDLE image-reference fixture now references its
image from the document instead of relying on an unreferenced media gallery.
Verification evidence is under `artifacts/logs/docx-inline-20261115/`; the private
reported document remains outside the repository.

Verification passed: Rust fmt, all-target/all-feature Clippy, Rust 400 passed/one
existing ignored, native MSBuild and managed Release builds (zero warnings/errors),
.NET 600 passed, full architecture `-SkipDist`, and diff checks. The focused .NET
format check returned success with the existing workspace-load warning. Portable
checks passed manifest/runtime/host/storage/isolation/profile/startup checks.

The reported local DOCX was checked through the production HANDLE export: all six
retained image references match the document order, text is interleaved, and the
borrowed HANDLE position is preserved. Orca desktop verification opened the same
document through Explorer/Space in the new portable build and visually confirmed
the first image followed by its next instruction, then the next image, instruction
and following image on the third approximate page. Screenshots and accessibility
snapshots are retained locally in the evidence directory. This confirms document
flow within the existing preview limits, not full Word pagination/wrapping.

Delivery: `artifacts/QuickLook.Next-1.0.4-docx-inline-portable-win-x64.zip`.
The unpacked copy is running locally with copied Settings/State; the previous
desktop portable folder and its Data remain available. No startup entry changed.

## Space, GBK tables and portable startup: 2026-11-14

Changes build on the existing uncommitted multi-window work without replacing it.
The HWND Space guard now distinguishes actual keyboard ownership from XAML's
remembered logical focus. XAML handles Space in PreviewKeyDown before scrolling
containers; editors, buttons and value controls retain their own Space behavior.
Both routes preserve foreground checks and reject repeats of the opening key.

The bounded Rust CSV/TSV reader now tries strict GBK with Chinese/high-byte-pair
evidence after Unicode, retaining the Windows-1252 fallback. Synthetic regressions
cover GBK CSV/TSV, UTF-8/BOM/UTF-16, Western accents including byte sequences that
are also valid GBK, malformed complete input and truncated final lead bytes.
No private CSV fixture or new dependency was added.

[ADR-0007](adr/0007-opt-in-portable-startup.md) allows explicit per-folder portable
startup. A hashed, separate HKCU Run value invokes Start.ps1 with the bundled
runtime environment; portable startup never automatically repairs an entry or
changes the installed copy. Commands above Windows' 260-character Run limit fail
instead of reporting successful registration. Package smoke checks availability
without enabling startup, and snapshots installed and portable startup entries.

Final verification: native MSBuild and managed Release builds, zero warnings or
errors; Rust 394 passed/one existing ignored; .NET 600 passed (Core 463,
ParserHost 52, RasterHost 72, ShellBroker 13); Rust fmt, all-feature/all-target
Clippy, .NET formatting, full architecture -SkipDist and diff checks passed.
The format checker reported a workspace-load warning but returned success.
Portable package checks passed both launchers, clean runtime resolution without
inherited developer overrides, three hosts, local storage, redirected-Data
rejection and unchanged profile/startup.

Local desktop verification with Orca reproduced the old CSV mojibake and failed
second-Space close, then confirmed readable Chinese in the reported CSV, three
consecutive open/close cycles, Space close after clicking the table scroller and
Space toggling a pointer-focused pin button without closing the preview. Settings
startup enabled a 243-character command; after exiting the app, that exact command
started the portable copy with developer/runtime overrides removed. Disabling
startup restored the original entry snapshot. This tests the registered command,
not a physical Windows reboot. Long-held physical keys and the entire multi-format
manual matrix remain pending. Orca sometimes returns app_not_found after a close
because no visible window remains; app logs and window enumeration corroborate it.

Evidence: `artifacts/logs/bugfix-20261114/` and `artifacts/bugfix-*.log`.
Delivery: `artifacts/QuickLook.Next-1.0.4-fixes-portable-win-x64.zip`, SHA-256
`E357378F3DF2DF1499AAA67EBDC858D1D83FCB8703826592758BD3A47B3E940C`.
The portable checker retained isolated evidence under
`artifacts/便携 测试 & isolated-389067d5a99044adb16075a808233019/`.
The previous desktop package and its Data remain available for rollback.
The final desktop copy has 416 manifest files verified and preserves the previous
Settings/State. Its CSV open/second-Space close passed again. The actual tray
popup's startup command was read back through Win32 as enabled and unchecked;
the menu was dismissed without changing startup. Evidence includes
`delivery-verification.json`, `delivery-state.json` and `delivery-tray-startup.json`.

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

## Space toggle after reopening: 2026-11-14

The preview HWND kept a Space-down latch until it received key-up. Closing on
key-down hides the window, so key-up can go to Explorer or a XAML child instead.
After reopening, a new physical press could be consumed without closing. The
App now uses each key-down message's previous-key-state bit, consumes repeats,
and retains the existing modifier, modal, visibility and control-focus checks.
Activation-state logging helps distinguish this missed toggle from actual
window deactivation; the reported intermittent focus loss is not yet manually
verified as resolved.

Verification: nine new Space-message regressions passed, including missing
key-up over five sessions and holding the opening Space across activation.
Native MSBuild and full managed Release build passed with zero warnings/errors;
serialized solution tests passed 576 cases (Core 439, ParserHost 52, RasterHost
72, ShellBroker 13). Full architecture `-SkipDist`, focused focus/search guards,
and `git diff --check` passed. Rust sources and dependency graphs are unchanged;
the full Rust format/Clippy/workspace suites were not rerun for this App-only fix.
The prerequisite doctor still reports no system Windows SDK libraries; the
existing process-local SDK configured by `artifacts/tools/Enter-Dev.ps1` was
used successfully without changing toolchain pins or machine configuration.
Desktop acceptance remains pending in the regression checklist.

### Follow-up: Space must respect foreground ownership

The owner confirmed the first candidate closes on the second Space, then reported
that Space in another location also closes a manually defocused preview. The
existing Rust hook intentionally emitted `CLOSE` for Space in Explorer whenever
a preview was visible. That behavior is now removed: global Space opens only a
hidden preview from Explorer content. Preview-local Space requires the current
foreground HWND, not a cached activation event. Late Explorer `OPEN` results
cannot close or reactivate an already active background preview. The prior
missing-key-up fix remains; global held-key tracking also covers presses that
start outside Explorer so holding Space while changing focus cannot reopen it.
Esc and the other shortcut contracts are unchanged.

The follow-up passed Rust format, locked all-target/all-feature Clippy, and
391 Rust tests (one existing ignored); managed Release built with zero
warnings/errors, and 584 serialized solution tests passed (Core 447, ParserHost
52, RasterHost 72, ShellBroker 13). Focus/search guards passed. Eight new managed
foreground-policy cases and a Rust policy test cover background preview,
Explorer, other applications, text input, hidden state and modal state.
Full architecture `-SkipDist` and `git diff --check` passed; the architecture
result is retained in `artifacts/focused-space-architecture.log`.

The independent local test payload is
`artifacts/local-preview-20261114-focused-space/`, started through
`artifacts/Start-Focused-Space-Fix.cmd`. All 185 copied files were verified against
the build outputs; the manifest is
`artifacts/local-preview-20261114-focused-space.manifest.json`. Earlier payloads
and launchers remain available. Actual foreground/background GUI acceptance is
pending; see the added desktop regression row.

## Bounded preview windows and temporary topmost: 2026-11-14

Preview windows now expose independent pin and always-on-top title-bar toggles.
Pinning protects the current file from Explorer selection and focus-loss close;
another file opens an unused window when all active windows are pinned. The pool
is bounded to three slots. Three pinned windows retain their contents and report
capacity through an InfoBar and tray balloon. Closing or unpinning restores
capacity. Topmost applies only to its window and resets, together with pinning,
on close. Schema 5 removes the former persisted global window-level preference.
All three locales have matching resources; no dependencies were added.

The manager owns one native input producer and aggregate visibility. Each window
retains separate sessions, cancellation generations, request maps and authenticated
host pipes. Close/pin changes invalidate queued input and native selection work;
ordinary content updates do not republish unchanged visibility or cancel newer
selection work. Foreground ownership and the accepted missing-key-up Space fix
remain enforced. See [ADR-0006](adr/0006-bounded-preview-windows.md).

Verification passed: native MSBuild, Rust format, locked all-target/all-feature
Clippy, and 391 Rust tests (one existing ignored); managed Release built with zero
warnings/errors. The final serialized .NET run passed 597 tests: Core 460,
ParserHost 52, RasterHost 72 and ShellBroker 13. Full architecture `-SkipDist`,
focused preview-window/performance guards and `git diff --check` passed. Build and
gate logs are retained as `artifacts/multi-window-*.log`.

The independent test payload is
`artifacts/local-preview-20261114-multi-window/`, launched through
`artifacts/Start-Multi-Window-Preview.cmd`. All 185 copied files were verified
against build-output hashes; the manifest is
`artifacts/local-preview-20261114-multi-window.manifest.json`. Earlier payloads
are retained. Exit the running older version through its tray before starting
this candidate. Desktop interaction remains pending in the regression checklist:
the GUI automation connection was unavailable, so automated policy tests do not
establish actual focus, button interaction or z-order behavior.

### Multi-window portable delivery: 2026-11-14

`artifacts/QuickLook.Next-1.0.4-multi-window-portable-win-x64.zip` contains the
tested multi-window changes and the accepted Space fixes. All 185 application
payload files match the tested local preview hashes. The package uses the
existing portable packing contract, .NET 10.0.10 and Visual C++ 14.44.35211.0;
no dependency or version graph changed. Its manifest records the current HEAD
and dirty source state. The previous portable directory is retained.

The ZIP is 91,937,653 bytes (about 87.7 MiB), with 416 extracted files totaling
245,693,550 bytes and an empty delivery Data directory. SHA-256:
`AA95DEAE59FAF001FAE7BCBB708D611697F44E3A5E39F93384E3E0CF17DD4069`.
The unmodified `tools/test-portable-package.ps1` passed archive and extracted
hashes, isolated runtime resolution with no SDK, WinUI resources, actual parser
HANDLE preview, RasterHost authentication, ShellBroker thumbnail, package-local
storage, redirected-Data rejection and unchanged existing profile/startup.
An initial profile comparison overlapped periodic diagnostic log writes by the
running nonportable App; the unchanged checker passed on retry. Both logs are
retained. This is automated validation on the development computer, with GUI
acceptance on other computers still pending.

Evidence is under `artifacts/logs/portable-multi-window/`, including
`delivery-verification.json`. Temporary extracted application/runtime copies
were removed after retaining and hashing their small Data evidence; the delivery
copy remains clean. Users should exit other instances, extract the complete ZIP
and run its `Start.cmd`.

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
RasterHost resource-test measurement and duplex cancellation coverage are
documented in [rasterhost-test-measurement.md](rasterhost-test-measurement.md);
the HANDLE budget remains unchanged. Historical metadata-child shutdown
failures still need separate evidence.
