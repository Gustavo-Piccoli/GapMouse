# 6. Windows shell integration

## Context

The app is tray-first: the tray icon, the pause hotkey and "bring the window to the front"
must work before any window exists, survive an Explorer restart and shut down cleanly when
Windows logs off. Avalonia's tray icon and toast support do not cover all of this.

## Decision

- **Hidden top-level shell window, not a message-only window.** `ShellWindow` is a
  `WS_POPUP` tool window (class `GapMouse.ShellWindow`) that is never shown. Message-only
  windows receive no `TaskbarCreated` broadcast, so the icon would vanish after an Explorer
  restart, and they cannot own a foreground popup menu, so the tray menu would not close on
  an outside click. This deviates from the spec's wording on purpose.
- **Balloons, not toasts.** Notifications use `Shell_NotifyIcon` balloons (`NIF_INFO`,
  `NIIF_NOSOUND`). WinRT toasts need an AppUserModelID and a Start Menu shortcut, which
  portable mode does not have.
- **`RegisterHotKey`.** One system-wide hotkey (default `Ctrl+Alt+P`) is bound to the shell
  window. A failed registration is logged as a warning and shown in Settings.
- **Mutex plus activate message.** `Local\GapMouse.SingleInstance` guards the instance. A
  second launch finds the shell window, posts a registered activate message and exits with
  code 0. If the first instance has no shell window yet, it retries for about 2 s and still
  exits with 0.
- **One thread.** The shell window is created on Avalonia's UI thread and driven by its
  message loop; no separate message-pump thread. Tray, hotkey and settings handlers run on
  the UI thread, and engine or update events reach view models only through `IUiDispatcher`.
- **One shutdown path.** Tray Exit, "Exit application" and `WM_ENDSESSION` all call
  `AppShutdown.Exit()`, which runs once, in this order: view models, tray, hotkey, engine
  host, state persister, log writer, update service, `HttpClient`, shell window, then
  `desktop.Shutdown()`. The mutex is released when `Main` returns.

## Consequences

- The tray works with no window and recovers from Explorer restarts.
- Closing the window only hides it; quitting needs the tray menu or Home.
- A second `dotnet run` exits at once while an instance is running.
- A failing shutdown step is logged and the remaining steps still run.
