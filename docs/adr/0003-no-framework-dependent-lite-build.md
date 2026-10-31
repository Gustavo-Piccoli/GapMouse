# 3. No framework-dependent "lite" build

## Context

With WPF or WinForms, a framework-dependent "lite" build (~1-2 MB, requiring the .NET
runtime to be installed) is a worthwhile companion to a heavy self-contained build.
With Avalonia and Native AOT (see ADR 0001), the complete self-contained executable is
already only ~20-30 MB.

## Decision

Do not ship a framework-dependent "lite" variant. The distribution variants are the
per-user installer and the portable single-file `.exe` (plus winget).

## Consequences

- One build configuration to test, publish and support.
- Users never need to install the .NET runtime.
- Downloads are ~20-30 MB instead of ~1-2 MB; considered acceptable.
