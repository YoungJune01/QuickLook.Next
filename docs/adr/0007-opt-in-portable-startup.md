# ADR-0007: Allow explicit startup for a portable copy

Status: Accepted (2026-11-14). Supersedes ADR-0005's prohibition on portable startup management.

## Context

The portable build's disabled tray/settings toggle prevents the requested login startup.
Launching its apphost directly also loses the bundled runtime environment.

## Decision

Portable startup remains off until the user enables it in the tray or settings.
Use a separate HKCU Run value named `QuickLookNext.Portable.<SHA256>` derived from
the normalized portable root. Query, enable and disable only that value and only
when its command belongs to the current copy; never inspect or repair installed
startup entries. Startup repair returns immediately in portable mode.

The command invokes the bundled `Start.ps1` as a quoted `-File` argument through
Windows PowerShell, with a hidden window and process-scoped execution policy.
The launcher validates package files and passes the bundled .NET runtime and
temporary paths through the child's environment. Paths are never interpolated
into executable script text. No policy or environment change persists globally.

All application-owned storage remains in `Data/` under ADR-0005. The per-user
startup entry is the explicit, user-directed exception. Disable startup before
moving/deleting the folder; the app does not migrate old entries automatically.
No dependencies, HANDLE/IPC permissions or host resource limits change.

## Verification

Registration tests cover per-location ownership, case/trailing separator
normalization, foreign-command rejection, shell metacharacters and incomplete
packages. The portable package check runs both launchers with isolated runtime
resolution and asserts the profile/startup snapshot stays unchanged. Startup
availability is checked by smoke without enabling it. Desktop checks separately
cover enabling, launching the registered command, disabling and restoring state.
