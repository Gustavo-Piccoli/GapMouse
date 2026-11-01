# GapMouse — Plan 3: Desktop App (Avalonia) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `GapMouse.exe`, the real desktop app. It covers:
- a tray icon whose color follows the engine state, with a context menu;
- a single instance;
- a global pause hotkey;
- a tabbed window (Home, Logs, Settings, About) in dark and light themes, with Portuguese and English switchable live;
- balloon notifications;
- the Plan 2 services (settings, logs, stats, start with Windows, update check) wired in.

**Architecture:**
- `GapMouse.App` (Avalonia 12, MVVM via CommunityToolkit.Mvvm, composition via Microsoft.Extensions.DependencyInjection) holds views, view models and composition.
- View models depend only on ports and Application types, never on Avalonia. An architecture test enforces this, so they are unit-tested without a UI.
- Windows shell integration lives in `GapMouse.Infrastructure/Shell`:
  - a hidden window;
  - `Shell_NotifyIcon` for the tray icon and balloons;
  - `RegisterHotKey` for the global hotkey;
  - a named mutex for the single instance.
- The shell window is created on Avalonia's UI thread, so Avalonia's own Win32 message loop dispatches its messages. There is no extra thread.

**Tech Stack:**

| Package | Version |
|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent | 12.1.4 |
| CommunityToolkit.Mvvm | 8.4.2 |
| Microsoft.Extensions.DependencyInjection | 10.0.14 |

Also used: Win32 `LibraryImport` and the existing Plan 1–2 stack (xUnit v3, Shouldly, FakeTimeProvider, NetArchTest).

**Spec:** `docs/superpowers/specs/2026-10-31-gapmouse-design.md` (Portuguese). Read §5.3–§5.7, §7 and §11 items 1, 2, 7 and 8 before any task.

**Roadmap after this plan:** Plan 4 is distribution. It covers release-please, the CD pipeline, x64 and ARM64 builds, the Inno Setup installer, the portable zip, SHA256SUMS and winget.

## Global Constraints

- **Plans 1–2 still apply:**
  - English code and commits; Conventional Commits with no AI attribution trailer;
  - warnings as errors and central package versions;
  - `[LibraryImport]` only, and everything AOT-safe;
  - `Method_Condition_ExpectedResult` test names;
  - MTP filter `--filter-not-trait "Category=Win32"`;
  - every power call on the engine thread;
  - JSON only through source-generated contexts;
  - Application has no I/O.
- **New packages:** only those in Tech Stack, at those exact versions. No `Avalonia.Fonts.Inter` (size), no `Avalonia.Diagnostics` and no ReactiveUI.
- **Avalonia 12:** verify APIs against the 12.x package you restore (XML docs and IntelliSense), not against Avalonia 11 knowledge. Compiled bindings are mandatory: set `AvaloniaUseCompiledBindingsByDefault` to true, and every view declares `x:DataType`. No `ReflectionBinding`.
- **AOT and globalization:**
  - The app publishes with `PublishAot=true` and **0** AOT or trim warnings.
  - It must **not** set `InvariantGlobalization`, because the UI formats dates and numbers per culture.
  - The target size of `GapMouse.exe` is ~20–30 MB (spec §2). Report the actual size.
- **View models:**
  - Namespaces `GapMouse.App.ViewModels*` and `GapMouse.App.Localization` must not reference `Avalonia`.
  - Work from the engine thread or timers reaches view-model state only through `IUiDispatcher.Post`.
- **Localization (spec §3, §11.8):**
  - Every UI string exists in English and Portuguese, written as `T("English", "Português")` in a `Localizer` partial file per area.
  - Language changes live, with no restart.
  - `Auto` means Portuguese when `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"`, otherwise English. Formatting culture is `pt-BR` or `en-US`.
  - Log tags (START…ERROR) stay English in both languages. On-disk log messages stay English.
- **Pause options (spec §5.3):** 10 min, 30 min, 1 h, Custom (whole minutes 1–1440) and Never (indefinite).
- **Tray (spec §5.5):**
  - The icon color follows the state: Waiting `#3B82F6`, Moving `#22C55E`, Paused `#F59E0B`.
  - The tooltip shows the state.
  - The right-click menu shows `Pause ▸ (10 min, 30 min, 1 h, Custom…, Never)`, or `Resume` when paused, then a separator, `Open` and `Exit`.
  - Double-click opens the window.
  - The window ✕ hides to the tray. Only Exit (tray) or "Exit application" (Home) quits.
  - The first ✕ shows the one-time balloon "GapMouse is still running in the tray" ("O GapMouse continua rodando na bandeja"), remembered via `AppSettings.TrayHintShown`.
- **Notifications (spec §5.6, §11.7):**
  - They are tray balloons via `Shell_NotifyIcon` (`NIF_INFO`), never WinRT toasts.
  - State-change balloons only when `NotificationsEnabled` is on, for `MovingStarted`, `UserReturned`, `EnginePaused` and `EngineResumed`, with `NIIF_NOSOUND`.
  - The "update available" balloon always shows, once per version per run. Clicking it opens the release page.
- **Hotkey (spec §5.7.2):**
  - The default is `Ctrl+Alt+P`. It toggles between Pause (Never) and Resume.
  - If registration fails, log a WARN and show it in Settings.
- **Single instance (spec §5.7.1):**
  - The mutex is `Local\GapMouse.SingleInstance`.
  - A second launch brings the existing window to the front and exits with code 0.
- **Start minimized:**
  - The `--minimized` argument or `AppSettings.StartMinimized` starts with no window, only the tray.
  - The tray, hotkey and single-instance activation must work before any window exists (spec §11.1).
- **Shell window:** the spec says "message-only window".
  - **Ruling:** use a hidden top-level tool window (class `GapMouse.ShellWindow`, `WS_POPUP`, `WS_EX_TOOLWINDOW`, never shown) instead.
  - Message-only windows get no `TaskbarCreated` broadcast, so the icon would vanish after an Explorer restart. They also cannot own a foreground popup menu, so the tray menu would not close on an outside click.
  - The deviation is recorded in ADR 0006 (Task 10).
- **Shutdown order:** tray, then hotkey, then host, then `AppStatePersister`, then `ActivityLogWriter`, then `UpdateService`, then `HttpClient`, then shell window, then mutex.
  - It runs once (idempotent).
  - It is the same path for Exit, "Exit application" and Windows logoff or shutdown (`WM_ENDSESSION`).
- **Settings:**
  - Saving applies to the running app at once. Engine changes take effect on the next cycle.
  - Automatic saves (`TrayHintShown`) are skipped when `SettingsLoadResult.ReadFailed` is true. An explicit Save by the user is always allowed.
- **Constants:**
  - GitHub repository URL `https://github.com/Gustavo-Piccoli/GapMouse`.
  - License `MIT`.
  - App name `GapMouse`.
  - Version from `AssemblyInformationalVersion`. Task 1 sets `<Version>0.1.0</Version>` in `Directory.Build.props`.
- **Tests:** UI-free. View models and coordinators use fakes and `FakeTimeProvider`. Real Win32 shell tests carry `[Trait("Category","Win32")]`.

## Review Focus

1. **Windows logoff or shutdown while GapMouse runs.**
   - Expected: today's stats are saved, power is released and the app exits without blocking logoff. `WM_ENDSESSION` runs the same idempotent shutdown as Exit.
   - Owners: Task 3 (`SessionEnding` raised from `WM_ENDSESSION`) and Task 10 (`AppShutdown` idempotent and wired).
2. **Explorer restarts (crash or update).**
   - Expected: the tray icon comes back with the current state, because `TaskbarCreated` re-adds it.
   - Owner: Task 3. Test it by sending the registered `TaskbarCreated` message to the window.
