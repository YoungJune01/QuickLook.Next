# Keyboard Focus Ownership And Key Consumers

The preview uses two focus modes: the initial Explorer-open session transfers keyboard focus to
QuickLook, while later Explorer-originated switches stay non-activating so shell selection
follow-up keeps working. This note records which component consumes each key for each focus owner,
so new keys can be added against the same contract instead of reverse-engineering the two paths.

## Ownership model

1. The initial `ExplorerOpen` session created by Space activates the preview and may move focus to
   its content. The loading shell uses the normal activation path so focus does not depend on the
   preview type or on the final renderer.
2. `ExplorerSwitch` sessions reveal with `SW_SHOWNOACTIVATE` / `SWP_NOACTIVATE`, preserving shell
   focus for selection follow-up. `WS_EX_NOACTIVATE` is removed when the window is shown so a later
   click can still focus it.
3. Clicking the preview window transfers keyboard focus to the preview. Explorer-driven switching
   stops afterwards because the native hook only acts while Explorer is the foreground window.
4. `PreviewSession.ShouldActivateWindow` / `ShouldFocusContent` allow initial `ExplorerOpen` and
   `WindowNavigation` focus; `ExplorerSwitch` never calls `Activate()` or moves element focus.
5. The standard window-level mode raises with `HWND_NOTOPMOST`, keeping the preview in the ordinary
   application band without a transient system-topmost state. The explicit always-on-top mode uses
   `HWND_TOPMOST` until that preview closes or its own title-bar toggle is disabled. The setting is
   temporary, independent for each window, and never written to AppSettings.
6. Optional focus-loss close is evaluated on a low-priority dispatcher callback so transient
   activation changes can settle. It is suppressed while QuickLook Next settings or modal UI owns
   the interaction, or while the preview is pinned, and never changes the initial `ExplorerOpen`
   focus-transfer contract.

## Consumer matrix

| Input | Explorer owns keyboard focus | Preview window owns keyboard focus |
| --- | --- | --- |
| Space | Hook requests `OPEN` from Explorer content. The manager creates/reuses a slot when all active previews are pinned; an ordinary background preview never closes or regains focus | `ClosePreviewFromKeyboard` only while the preview HWND is foreground and the focused element in that window's XamlRoot does not use Space |
| Arrow keys | Delayed selection switch: generation-checked 80 ms timer re-reads the Explorer selection (`WM_QL_SWITCH_DELAYED`) | PDF Left/Up and Right/Down move by whole pages; image Left/Right moves between neighboring files; other previews retain their local list/scroll behavior |
| Page Up / Page Down | — | PDF previous/next page |
| Mouse click on an Explorer item | Delayed selection switch (`mouse_proc`) | Ownership transfer to the preview window |
| Esc | Close the most recently active unpinned preview (`WM_QL_CLOSE`) unless an Explorer text input has focus | WinUI controls/search bars dismiss themselves first; the root event or HWND-only focus fallback calls `TryHandlePreviewWindowEscape` for this window, including pinned previews |
| Ctrl+F | — (the hook ignores chords) | Text search (`OpenTextSearch`) or listing filter (`FocusFilter`) |
| F3 / Shift+F3 | — | Search next/previous |
| + / - (OEM or numpad) | Zoom in/out (`WM_QL_ZOOM_IN` / `WM_QL_ZOOM_OUT`) | — |
| F5 | Reload preview (bare key; works for either owner) | Same |
| F11 | Fullscreen toggle (bare key; works for either owner) | Same |

## Guardrails

- Hook-driven keys require no modifiers, suppress text inputs
  (`foreground_text_input_active` class check for Edit/RichEdit on the foreground window's
  focus), and — except F5/F11 — additionally require Explorer to be the foreground
  window. The hook never emits Esc close outside Explorer: Win32 class names do not identify
  text input focus inside XAML. Preview-window Esc is owned by the App: XAML routes control
  dismissal, while `PreviewKeyboardHook` handles HWND-only focus after image transitions. A
  handled HWND Esc is consumed (including repeats) so it cannot dismiss both search and preview.
