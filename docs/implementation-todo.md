# Maintenance Queue

This is the active local maintenance queue. Implementation evidence belongs in
[review-readiness.md](review-readiness.md), runtime contracts in [ADRs](adr/README.md)
and [HANDLE inputs](handle-based-preview-inputs.md). Upstream historical detail
is preserved at local commit `54302d0`; it is not a second active roadmap.

## Current acceptance

- [x] Desktop selection and the active Explorer tab: owner accepted.
- [x] Excel row height and fixed sheet selector: owner accepted.
- [x] Oversized workbook errors with Open File action: owner accepted.
- [x] Blank worksheet cells and merged regions: owner accepted.
- [ ] Complete repeated-input, empty-selection, cancellation, error-to-content,
  sheet-switching, theme, and accessibility combinations in the
  [manual checklist](desktop-regression-checklist.md).

## Stability and measured performance

- [ ] `E26-04`: investigate historical RasterHost HANDLE-budget fluctuations
  and early metadata-child pipe closure if they recur. Unchanged reruns passing
  do not explain first failures; retain budgets and capture exit codes/threads.
- [ ] `E26-01` / `O26-12`: make local backup and repeatable delivery reproducible
  on a second machine; establish a fork/upstream workflow when needed.
- [ ] `E26-02`: collect controlled first-paint p50/p95, build latency, memory,
  HANDLE count, and host-drain/recycle measurements. Keep cold/warm conditions
  explicit and avoid treating a parser probe as App first paint.
- [ ] `E26-03` / `R26-P1-09`: move Explorer COM work off the hook message pump
  with bounded queuing and generation/cancellation checks.
- [ ] `R26-P1-08`: profile target-size image decode and retained animation
  costs; deeper codec cancellation and GPU upload changes need measured benefit.
- [ ] `R26-P1-10`: expand fuzz/property and sanitizer coverage for hostile
  archives, Office, media, and cancellation/ownership boundaries.

## UI, coverage, and architecture

- [ ] `R26-HDR-05`: complete physical SDR/HDR, mixed-monitor, DPI, high-contrast,
  and reduced-motion evidence using the [HDR matrix](hdr-validation-matrix.md).
- [ ] Complete [title-bar](titlebar-visual-check.md) and screen-reader checks for
  loading, success, failure, and PDF page errors. Keep manual evidence separate.
- [ ] `O26-06` / `E26-05`: improve behavioral guard coverage and extract only
  demonstrated ownership/lifecycle components; do not lower limits or remove
  safety checks to reduce file sizes.
- [ ] `O26-08`: centralize duplicated format facts while preserving the
  [format registry](format-registry.json), contract tests, and host boundaries.
- [ ] `O26-09` / `O26-10`: isolate smoke entrypoints and profile CI caching or
  parallelism without changing test deadlines or the production launch path.
- [ ] Keep ShellBroker responsible for Shell thumbnail compatibility; additional
  fields must use bounded HANDLE-based readers in their designated hosts.
- [ ] Evaluate native PDF close APIs, bounded animation packet revisions,
  AppContainer/network isolation, cache controls, and shortcut diagnostics only
  against a concrete need. ARM64 and any extension SDK remain separate projects.

## Scope and history

Office remains approximate preview; macros, formula recalculation, and complete
Excel fidelity are outside this maintenance scope. Existing x64 format support,
resource limits, three locales, and optional sideload installation are retained.
Store submission/certification, the marketing website, reference plugins, and
historical completed-task narratives have been retired from the working tree.
Their removal does not mark unresolved runtime tasks as complete.
