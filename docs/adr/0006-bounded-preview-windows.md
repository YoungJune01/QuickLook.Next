# ADR-0006: Bound independent preview windows to three slots

- Status: Accepted
- Date: 2026-11-14

## Context

One reusable preview cannot compare two files: Explorer selection replaces its content. Users
need to keep a file open and optionally keep a particular window above other applications.
Duplicating the global hook, callback or native visibility owner would introduce input races.

## Decision

PreviewWindowManager owns a UI-thread pool of at most three reusable MainWindow slots and one
NativeBridge input callback. The first slot owns tray/settings/startup services. Each slot keeps
its existing independent PreviewSession, request map, cancellation generations, authenticated
host pipes, supervisors and resource ownership. The pool does not share preview HANDLEs or hosts.
The existing per-host limits remain; the process can have up to three of each host type.

Pinning excludes a slot from Explorer selection and Explorer Escape/zoom routing, and suppresses
focus-loss auto-close. Local navigation and explicit local close remain available. Explorer Space
is always an OPEN request, never a CLOSE: a different file reuses an unpinned slot without taking
focus; when all active slots are pinned, it opens an unused slot. Reopening the same pinned path
activates that slot. Three pinned slots reject a fourth file with an informational message and
tray balloon. No existing preview is evicted. Unpinning or closing restores capacity.

Native visibility and App process power mode are aggregates of visible and pending slots.
Closing or pinning a slot invalidates both queued UI input and native selection reads even when
aggregate visibility remains true. Ordinary content updates do not republish unchanged visibility,
so they cannot cancel newer Explorer selection work for another slot.
Per-host power modes remain local. Shutdown stops the sole input producer and invalidates/stops
all sessions and hosts. Window-local pin and topmost flags start false and reset on every close;
they are never serialized. Schema 5 discards the former global window-level preference.

## Verification

- PreviewWindowRoutingPolicyTests and PreviewWindowPolicyTests exercise bounded routing,
  background Space, unpinning, temporary option isolation and session invalidation.
- Existing stale callback, host crash/timeout and HANDLE ownership suites continue to apply.
- tools/test-preview-window-focus.ps1 guards aggregate ownership and per-XamlRoot input checks.
- docs/desktop-regression-checklist.md tracks actual desktop acceptance separately.