3. **The hotkey is taken by another app, or the saved hotkey text is invalid.**
   - Expected: the app runs normally, logs one WARN, and Settings shows "shortcut unavailable". Saving a free hotkey fixes it without a restart.
   - Owners: Task 3 (duplicate registration returns false), Task 4 and Task 7.
4. **A second launch while the first runs hidden in the tray (or started `--minimized`, with no window yet).**
   - Expected: the existing instance creates or shows its window and brings it to the front, and the second process exits 0.
   - Owners: Task 3 (a duplicate mutex returns null; the activate message raises `ActivationRequested`) and Task 10.
5. **Bad values typed in Settings or in the Custom pause box.** Examples: min greater than max, empty fields, out-of-range numbers, 0 or 1441 minutes, letters.
   - Expected: inline errors, Save disabled or no pause applied, and the engine never receives invalid settings.
   - Owners: Task 5 (custom prompt validation) and Task 7.

---

### Task 1: App skeleton, icons, localization base, UI service ports, CI publish

**Files:**
- Create: `src/GapMouse.App/GapMouse.App.csproj`, `app.manifest`, `Program.cs`, `App.axaml`, `App.axaml.cs`
- Create: `src/GapMouse.App/Views/MainWindow.axaml(.cs)`, `HomeView`, `LogsView`, `SettingsView`, `AboutView` (`.axaml` + `.axaml.cs`, placeholder content)
- Create: `src/GapMouse.App/ViewModels/ViewModelBase.cs`, `MainWindowViewModel.cs`, `HomeViewModel.cs`, `LogsViewModel.cs`, `SettingsViewModel.cs`, `AboutViewModel.cs` (placeholders)
- Create: `src/GapMouse.App/Localization/Localizer.cs`, `Localizer.Common.cs`
- Create: `src/GapMouse.App/Services/IUiDispatcher.cs`, `AvaloniaUiDispatcher.cs`, `IShellLauncher.cs`, `ShellLauncher.cs`, `IClipboardService.cs`, `AvaloniaClipboardService.cs`, `IAppLifetime.cs`, `IMainWindowService.cs`, `IPausePrompt.cs`, `PauseOption.cs`
- Create: `tools/icons/New-GapMouseIcons.ps1`. Its outputs are `src/GapMouse.App/Assets/Icons/gapmouse.ico`, `tray-waiting.ico`, `tray-moving.ico` and `tray-paused.ico`, all committed.
- Create: `tests/GapMouse.App.Tests/GapMouse.App.Tests.csproj`, `Localization/LocalizerTests.cs`, `Services/PauseOptionTests.cs`, `Fakes/ImmediateUiDispatcher.cs`
- Move: `tests/GapMouse.Application.Tests/Fakes/FakeEngineController.cs` to `tests/GapMouse.Testing/Engine/FakeEngineController.cs`. It becomes `public`, in namespace `GapMouse.Testing.Engine`, and `GapMouse.Testing` gains a reference to Application. Update its usages.
- Move: `tests/GapMouse.Application.Tests/Fakes/ListLogger.cs` to `tests/GapMouse.Testing/Logging/ListLogger.cs` (public, namespace `GapMouse.Testing.Logging`; GapMouse.Testing gains a `Microsoft.Extensions.Logging.Abstractions` reference). Update its usages.
- Modify: `Directory.Packages.props`, `Directory.Build.props` (add `<Version>0.1.0</Version>`), `GapMouse.slnx`, `tests/GapMouse.Architecture.Tests` (reference the App and add the rules), `.github/workflows/ci.yml`

**Interfaces (Produces, all `public`):**
- `sealed partial class Localizer : ObservableObject`, with:
  - `UiLanguage RequestedLanguage`;
  - `bool IsPortuguese`;
  - `CultureInfo Culture`;
  - `void SetLanguage(UiLanguage requested)`, which raises `PropertyChanged` with an empty name only when the effective language changes;
  - `protected string T(string en, string pt)`.
  - The constructor takes `Func<CultureInfo> uiCulture` (default `() => CultureInfo.CurrentUICulture`) for testability.
  - `Localizer.Common.cs` defines `AppName`, `TabHome` (Home/Início), `TabLogs` (Logs/Logs), `TabSettings` (Settings/Configurações), `TabAbout` (About/Sobre), `Ok`, `Cancel` and `None` ("—"/"—").
- `abstract class ViewModelBase(Localizer l) : ObservableObject` exposes `Localizer L`.
- Placeholder view models, each `sealed`, with a constructor taking only `Localizer`: `HomeViewModel`, `LogsViewModel`, `SettingsViewModel`, `AboutViewModel`. Later tasks replace these constructors.
- `MainWindowViewModel(Localizer l, HomeViewModel home, LogsViewModel logs, SettingsViewModel settings, AboutViewModel about)`.
- Service interfaces:
  - `IUiDispatcher { void Post(Action action); }`
  - `IShellLauncher { void OpenFolder(string path); void OpenUrl(Uri url); }`
  - `IClipboardService { Task SetTextAsync(string text); }`
  - `IAppLifetime { void Exit(); }`
  - `IMainWindowService { void Show(); }`
  - `IPausePrompt { Task<int?> AskMinutesAsync(); }`, which returns whole minutes from 1 to 1440, or null when cancelled.
- `enum PauseOption { TenMinutes, ThirtyMinutes, OneHour, Custom, Never }`, plus `static class PauseOptions { static PauseDuration ToDuration(PauseOption option, int? customMinutes); }`. `Custom` needs minutes, otherwise it throws `ArgumentException`.
- Test fake `ImmediateUiDispatcher : IUiDispatcher` (runs the action inline).

- [ ] **Step 1: Scaffold.**
  - **Packages:** add them to `Directory.Packages.props`.
  - **Project file:** `GapMouse.App.csproj` has:
    - `OutputType` `WinExe`, `net10.0-windows`, `PublishAot` true;
    - `ApplicationManifest` (copy `tools/GapMouse.DevConsole/app.manifest`, then add `supportedOS` for Windows 10/11);
    - `ApplicationIcon` `Assets\Icons\gapmouse.ico`;
    - `AvaloniaUseCompiledBindingsByDefault` true;
    - `AvaloniaResource` for `Assets/**`.
  - **Startup and theme:**
    - `Program.Main` uses `StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown)`.
    - `App.axaml` uses `FluentTheme` with `RequestedThemeVariant="Dark"`.
    - Until Task 10, `OnFrameworkInitializationCompleted` shows the `MainWindow` with placeholder view models, and closing it shuts down.
  - **Main window:**
    - `MainWindow` has the title `GapMouse`, 760×600, min 640×520, the window icon `gapmouse.ico`, and a `TabControl` with the four views. Tab headers bind to `L.Tab*`.
    - `HomeView`, `LogsView`, `SettingsView` and `AboutView` are placeholder `UserControl`s with `x:DataType` set.
  - **Solution and CI:**
    - Add the App and App.Tests to the solution.
    - Add a CI `aot-publish` step: `dotnet publish src/GapMouse.App -c Release -r win-x64 -o artifacts/app`, uploaded as artifact `gapmouse-win-x64` (retention 7 days).
- [ ] **Step 2: Icons.**
  - **Generator:** `New-GapMouseIcons.ps1` works in both Windows PowerShell 5.1 and pwsh on Windows (System.Drawing). It writes multi-size `.ico` files with PNG entries at 16, 20, 24, 32, 40, 48, 64 and 256 px.
  - **Glyph:** a filled circle in the state color with a white stylized mouse (rounded rectangle body, short vertical wheel line), anti-aliased.
  - **Colors:** `gapmouse.ico` uses `#3B82F6`, and the tray files use the Global Constraints colors.
  - **Verify:** run the script, check that each file starts with bytes `00 00 01 00` and has 8 images, and commit the outputs.
