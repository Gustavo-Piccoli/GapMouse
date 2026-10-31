# 5. Settings, state and logs on disk

## Context

The app must remember settings and daily stats between runs and keep an activity log,
without admin rights and without surprising the user. It ships both installed and as a
portable folder, and a corrupt or hand-edited file must never stop it from starting.

## Decision

- **Portable detection.** Portable mode is on when `<exe dir>\portable` or
  `<exe dir>\GapMouse.settings.json` exists. Config and state then live in the exe
  directory and logs in `<exe dir>\logs`. Otherwise config and state go to
  `%AppData%\GapMouse` and logs to `%LocalAppData%\GapMouse\logs`.
- **JSON via source generation.** Files are UTF-8 without BOM, read and written only
  through `System.Text.Json` source-generated contexts, so the code stays Native AOT safe.
- **Atomic writes.** Each save writes `<target>.tmp` and then replaces the target, so a
  crash never leaves a half-written file.
- **Corrupt-file policy.** An unparsable settings file is renamed to
  `GapMouse.settings.json.bad` and defaults are used; invalid individual fields fall back
  to their defaults. Every case produces a warning instead of an exception. A broken
  state file is treated as no state.
- **Log retention.** One file per day, `gapmouse-yyyy-MM-dd.log`. Old files are removed
  by the date in the file name (not the modified time), only for names matching
  `gapmouse-*.log`, so unrelated files in the folder are never touched.
- **`AppSettings` lives in Application.** It is a plain record plus ports
  (`ISettingsStore`, `IAppStateStore`, `IActivityLog`); Infrastructure holds all file
  access. An architecture test keeps `System.Text.Json`, `System.Net.Http` and
  `Microsoft.Win32` out of Application.

## Consequences

- Portable copies are self-contained and leave nothing in the user profile.
- A bad file costs the user their settings but never blocks startup, and the `.bad`
  backup allows recovery by hand.
- Retention cannot delete foreign files, at the price of ignoring renamed logs.
- Plan 3 can bind the UI to `AppSettings` without touching Infrastructure.
