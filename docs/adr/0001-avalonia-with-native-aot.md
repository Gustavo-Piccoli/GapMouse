# 1. Avalonia UI with Native AOT

## Context

GapMouse needs a modern, self-contained desktop UI (tabs, tray icon, light/dark
theme) that does not feel heavy. Three options were considered:

- **WPF + MVVM**: most common desktop stack, Fluent theme in .NET 9+, but no native
  tray icon and no trimming support, so the self-contained executable is ~65-75 MB.
- **WinForms**: native tray icon and simplest, but dated look and limited dark mode;
  ~45-55 MB self-contained.
- **Avalonia + Native AOT**: single native executable of ~20-30 MB that starts
  instantly, Fluent light/dark theme, native tray icon, testable MVVM. Controls are
  not native and AOT demands discipline.

WinUI 3 (complex packaging, large output) and pure Win32 with AOT (smallest, but
building a tabbed UI is disproportionate work) were also discarded.

## Decision

Use Avalonia UI compiled with Native AOT. The owner considered ~70 MB too heavy, and
this is the only path to a modern, self-contained and small application.

## Consequences

- A single ~20-30 MB native `.exe`.
- Every `src/` project must stay AOT-compatible: no reflection-based APIs, and
  P/Invoke only through `[LibraryImport]`. CI publishes with AOT to enforce it.
- Avalonia is less common in job listings than WPF; accepted as a trade-off.