- [ ] **Step 3: Write the failing tests.**
  - `LocalizerTests`:
    - `SetLanguage_Portuguese_ReturnsPortugueseText`: `TabSettings == "Configurações"`.
    - `SetLanguage_English_ReturnsEnglishText`.
    - `SetLanguage_AutoWithPtBrUiCulture_IsPortuguese`.
    - `SetLanguage_AutoWithFrUiCulture_IsEnglish`.
    - `SetLanguage_Changed_RaisesPropertyChangedWithEmptyName`.
    - `SetLanguage_Same_DoesNotRaise`.
    - `Culture_Portuguese_IsPtBr`.
  - `PauseOptionTests`:
    - each fixed option maps to the matching `PauseDuration`, and `Never` maps to `Indefinite`;
    - `Custom` with 45 gives 45 min;
    - `Custom` with null throws.
  - `LayeringTests`:
    - `AppViewModels_DoNotDependOnAvalonia`: types in namespaces starting with `GapMouse.App.ViewModels` or `GapMouse.App.Localization` should not depend on `Avalonia`.
    - `Infrastructure_DoesNotDependOnApp`.
- [ ] **Step 4: Run `dotnet test --project tests/GapMouse.App.Tests`.** Expected: the build fails.
- [ ] **Step 5: Implement the Interfaces block.**
  - `AvaloniaUiDispatcher` posts to `Dispatcher.UIThread`.
  - `ShellLauncher` uses `Process.Start` with `UseShellExecute = true`: a folder opens in `explorer.exe` (created first if missing), and a URL opens only if it is https.
  - `AvaloniaClipboardService(Func<TopLevel?> topLevel)` uses the TopLevel clipboard and is a no-op without a TopLevel.
- [ ] **Step 6: Verify.**
  - `dotnet build -c Release` and `dotnet test --filter-not-trait "Category=Win32"` pass.
  - `dotnet format --verify-no-changes` is clean.
  - `dotnet publish src/GapMouse.App -c Release -r win-x64 -o artifacts/app` has 0 warnings; record the exe size.
  - Launching `artifacts/app/GapMouse.exe` shows the dark window with 4 tabs. Close it.
- [ ] **Step 7: Commit:** `feat(app): add Avalonia app skeleton with localization base, icons and AOT publish`.

---

### Task 2: Application additions — in-memory and composite logs, shell ports, hotkey gesture, settings and update contracts

**Files:**
- Modify: `src/GapMouse.Application/Logging/LogEntry.cs`, `LogEntryFormatter.cs`, `src/GapMouse.Application/Settings/SettingsLoadResult.cs`, `src/GapMouse.Application/Updates/UpdateService.cs`
- Modify: `src/GapMouse.Infrastructure/Logging/FileActivityLog.cs` (implements `ILogFileOptions`) and `src/GapMouse.Infrastructure/Settings/JsonSettingsStore.cs` (sets `ReadFailed`)
- Create: `src/GapMouse.Application/Logging/InMemoryActivityLog.cs`, `CompositeActivityLog.cs`, `ILogFileOptions.cs`
- Create: `src/GapMouse.Application/Shell/HotkeyGesture.cs`, `HotkeyModifiers.cs`, `IGlobalHotkey.cs`, `ITrayIcon.cs`, `TrayIconState.cs`, `TrayMenuItem.cs`
- Create: `src/GapMouse.Application/Settings/ISettingsService.cs`, `SettingsSaveResult.cs`, `src/GapMouse.Application/Updates/IUpdateChecker.cs`
- Test: `tests/GapMouse.Application.Tests/Logging/InMemoryActivityLogTests.cs`, `CompositeActivityLogTests.cs`, `tests/GapMouse.Application.Tests/Shell/HotkeyGestureTests.cs`. Extend `LogEntryFormatterTests` and `JsonSettingsStoreTests`.

**Interfaces (Produces):**
- `LogEntry(DateTimeOffset At, LogKind Kind, string Message, EngineEvent? Source = null)`. `LogEntryFormatter.FromEvent` sets `Source`. `Tag` and `ToLine()` are unchanged.
- `sealed class InMemoryActivityLog : IActivityLog`:
  - `const int Capacity = 1000`;
  - `IReadOnlyList<LogEntry> GetEntries()` (a copy, oldest first);
  - `event EventHandler<LogEntry>? EntryAdded`, raised outside the lock;
  - thread-safe.
- `sealed class CompositeActivityLog(IReadOnlyList<IActivityLog> sinks) : IActivityLog`. A sink that throws does not stop the others: use a justified catch-all.
- `interface ILogFileOptions { void Configure(bool enabled, int retentionDays); }`. `FileActivityLog` implements it; the signature is unchanged.
- `SettingsLoadResult` gains `bool ReadFailed { get; init; }`. `JsonSettingsStore` sets it to true only on the IO or UnauthorizedAccess read-failure path.
- Hotkeys:
  - `[Flags] enum HotkeyModifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8 }`. The values equal Win32 `MOD_*`.
  - `sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)`, with:
    - `uint VirtualKey`: A–Z → 0x41+, 0–9 → 0x30+, F1–F24 → 0x70+n−1;
    - `static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyGesture? gesture)`;
    - `override string ToString()`, the canonical form `Ctrl+Alt+Shift+Win+<Key>` in that order.
  - Parsing rules:
    - Tokens are split on `+` and trimmed, case-insensitive.
    - Aliases: `Control` → Ctrl, `Windows` / `Meta` → Win.
    - Exactly one key from A–Z, 0–9 or F1–F24.
    - At least one of Ctrl, Alt or Win.
    - No duplicate tokens.
- `interface IGlobalHotkey : IDisposable { bool TryRegister(HotkeyGesture gesture); void Unregister(); event EventHandler? Pressed; }`. `TryRegister` replaces any previous registration.
- `enum TrayIconState { Waiting, Moving, Paused }`.
- `sealed record TrayMenuItem(string Id, string Text, IReadOnlyList<TrayMenuItem>? Children = null)`, with `static TrayMenuItem Separator` (Id `"-"`).
- `interface ITrayIcon : IDisposable`, with:
  - `void SetState(TrayIconState state, string tooltip)` (the first call adds the icon);
  - `void ShowBalloon(string title, string message, string? tag, bool silent)`;
  - `string? ShowMenu(IReadOnlyList<TrayMenuItem> items)` (blocks until it closes and returns the chosen Id);
  - `event EventHandler? OpenRequested`, `event EventHandler? MenuRequested` and `event EventHandler<string?>? BalloonClicked` (the tag of the clicked balloon).
- `interface IUpdateChecker { Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken); event EventHandler<UpdateCheckResult>? UpdateFound; }`. `UpdateService` implements it.
- Settings service contract:
  - `interface ISettingsService` with `AppSettings Current { get; }`, `bool IsHotkeyRegistered { get; }`, `void Initialize()`, `SettingsSaveResult Save(AppSettings settings)`, `void MarkTrayHintShown()` and `event EventHandler<AppSettings>? Changed`.
  - `sealed record SettingsSaveResult(bool Saved, bool StartupApplied, bool HotkeyRegistered)`.

