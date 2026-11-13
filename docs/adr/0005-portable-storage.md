# ADR-0005: Keep portable deployment state inside the package

Status: Accepted (2026-11-13)

## Context

A local fork must run on another Windows x64 computer without installing its
runtime or spreading application-owned data across the user profile. Existing
installed/development deployments must keep their storage behavior.

## Decision

An explicit `portable.mode` marker identifies a portable root. Only the documented
App/ParserHost/RasterHost deployment layout inherits that marker. Portable settings,
welcome state, logs, PDF cache, temporary files, and host work use `Data/` below
that root. The launcher scopes .NET resolution and temporary environment variables
to its child process. The package contains one shared .NET runtime, self-contained
Windows App SDK files, and app-local Visual C++ runtime libraries; no SDK is shipped.

Portable startup validates writable data directories and rejects reparse points
within them. Failure does not fall back to profile or system temporary directories.
Automatic startup management is disabled, including querying or repairing another
installation's startup entries. Windows-managed records and explicit user-directed
save/open operations remain outside application storage ownership.

ParserHost and ShellBroker retain separate random work roots and existing limited
write grants. Each redirects TEMP/TMP to its own work root after validation. No
write grants are added to the runtime, application, or complete Data tree. HANDLE
authority, authenticated pipes, process restrictions, jobs, and parsing budgets
remain unchanged.

## Consequences

Copying the complete, closed application folder preserves preferences. Portable
distribution is larger because it includes runtime dependencies, but needs no
system runtime installation. A writable local folder is required. Packages are
created in new directories from a tested, hash-verified release payload; they
include licenses, a file manifest, and an archive checksum. Verification includes
isolated runtime resolution, real ParserHost HANDLE input, and RasterHost IPC.