- Switch messages carry `PREVIEW_VISIBILITY_GENERATION`. The pump re-validates with
  `accepts_switch_event` before arming the 80 ms timer, and `switch_timer_proc` re-checks
  `SWITCH_GENERATION` and `PREVIEW_VISIBLE` at fire time. Hiding posts `WM_QL_CANCEL_SWITCH`;
  the generation re-check is the second layer that also covers a lost cancel message (for
  example a hide before the hook thread reports ready). `OPEN`/`CLOSE` intents invalidate any
  armed timer.
- Global Space never emits `CLOSE`. Manually moving focus to Explorer, the desktop, settings,
  or another application stops Space closing the preview. The manager ignores `OPEN` for the same
  ordinary background file and routes a different file through non-activating `ExplorerSwitch`.
  When all active previews are pinned, `OPEN` may create another window, up to three; opening an
  already pinned file activates its existing window. At capacity all pinned previews survive and
  receive an informational message, with a tray balloon as well. Both preview-local Space routes check the current foreground HWND rather
  than the last activation notification. Other shortcut policies, including Esc, are unchanged.
- Duplicate closes are deduplicated by `_keyboardCloseQueued`; the legacy duplicate-open
  reveal guard remains for foreground `OPEN` races in `MainWindow`.
- HWND Space handling uses bit 30 (previous key state) from each key-down message to consume
  repeats without closing again. It keeps no Space-down latch: hiding the preview on key-down
  or moving focus to a XAML child can route key-up elsewhere. A later physical press must close
  immediately after reopening even when the previous release never reached the preview HWND.
  Holding the opening Space across activation must not close the new preview on auto-repeat.
- XAML Space is handled in `PreviewKeyDown` before scrolling containers consume it. Actual
  editors, buttons and value controls retain Space; ScrollViewer and ordinary preview lists
  allow closing. When native `GetFocus()` points at the preview HWND itself, XAML's remembered
  logical element does not own keyboard input and cannot veto the HWND close path. The XAML
  path also ignores `KeyStatus.WasKeyDown` repeats across initial activation.
- PDF page keys are also observed from the nested `ScrollViewer` after its class handling so
  page navigation remains available when the document surface owns element focus. Image repeats
  resolve from `PreviewSession.ActivePath`, allowing each new key repeat to supersede an in-flight
  sibling preview instead of reopening the same file.

## Code pointers

- Hook thread, key classification, and switch timer: `native/quicklook_next_native/src/lib.rs`
  (`keyboard_proc`, `mouse_proc`, `hook_thread`, `switch_timer_proc`, `accepts_switch_event`).
- Preview-window key handling: `src/QuickLook.Next.App/MainWindow.xaml.cs` (`OnRootGridKeyDown`).
- Focus policy: `src/QuickLook.Next.App/PreviewSession.cs`.
- Window activation styles: `src/QuickLook.Next.App/PreviewWindowController.cs`.
- Bounded routing, shared hook visibility, and process power mode:
  `src/QuickLook.Next.App/PreviewWindowManager.cs` and `PreviewWindowRoutingPolicy.cs`.
- Pin/topmost options: `PreviewWindowOptions` in `PreviewWindowPolicy.cs` (window session only).

## Multiple preview windows

The App keeps at most three reusable HWND slots on one UI thread, with one native hook/callback,
tray, and settings owner. Each slot owns an independent PreviewSession, generation, cancellation
tokens, presenters, request map and supervised hosts. Host pipe names and launch tokens remain
unique. Closing one slot invalidates its session before hiding it and never cancels another slot.
Pending reveals count as active; native `PREVIEW_VISIBLE` is the aggregate of all slots.
The App's process efficiency state uses that aggregate; host efficiency remains per slot.
Closing a slot or changing its pin state invalidates the manager's queued-input epoch and the native
selection-read generation, even if another slot stays visible. Ordinary content updates do not
republish an unchanged aggregate: they must not cancel newer selection work for another slot.

Pinning cancels queued selection debounce work and excludes the window from Explorer selection,
zoom and Escape routing. Local navigation, local Escape/Space and explicit close still work.
If multiple windows are unpinned, the most recently activated one follows Explorer. F5/F11 target
the foreground preview first, including a pinned one, otherwise the latest unpinned preview.
Only the drag region is registered with SetTitleBar; the stable XAML toggle buttons sit outside it.
Closing resets both pin and topmost before reuse. Settings schema 5 ignores the old global
PreviewWindowLevel field while preserving other supported preferences.