- [ ] **Step 1: Write the failing tests.**
  - `InMemoryActivityLogTests`:
    - `Append_KeepsOldestFirst`.
    - `Append_OverCapacity_DropsOldest`: 1005 appends keep 1000 entries, and the first one is entry #6.
    - `Append_RaisesEntryAdded`.
    - `Append_FromManyThreads_KeepsAllUpToCapacity` (Parallel.For 500).
  - `CompositeActivityLogTests`:
    - `Append_ForwardsToEverySink`.
    - `Append_SinkThrows_OtherSinksStillReceive`.
  - `HotkeyGestureTests`:
    - **Parses:** `"Ctrl+Alt+P"` gives `Control|Alt`, key `P`, VK 0x50. `"control + shift + f12"` gives canonical `"Ctrl+Shift+F12"`, VK 0x7B. `"Win+1"` gives VK 0x31.
    - **Rejected:** `""`, `"P"`, `"Shift+P"`, `"Ctrl+Alt"`, `"Ctrl+P+Q"`, `"Ctrl+Ctrl+P"`, `"Ctrl+F25"`, `"Ctrl+Esc"`.
    - `ToString_RoundTripsThroughTryParse`.
  - `LogEntryFormatterTests`: `FromEvent_SetsSource`.
  - `JsonSettingsStoreTests`: `Load_FileLocked_SetsReadFailed`, and `Load_Missing_ReadFailedFalse`.
- [ ] **Step 2: Run the Application and Infrastructure test projects.** Expected: the build fails.
- [ ] **Step 3: Implement the Interfaces block.** `UpdateService` keeps its behavior and only adds `: IUpdateChecker`.
- [ ] **Step 4: Run `dotnet test --filter-not-trait "Category=Win32"`.** Expected: all pass, including the architecture tests (the new Application files do no I/O).
- [ ] **Step 5: Commit:** `feat(application): add in-memory activity log, shell ports, hotkey gesture and settings service contract`.

---

### Task 3: Windows shell integration (hidden window, tray, hotkey, single instance)

**Files:**
- Create: `src/GapMouse.Infrastructure/Shell/ShellWindow.cs`, `Win32TrayIcon.cs`, `Win32GlobalHotkey.cs`, `SingleInstance.cs`, `IcoImage.cs`, `ShellNativeMethods.cs`
- Test: `tests/GapMouse.Infrastructure.Tests/Shell/IcoImageTests.cs` (pure), `ShellWindowTests.cs`, `Win32TrayIconTests.cs`, `Win32GlobalHotkeyTests.cs` and `SingleInstanceTests.cs` (all four with `[Trait("Category","Win32")]`)

**Interfaces:**
- Consumes: Task 2's `ITrayIcon`, `TrayIconState`, `TrayMenuItem`, `IGlobalHotkey`, `HotkeyGesture` and `HotkeyModifiers`.
- Produces (`public sealed`):
  - `ShellWindow : IDisposable`:
    - It is created on the calling thread, which must pump messages (Avalonia's UI thread).
    - `const string ClassName = "GapMouse.ShellWindow"` and `nint Handle`.
    - Events `ActivationRequested` and `SessionEnding`. `SessionEnding` is raised synchronously on `WM_ENDSESSION` with wParam TRUE; `WM_QUERYENDSESSION` returns TRUE.
    - `internal` hooks for the tray and hotkey to observe messages.
    - Registered messages: `"GapMouse.Activate"` and `"TaskbarCreated"` (via `RegisterWindowMessageW`).
  - `Win32TrayIcon(ShellWindow window, IReadOnlyDictionary<TrayIconState, byte[]> icoFiles) : ITrayIcon`.
  - `Win32GlobalHotkey(ShellWindow window) : IGlobalHotkey`.
  - `SingleInstance : IDisposable`, with:
    - `static SingleInstance? TryAcquire(string name = @"Local\GapMouse.SingleInstance")`, which returns null when another holder exists (use `createdNew`; an abandoned mutex counts as acquired);
    - `static bool SignalExisting()`, which finds `ClassName` via `FindWindowW`, calls `AllowSetForegroundWindow` for its process id, posts `"GapMouse.Activate"`, and returns whether a window was found.
  - `static class IcoImage { static byte[] SelectImage(byte[] ico, int size); }`. It returns the entry bytes whose width is the smallest ≥ size, or else the largest. It throws `InvalidDataException` on a bad header.

- [ ] **Step 1: Write the failing tests.**
  - `IcoImageTests`: use Task 1's `src/GapMouse.App/Assets/Icons/tray-waiting.ico`, copied into the test output as a linked item.
    - `SelectImage_16_ReturnsSixteenPixelEntry`: the entry is a PNG with width 16 in the IHDR.
    - `SelectImage_100_ReturnsTwoFiftySix`.
    - `SelectImage_BadHeader_Throws`.
  - `ShellWindowTests`:
    - `Create_HasHandleAndIsFindableByClass`: `FindWindowW(ClassName)` returns the handle.
    - `ActivateMessage_RaisesActivationRequested`: `SendMessageW` with the registered activate message.
    - `EndSession_RaisesSessionEnding`.
    - `Dispose_DestroysWindow`.
  - `Win32TrayIconTests`:
    - `SetState_AddsIconThenUpdates_DoesNotThrow`.
    - `TaskbarCreated_ReaddsIcon`: send `TaskbarCreated`, then assert the internal re-add counter is 1.
    - `Dispose_RemovesIcon`.
  - `Win32GlobalHotkeyTests` (use `Ctrl+Alt+Shift+F24`):
    - `TryRegister_FreeGesture_ReturnsTrue`.
    - `TryRegister_SameGestureInSecondWindow_ReturnsFalse`.
    - `HotkeyMessage_RaisesPressed`: send `WM_HOTKEY` with id 1.
  - `SingleInstanceTests` (unique name per test):
    - `TryAcquire_First_ReturnsInstance`.
    - `TryAcquire_WhileHeld_ReturnsNull`.
    - `TryAcquire_AfterDispose_Succeeds`.
- [ ] **Step 2: Run `dotnet test --project tests/GapMouse.Infrastructure.Tests`.** Expected: the build fails.
- [ ] **Step 3: Implement.**
  - **Window procedure:** an `[UnmanagedCallersOnly]` static `WndProc` with a `delegate* unmanaged` pointer in `WNDCLASSEXW`. Route to the instance through a `GCHandle` stored in `GWLP_USERDATA`. Register the class once per process.
  - **Tray icon:**
    - `NOTIFYICONDATAW` with uID 1, callback `WM_APP + 1`, `NOTIFYICON_VERSION_4`, and `NIF_MESSAGE|NIF_ICON|NIF_TIP|NIF_SHOWTIP`.
    - Truncate the tooltip to 127 chars, the balloon title to 63 and the message to 255.
    - Callback mapping: `WM_CONTEXTMENU` → `MenuRequested`; `WM_LBUTTONDBLCLK` → `OpenRequested`; `NIN_BALLOONUSERCLICK` → `BalloonClicked` with the last balloon's tag.
    - Icons: `CreateIconFromResourceEx(IcoImage.SelectImage(bytes, GetSystemMetrics(SM_CXSMICON)), …, fIcon: true, 0x00030000, …)`, created once per state and destroyed on Dispose.
  - **Tray menu:** `ShowMenu` builds the menu with `CreatePopupMenu`/`AppendMenuW` (submenus for `Children`, `MF_SEPARATOR` for `Separator`). It calls `SetForegroundWindow` and `TrackPopupMenuEx(TPM_RETURNCMD|TPM_RIGHTBUTTON|TPM_NONOTIFY)` at `GetCursorPos`, then `PostMessageW(WM_NULL)`, then `DestroyMenu`. It maps command ids back to `Id`.
  - **Hotkey:** `RegisterHotKey(hwnd, 1, (uint)modifiers | MOD_NOREPEAT, vk)`.
  - **Visibility:** expose the test counters through `internal` members.
- [ ] **Step 4: Run `dotnet test --project tests/GapMouse.Infrastructure.Tests`.** This includes the Win32 trait; a tray icon flashes briefly. Then run the full suite with the CI filter. Expected: all pass.
- [ ] **Step 5: Commit:** `feat(infrastructure): add Windows shell window, tray icon, global hotkey and single instance`.

---

### Task 4: SettingsService (apply and save use case)

**Files:**
- Create: `src/GapMouse.Application/Settings/SettingsService.cs`
- Test: `tests/GapMouse.Application.Tests/Settings/SettingsServiceTests.cs`

**Interfaces:**
- Consumes:
  - `ISettingsService` and `SettingsSaveResult`, `ILogFileOptions`, `IGlobalHotkey` and `HotkeyGesture` (Task 2);
  - `StartWithWindowsService`, `ISettingsStore` and `SettingsLoadResult` (Plan 2);
  - `IEngineController`.
- Produces: `public sealed partial class SettingsService(SettingsLoadResult loaded, ISettingsStore store, IEngineController engine, ILogFileOptions logs, StartWithWindowsService startup, IGlobalHotkey hotkey, ILogger<SettingsService> logger) : ISettingsService`.

**Behavior:**
- `Initialize()`:
  1. The effective start-with-Windows value is `startup.InitialPreference(loaded.FileExisted, loaded.Settings.StartWithWindows)`. `Current` is `loaded.Settings` with that value.
  2. Call `startup.Apply(effective)` and `logs.Configure(Current.SaveLogsToFile, Current.LogRetentionDays)`.
  3. Register the hotkey from `Current.PauseHotkey`. An unparsable gesture or a failed registration logs a Warning and sets `IsHotkeyRegistered=false`.
- `Save(s)`:
  1. Apply in this order, with no restart:
     - `engine.ApplySettings(s.Engine)`, only if the engine settings changed;
     - `logs.Configure`;
     - `startup.Apply(s.StartWithWindows)`;
     - re-register the hotkey, only if the gesture text changed or it is not currently registered.
  2. Set `Current = s` and call `store.Save(s)`. A store `IOException` or `UnauthorizedAccessException` logs a Warning and gives `Saved=false`; the session keeps the new settings.
  3. Raise `Changed` and return the result.
  4. Clear the read-failed guard after a successful explicit save.
- `MarkTrayHintShown()`: set `Current = Current with { TrayHintShown = true }`, then save, unless `loaded.ReadFailed` is still in force. Never throw. Raise `Changed`.

- [ ] **Step 1: Write the failing tests.** Use fakes for the store, registrar, log options and hotkey; a `FakeEngineController`; and a `ListLogger`.
  - `Initialize_FirstLaunchWithRunKey_TurnsStartWithWindowsOn`.
  - `Initialize_InvalidHotkeyText_WarnsAndNotRegistered`.
  - `Initialize_HotkeyTaken_WarnsAndNotRegistered`.
  - `Save_EngineChanged_AppliesToEngine`.
  - `Save_EngineUnchanged_DoesNotApplyToEngine`.
  - `Save_ConfiguresLogsAndStartup`.
  - `Save_NewHotkey_ReRegisters`.
  - `Save_StoreThrows_ReturnsNotSavedButKeepsSettingsInSession`.
  - `Save_RaisesChanged`.
  - `MarkTrayHintShown_SavesWithFlag`.
  - `MarkTrayHintShown_AfterReadFailure_DoesNotSave`.
  - `Save_AfterReadFailure_SavesAndLaterHintSaves`.
- [ ] **Step 2: Run the Application tests.** Expected: the build fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the Application tests, then the CI-filtered suite.** Expected: all pass.
- [ ] **Step 5: Commit:** `feat(application): add settings service that applies and saves settings live`.

---

### Task 5: Home tab and the Custom pause prompt

**Files:**
- Modify: `src/GapMouse.App/ViewModels/HomeViewModel.cs`, `src/GapMouse.App/Views/HomeView.axaml(.cs)`
- Create: `src/GapMouse.App/Localization/Localizer.Home.cs`, `src/GapMouse.App/Views/CustomPauseWindow.axaml(.cs)`, `src/GapMouse.App/ViewModels/CustomPauseViewModel.cs`, `src/GapMouse.App/Services/AvaloniaPausePrompt.cs`
- Test: `tests/GapMouse.App.Tests/ViewModels/HomeViewModelTests.cs`, `CustomPauseViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1's `Localizer`, `IUiDispatcher`, `IPausePrompt`, `IAppLifetime`, `PauseOption` and `PauseOptions`; `IEngineController`; `TimeProvider`.
- Produces:
  - `HomeViewModel(Localizer l, IEngineController engine, IUiDispatcher ui, TimeProvider time, IPausePrompt pausePrompt, IAppLifetime lifetime) : ViewModelBase, IDisposable`, with `void Start()`. `Start` subscribes to `EventRaised` and starts a 1 s `time.CreateTimer`; both call `Refresh()` via `ui.Post`.
  - `CustomPauseViewModel(Localizer l)`, with `string MinutesText`, `int? Minutes` (null when invalid), `string? Error` and `bool CanConfirm`.
  - `AvaloniaPausePrompt : IPausePrompt` shows `CustomPauseWindow` modally over the main window when one is visible, otherwise standalone and centered.

**Home content (spec §5.6):**

| Element | Content |
|---|---|
| State text | Waiting/Aguardando, Moving/Movendo, Paused/Pausado; exposed as `EngineState State` for the view's color |
| `IdleText` | "Idle for {0} s of {1} s" / "Inativo há {0} s de {1} s", whole seconds, idle capped at the threshold |
| `IdleProgress` | 0–1 |
| `PauseText` | when paused: "Resumes in {mm:ss}" / "Retoma em {mm:ss}" (`h:mm:ss` from an hour up), or "Paused until you resume" / "Pausado até você retomar" |
| Times | started, last movement, last return, last pause, last resume (`HH:mm:ss`, or `L.None`) |
| Today | active time (`h:mm:ss`) and activations count |

Commands:
- `PauseCommand(PauseOption)`: `Custom` asks the prompt first and does nothing on null.
- `ResumeCommand`, enabled only when paused.
- `ExitCommand`, which calls `lifetime.Exit()`.

The view uses a pause button with a flyout listing the five options; Resume shows instead when paused.

- [ ] **Step 1: Write the failing tests** (`FakeEngineController`, `FakeTimeProvider`, `ImmediateUiDispatcher`, a fake prompt).
  - `Refresh_Waiting_ShowsIdleOfThreshold`: LastIdle 45 s with threshold 120 s gives "Idle for 45 s of 120 s" and progress 0.375.
  - `Refresh_PausedTimed_ShowsCountdown`: 9 min 41 s left gives "Resumes in 09:41".
  - `Refresh_PausedIndefinite_ShowsUntilResume`.
  - `Refresh_TodayStats_FormatsHours`: 1 h 2 min 3 s gives "1:02:03".
  - `Pause_TenMinutes_PausesEngine`.
  - `Pause_CustomCancelled_DoesNothing`.
  - `Pause_Custom45_PausesFortyFiveMinutes`.
  - `Resume_WhenPaused_ResumesEngine`.
  - `Exit_CallsLifetime`.
  - `Timer_EverySecond_Refreshes`.
  - `LanguageChange_RefreshesTexts`.
  - `Dispose_StopsRefreshing`.
  - `CustomPauseViewModel`:
    - `MinutesText` of `"1"` and `"1440"` is valid.
    - `"0"`, `"1441"`, `""`, `"abc"` and `"2.5"` are invalid, each with an error and `CanConfirm` false.
- [ ] **Step 2: Run the App tests.** Expected: the build fails.
- [ ] **Step 3: Implement the view models, `Localizer.Home.cs`, `HomeView` and `CustomPauseWindow`.** State colors use the tray colors.
- [ ] **Step 4: Run the App tests.** Expected: they pass.
- [ ] **Step 5: Commit:** `feat(app): add home tab with live engine status, pause options and custom pause prompt`.

---

### Task 6: Logs tab

**Files:**
- Modify: `src/GapMouse.App/ViewModels/LogsViewModel.cs`, `src/GapMouse.App/Views/LogsView.axaml(.cs)`
- Create: `src/GapMouse.App/Localization/Localizer.Logs.cs`, `src/GapMouse.App/ViewModels/LogLineViewModel.cs`, `LogFilter.cs`, `src/GapMouse.App/Views/LogKindBrushConverter.cs`
- Test: `tests/GapMouse.App.Tests/ViewModels/LogsViewModelTests.cs`

**Interfaces:**
- Consumes:
  - Task 2's `InMemoryActivityLog`, `LogEntry` (with `Source`) and `LogKind`;
  - Task 1's `IUiDispatcher`, `IClipboardService` and `IShellLauncher`.
- Produces:
  - `enum LogFilter { All, Movement, Pauses, System }`. Movement covers Waiting, Moving and Return; Pauses covers Paused and Resumed; System covers Start, Stop, Config, Warn and Error.
  - `sealed record LogLineViewModel(string Time, string Tag, string Message, LogKind Kind)`.
  - `LogsViewModel(Localizer l, InMemoryActivityLog log, IUiDispatcher ui, IClipboardService clipboard, IShellLauncher launcher, string logsDirectory) : ViewModelBase, IDisposable`, with:
    - `ObservableCollection<LogLineViewModel> Lines` (visible lines, at most 1000);
    - `LogFilter Filter`;
    - `CopyCommand`, which copies the visible lines as `HH:mm:ss | TAG | message`, joined with `Environment.NewLine`;
    - `OpenFolderCommand`;
    - `ClearCommand`, which hides every entry up to now and deletes nothing.

**Localized message:** when `Source` is an engine event, the on-screen message is localized. Otherwise `Message` is shown as is.

| Event | English | Portuguese |
|---|---|---|
| EngineStarted | GapMouse started | GapMouse iniciado |
| WaitingStarted | Waiting for {0} s of inactivity | Aguardando {0} s de inatividade |
| MovingStarted | Moving the mouse every {0}–{1} s | Movendo o mouse a cada {0}–{1} s |
| UserReturned | User activity detected | Atividade do usuário detectada |
| EnginePaused, timed | Paused until {0} | Pausado até {0} |
| EnginePaused, indefinite | Paused indefinitely | Pausado por tempo indeterminado |
| EngineResumed | Resumed (manual) / Resumed (pause expired) | Retomado (manual) / Retomado (tempo esgotado) |
| SettingsChanged | Settings changed | Configurações alteradas |
| EngineStopped | GapMouse stopped | GapMouse encerrado |

Seconds use the `0.##` format in the current culture.

**Colors** (`LogKindBrushConverter`, in Views, readable on both themes):

| Kind | Color |
|---|---|
| Start | yellow |
| Waiting | cyan |
| Moving | green |
| Return | gray |
| Paused, Resumed | magenta |
| Config | dim gray |
| Stop | red |
| Warn | amber |
| Error | red |

- [ ] **Step 1: Write the failing tests:**
  - `Constructor_LoadsExistingEntries`.
  - `EntryAdded_AppendsLine`.
  - `Filter_Movement_ShowsOnlyMovementKinds`.
  - `Filter_System_IncludesWarnAndError`.
  - `Clear_HidesExistingButShowsNew`.
  - `Copy_CopiesVisibleLinesInPipeFormat`.
  - `OpenFolder_OpensLogsDirectory`.
  - `Portuguese_LocalizesEngineEventMessages`: `MovingStarted` 10–25 gives "Movendo o mouse a cada 10–25 s".
  - `NonEngineEntry_ShowsRawMessage`.
  - `LanguageChange_RerendersLines`.
  - `MoreThanCapacity_KeepsLastThousand`.
- [ ] **Step 2 to Step 4: Red, then implement, then green**, as in Task 5.
- [ ] **Step 5: Commit:** `feat(app): add logs tab with filters, copy, open folder and localized messages`.

---

### Task 7: Settings tab

**Files:**
- Modify: `src/GapMouse.App/ViewModels/SettingsViewModel.cs`, `src/GapMouse.App/Views/SettingsView.axaml(.cs)`
- Create: `src/GapMouse.App/Localization/Localizer.Settings.cs`
- Test: `tests/GapMouse.App.Tests/ViewModels/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes:
  - Task 2's `ISettingsService`, `SettingsSaveResult`, `HotkeyGesture` and `HotkeyModifiers`;
  - `EngineSettings.Create` and `SettingsError` (Domain);
  - `AppSettings` with its constants, `AppTheme` and `UiLanguage`.
- Produces: `SettingsViewModel(Localizer l, ISettingsService settings) : ViewModelBase, IDisposable`.
  - Fields:
    - text for idle, check, min, max, amplitude and retention;
    - bools for display, start with Windows, start minimized, notifications, check updates and save logs;
    - `string HotkeyText`, `AppTheme Theme` and `UiLanguage Language`.
  - Errors:
    - a `string?` error per field;
    - `bool HasErrors` and `bool IsDirty`;
    - `string? HotkeyWarning`, shown when `!settings.IsHotkeyRegistered`: "Shortcut unavailable — another app may be using it." / "Atalho indisponível — outro programa pode estar usando."
  - `string? StatusMessage` after a save.
  - `SaveCommand` (enabled only when the form is dirty and has no errors) and `RestoreDefaultsCommand`, which fills the form from `AppSettings.Default` and marks it dirty without saving.
  - `void CaptureHotkey(HotkeyModifiers modifiers, string key)` is called by the view's capture box `KeyDown`. The view maps Avalonia `Key` to `A`–`Z`, `D0`–`D9` → `0`–`9`, and `F1`–`F24`.

**Validation:**
- Integers are parsed with the current culture.
- Engine fields are validated through `EngineSettings.Create`. Map each `SettingsError.Field` to its field and give a localized message per `SettingsErrorCode`, for example min greater than max.
- Retention must be 1–365.
- The hotkey must pass `HotkeyGesture.TryParse` and is stored in canonical form.

**Status messages after `Save`:**

| Result | English | Portuguese |
|---|---|---|
| saved | Settings saved. | Configurações salvas. |
| not saved to disk | Applied, but the settings file could not be written. | Aplicadas, mas o arquivo de configurações não pôde ser gravado. |
| startup failed | Saved, but start with Windows could not be changed. | Salvas, mas não foi possível alterar o início com o Windows. |
| hotkey failed | the hotkey warning | |

The form reloads from `settings.Current` when `Changed` fires and the form is not dirty.

- [ ] **Step 1: Write the failing tests** (fake `ISettingsService`):
  - `Load_ShowsCurrentValues`.
  - `MinGreaterThanMax_ShowsErrorAndDisablesSave`.
  - `EmptyIdle_ShowsError`.
  - `Letters_ShowError`.
  - `Retention0_ShowsError`.
  - `InvalidHotkeyText_ShowsError`.
  - `CaptureHotkey_SetsCanonicalText`: Ctrl+Alt with `K` gives `"Ctrl+Alt+K"`.
  - `Save_ValidForm_CallsServiceWithParsedSettings`.
  - `Save_NotSaved_ShowsDiskMessage`.
  - `RestoreDefaults_FillsDefaultsAndIsDirtyWithoutSaving`.
  - `HotkeyNotRegistered_ShowsWarning`.
  - `ExternalChange_ReloadsWhenNotDirty`.
  - `Portuguese_ErrorMessagesInPortuguese`.
- [ ] **Step 2 to Step 4: Red, then implement** (the view is grouped into Movement, Power, System, Appearance and Logs, with inline errors under each field), **then green**.
- [ ] **Step 5: Commit:** `feat(app): add settings tab with inline validation, hotkey capture and live apply`.

---

### Task 8: About tab

**Files:**
- Modify: `src/GapMouse.App/ViewModels/AboutViewModel.cs`, `src/GapMouse.App/Views/AboutView.axaml(.cs)`
- Create: `src/GapMouse.App/Localization/Localizer.About.cs`
- Test: `tests/GapMouse.App.Tests/ViewModels/AboutViewModelTests.cs`

**Interfaces:**
- Consumes: Task 2's `IUpdateChecker`, Plan 2's `AppPaths` and `UpdateCheckResult`, and Task 1's `IShellLauncher` and `IUiDispatcher`.
- Produces: `AboutViewModel(Localizer l, IUpdateChecker updates, IShellLauncher launcher, IUiDispatcher ui, AppPaths paths, string version) : ViewModelBase, IDisposable`, with:
  - `VersionText`;
  - `RepositoryUrl`, the constant `https://github.com/Gustavo-Piccoli/GapMouse`;
  - `LicenseText` "MIT License" / "Licença MIT";
  - `SettingsFilePath` and `LogsDirectory`;
  - `CheckUpdatesCommand` (async, disabled while running);
  - `UpdateStatus` text;
  - `Uri? AvailableReleaseUrl` with `OpenReleaseCommand`;
  - `OpenRepositoryCommand`, `OpenSettingsFolderCommand` and `OpenLogsFolderCommand`.
  - `UpdateFound`, raised by the scheduled check, sets the available state through `ui.Post`.

**Status texts:**

| State | English | Portuguese |
|---|---|---|
| checking | Checking… | Verificando… |
| up to date | You're up to date. | Você está na versão mais recente. |
| no releases | No releases published yet. | Nenhuma versão publicada ainda. |
| failed | Could not check for updates. | Não foi possível verificar atualizações. |
| available | Version {0} is available. | Versão {0} disponível. |

- [ ] **Step 1: Write the failing tests:**
  - `CheckUpdates_UpdateAvailable_ShowsVersionAndUrl`.
  - `CheckUpdates_UpToDate_ShowsMessage`.
  - `CheckUpdates_Failed_ShowsMessage`.
  - `CheckUpdates_WhileRunning_CommandDisabled`.
  - `UpdateFoundEvent_ShowsAvailable`.
  - `OpenRelease_OpensUrl`.
  - `OpenSettingsFolder_OpensConfigDirectory`.
  - `Portuguese_Texts`.
- [ ] **Step 2 to Step 4: Red, then implement, then green.**
- [ ] **Step 5: Commit:** `feat(app): add about tab with version, paths and manual update check`.

---

### Task 9: Tray coordinator, notifications and hotkey toggle

**Files:**
- Create: `src/GapMouse.App/ViewModels/TrayCoordinator.cs`, `src/GapMouse.App/Localization/Localizer.Tray.cs`
- Test: `tests/GapMouse.App.Tests/ViewModels/TrayCoordinatorTests.cs`

**Interfaces:**
- Consumes:
  - Task 2's `ITrayIcon`, `TrayIconState`, `TrayMenuItem`, `IGlobalHotkey`, `ISettingsService` and `IUpdateChecker`;
  - Task 1's `IPausePrompt`, `IAppLifetime`, `IMainWindowService`, `IUiDispatcher`, `IShellLauncher`, `PauseOption` and `PauseOptions`;
  - `IEngineController`.
- Produces: `public sealed class TrayCoordinator(Localizer l, IEngineController engine, ITrayIcon tray, IGlobalHotkey hotkey, ISettingsService settings, IUpdateChecker updates, IPausePrompt pausePrompt, IMainWindowService window, IAppLifetime lifetime, IShellLauncher launcher, IUiDispatcher ui) : IDisposable`, with `void Start()` and `void OnMainWindowHidden()`.

**Behavior:**
- **Start:** call `tray.SetState(map(engine.Snapshot.State), tooltip)`. The tooltip is `"GapMouse — {state}"`, localized. Language changes refresh the tooltip.
- **Engine events** (through `ui.Post`):
  - Update the state and tooltip.
  - When `settings.Current.NotificationsEnabled` is on, show a silent balloon for:

    | Event | English | Portuguese |
    |---|---|---|
    | `MovingStarted` | Keeping your PC awake | Mantendo seu PC ativo |
    | `UserReturned` | Welcome back | Bem-vindo de volta |
    | `EnginePaused` | Paused | Pausado |
    | `EngineResumed` | Resumed | Retomado |

    The balloon title is `GapMouse` and the body is that phrase.
- **`MenuRequested`:**
  - When paused, the menu is `[Resume]`. Otherwise it is `[Pause ▸ 10 min, 30 min, 1 h, Custom…, Never]`.
  - Then a separator, `Open` and `Exit`, all localized. Portuguese: Pausar, Personalizado…, Nunca, Retomar, Abrir, Sair.
  - Item ids are `pause:TenMinutes|ThirtyMinutes|OneHour|Custom|Never`, `resume`, `open` and `exit`. Execute the chosen one. Custom goes through `pausePrompt`.
- **`OpenRequested`:** `window.Show()`.
- **`hotkey.Pressed`:** resume when paused, otherwise `Pause(PauseDuration.Indefinite)`.
- **`updates.UpdateFound`:** show a non-silent balloon "Update available" / "Atualização disponível" with "Version {0} is available." / "Versão {0} disponível.". The tag is `update:<url>`. Show it once per version per run. `BalloonClicked` with that tag calls `launcher.OpenUrl`.
- **`OnMainWindowHidden()`:** when `!settings.Current.TrayHintShown`, show the balloon "GapMouse is still running in the tray" / "O GapMouse continua rodando na bandeja", then call `settings.MarkTrayHintShown()`.

- [ ] **Step 1: Write the failing tests** (fakes for each port):
  - `Start_SetsWaitingIconAndTooltip`.
  - `MovingStarted_SwitchesToMovingIcon`.
  - `Paused_SwitchesToPausedIcon`.
  - `NotificationsOff_NoBalloonOnStateChange`.
  - `NotificationsOn_MovingStarted_ShowsSilentBalloon`.
  - `Menu_WhenRunning_HasPauseSubmenuOpenExit`.
  - `Menu_WhenPaused_HasResume`.
  - `Menu_ChooseThirtyMinutes_PausesThirty`.
  - `Menu_ChooseCustom_UsesPrompt`.
  - `Menu_ChooseExit_CallsLifetime`.
  - `DoubleClick_ShowsWindow`.
  - `Hotkey_WhenRunning_PausesIndefinitely`.
  - `Hotkey_WhenPaused_Resumes`.
  - `UpdateFound_ShowsBalloonOncePerVersion`.
  - `UpdateBalloonClicked_OpensReleaseUrl`.
  - `FirstWindowHide_ShowsHintAndMarksShown`.
  - `LaterWindowHide_NoHint`.
  - `Portuguese_MenuTexts`.
- [ ] **Step 2 to Step 4: Red, then implement, then green.**
- [ ] **Step 5: Commit:** `feat(app): add tray coordinator with state icon, menu, notifications and hotkey toggle`.

---

### Task 10: Composition, lifecycle, docs and smoke test

**Files:**
- Modify: `src/GapMouse.App/Program.cs`, `App.axaml.cs`, `Views/MainWindow.axaml.cs`
- Create:
  - `src/GapMouse.App/Composition/AppComposition.cs`;
  - `src/GapMouse.App/Services/MainWindowService.cs`, `AppShutdown.cs`, `ThemeApplier.cs`;
  - `src/GapMouse.App/StartupOptions.cs`.
- Create: `docs/adr/0006-windows-shell-integration.md`
- Modify: `README.md`, `CLAUDE.md`
- Test: `tests/GapMouse.App.Tests/Services/AppShutdownTests.cs`, `StartupOptionsTests.cs`, `Composition/AppCompositionTests.cs`

**Interfaces:**
- Consumes everything above.
- Produces:
  - `StartupOptions.Parse(string[] args)`, which returns `bool Minimized` for `--minimized`, case-insensitive. Other arguments are ignored.
  - `AppShutdown(IReadOnlyList<Action> steps, ILogger<AppShutdown> logger) : IAppLifetime`. `Exit()` runs each step once, in order, even if one throws, then is a no-op on later calls. It is thread-safe.
  - `MainWindowService(Func<MainWindow> factory, TrayCoordinator tray) : IMainWindowService`.
    - It creates the window lazily.
    - `Show()` restores it if minimized, shows it, activates it and brings it to the front.
    - The window's `Closing` event, unless it comes from the shutdown, cancels the close, hides the window and calls `tray.OnMainWindowHidden()`.
  - `AppComposition.Build(AppPaths paths, StartupOptions options, SettingsLoadResult loaded, Action<IServiceCollection>? overrides = null)` builds an `IServiceProvider` (`ServiceCollection`) with explicit singleton factory registrations only. It uses no open generics and no reflection-based activation that trimming would break. One closed `ILogger<T>` is registered per consumer as `ActivityLogger<T>(compositeLog, TimeProvider.System)`.

**Startup sequence** (`Program.Main`, then `OnFrameworkInitializationCompleted`):
1. Call `SingleInstance.TryAcquire()`. When it returns null, call `SingleInstance.SignalExisting()` and return 0 **before** Avalonia starts.
2. Resolve paths with `AppPathsResolver.Resolve(AppContext.BaseDirectory, %AppData%, %LocalAppData%, File.Exists)`.
3. Load the settings. Each warning becomes a `Warn` log entry once the logs exist.
4. Logs: `FileActivityLog`, plus `InMemoryActivityLog`, combined in a `CompositeActivityLog`.
5. Restore today's stats, then create the `EngineHost` (with `ActivityLogger<EngineHost>`), `ActivityLogWriter(host, compositeLog)` and `AppStatePersister`, and call `Start()` on each.
6. Create the `ShellWindow`, then `Win32TrayIcon` (with the tray icon bytes read from the `avares://GapMouse.App/Assets/Icons/*.ico` assets) and `Win32GlobalHotkey`.
7. `SettingsService.Initialize()`.
8. Apply the theme (`Application.RequestedThemeVariant`) and the language (`Localizer.SetLanguage`). Both are re-applied on `ISettingsService.Changed`.
9. Start `TrayCoordinator` and `HomeViewModel`. Call `UpdateService.Start(() => settings.Current.CheckForUpdates)` with the `HttpClient` and `GitHubReleaseSource` (product version = `InformationalVersion`).
10. `ShellWindow.ActivationRequested` calls `MainWindowService.Show()`, and `ShellWindow.SessionEnding` calls `AppShutdown.Exit()`.
11. Unless `options.Minimized || settings.Current.StartMinimized`, call `MainWindowService.Show()`.

`AppShutdown` steps, in Global Constraints order, then `desktop.Shutdown()` last.

- [ ] **Step 1: Write the failing tests.**
  - `StartupOptionsTests`: `--minimized`, `--MINIMIZED` and no arguments.
  - `AppShutdownTests`:
    - `Exit_RunsStepsInOrder`.
    - `Exit_StepThrows_RunsRemainingAndLogs`.
    - `Exit_Twice_RunsOnce`.
    - `Exit_ConcurrentCalls_RunsOnce`.
  - `AppCompositionTests`: building with temp-dir paths and an `overrides` callback (which replaces `ShellWindow`, `ITrayIcon` and `IGlobalHotkey` with fakes) resolves every view model and service without throwing. Do not start the host or the shell.
- [ ] **Step 2: Run the App tests.** Expected: the build fails.
- [ ] **Step 3: Implement the startup sequence and services.** Replace Task 1's temporary startup.
- [ ] **Step 4: Docs.**
  - **ADR 0006** (Context / Decision / Consequences, ≤ 40 lines) covers:
    - the hidden top-level window versus a message-only window (Global Constraints ruling);
    - `Shell_NotifyIcon` balloons versus toasts;
    - `RegisterHotKey`;
    - the mutex plus activate message;
    - running on Avalonia's UI thread;
    - the shutdown order.
  - **README:**
    - a "Using GapMouse" section: tray colors, menu, hotkey, close to tray, start minimized and the tabs;
    - update the Architecture section with the App and Shell parts;
    - set the roadmap so Plan 3 is current;
    - leave screenshots and a GIF for Plan 4.
  - **CLAUDE.md** (Portuguese, with accents):
    - the current state is Plan 3;
    - the `GapMouse.App` structure (Views, ViewModels, Localization, Services, Composition) and the rule that view models have no Avalonia references;
    - how to run the app (`dotnet run --project src/GapMouse.App`);
    - add ADR 0006;
    - the non-obvious points:
      - the shell window runs on Avalonia's message loop;
      - localization is `T(en, pt)` per area;
      - `--minimized`;
      - single instance blocks a second `dotnet run`.
- [ ] **Step 5: Verify.**
  - Run `dotnet build -c Release` (0 warnings), `dotnet test --filter-not-trait "Category=Win32"` (all pass), `dotnet test` (all pass, including Win32) and `dotnet format --verify-no-changes`.
  - Run `dotnet publish src/GapMouse.App -c Release -r win-x64 -o artifacts/app` (0 warnings) and record the size.
  - Smoke test:
    1. Create an empty `artifacts/app/portable` file.
    2. Start `GapMouse.exe --minimized`.
    3. After 3 s, the process is alive, and `artifacts/app/logs/gapmouse-<today>.log` has a START line.
    4. Start a second `GapMouse.exe`. It exits with code 0 within 5 s, and the first instance is still alive.
    5. Kill the first with `taskkill /PID <pid> /F`.
    6. Delete `artifacts/app/portable`, the logs and the settings created by the smoke test.
  - Record the results in the report.
  - List for the controller the manual checks that need a human:
    - the tray menu;
    - double-click;
    - the hotkey;
    - close to tray and the hint balloon;
    - live theme and language switching;
    - Explorer restart.
- [ ] **Step 6: Commit:** `feat(app): compose the desktop app with tray-first lifecycle, single instance and session-end shutdown`. Docs may go in a second `docs:` commit.

---

## Execution notes

- **Branch:** `feat/desktop-app` from `main`, delivered through a PR. `main` needs `build-test` and `aot-publish`.
- **Waves:**
  1. Tasks 1 and 2 in parallel. They touch disjoint projects, except that both edit test projects: Task 1 moves `FakeEngineController` and `ListLogger` into `GapMouse.Testing`, and Task 2 must not touch those files.
  2. Tasks 3–9 in parallel worktrees. Each owns its own files and its own `Localizer.<Area>.cs`. No task edits `Localizer.cs`, `Localizer.Common.cs`, `MainWindow` or the csproj, except where its Files list says so.
  3. Task 10 last.
- **Models:** every agent is dispatched with model **sonnet** (spec §3).
