# Lock Pause Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop moving the mouse while the Windows session is locked, with a setting to keep the system awake while locked (default off = release power), plus LOCKED/UNLOCKED log lines, a de-duplicated port failure message and a logged port recovery line.

**Architecture:** The lock is a flag on `KeepAwakeEngine`, orthogonal to Waiting/Moving/Paused, with a time-only advance path so the host reads no cursor while locked. `ShellWindow` receives WTS session notifications and the App wires them to `EngineHost.SetSessionLocked`, which enqueues to the engine thread like every other command.

**Tech Stack:** .NET 10, Avalonia 12.1.4, Native AOT, xUnit on Microsoft.Testing.Platform, Win32 `wtsapi32`.

**Spec:** `docs/superpowers/specs/2026-11-01-gapmouse-lock-pause-design.md`

## Global Constraints

- Layers: Domain pure (no I/O); Application ports + logic only, no `Microsoft.Win32` / `System.Text.Json` / `System.Net.Http` (architecture tests); Win32 only in Infrastructure.
- Every power call happens on the engine thread (`SetThreadExecutionState` is per thread).
- Native AOT, compiled bindings (`x:DataType`), warnings are errors, `dotnet format --verify-no-changes` passes.
- Every new UI string is `T("English", "Português")` in `Localizer.<Area>.cs` with a `LocalizerTests` case; log tags and on-disk messages stay English.
- Colours only via tokens or the converter tables.
- Settings JSON: missing field = default, no warning; new field is `bool`.
- Tests: `dotnet test --filter-not-trait "Category=Win32"`; real-Win32 tests carry `[Trait("Category","Win32")]`.
- Conventional Commits, English, **no trailers of any kind** (no Co-Authored-By, no Claude-Session, no Generated-with).
- Never stop/kill a GapMouse process or launch the real app; use `tools/GapMouse.Screenshots` to see UI.

Exact copy (spec §5.3, §5.5):
- Disk log: `Screen locked; movement stopped, power released` / `Screen locked; movement stopped, system kept awake` / `Screen unlocked`; tags `LOCKED`, `UNLOCKED`.
- UI log (PT): `Tela bloqueada; movimento parado, energia liberada` / `Tela bloqueada; movimento parado, sistema mantido acordado` / `Tela desbloqueada`.
- Setting: T("Keep the PC awake while the screen is locked", "Manter o PC acordado com a tela bloqueada"); caption T("The mouse stays still while locked; the screen can turn off.", "O mouse fica parado com a tela bloqueada; a tela pode desligar.").

## Review Focus

1. Lock arrives mid-movement and unlock follows seconds later: no stray cursor move after lock, Waiting restarts its idle count on unlock (Task 1 test `LockWhileMoving_ThenUnlock_RestartsWaiting`).
2. Windows sends LOCK twice, or UNLOCK without LOCK (RDP reconnects, fast user switching): no duplicate log lines, no state change (Task 1 test `SetSessionLocked_IsIdempotent`).
3. App starts with the session already locked, or lock arrives before the host thread starts: the first iteration reads no cursor (Task 2 test `LockedBeforeStart_FirstIterationReadsNoCursor`).
4. "Keep the display on" is checked and "keep awake while locked" is on: while locked only `KeepSystemAwake` is requested (Task 1 test `Locked_KeepAwake_RequestsSystemOnly_EvenWithDisplayOn`).
5. A user upgrading from 1.1 has a settings file without the field: it loads with the default and no warning (Task 3 test `Load_WithoutKeepAwakeWhileLocked_UsesDefaultWithoutWarning`).

---

### Task 1: Engine lock state (Domain)

**Files:**
- Modify: `src/GapMouse.Domain/Settings/EngineSettings.cs`, `src/GapMouse.Domain/Engine/KeepAwakeEngine.cs`, `src/GapMouse.Domain/Engine/EngineEvents.cs`, `src/GapMouse.Domain/Engine/EngineSnapshot.cs`
- Test: `tests/GapMouse.Domain.Tests/Engine/KeepAwakeEngineLockTests.cs` (new), `tests/GapMouse.Domain.Tests/Settings/…` (existing EngineSettings tests, add cases)

**Interfaces:**
- Produces:
  - `EngineSettings.KeepAwakeWhileLocked : bool` (Default `false`); `EngineSettings.Create(..., bool keepDisplayOn, bool keepAwakeWhileLocked = false)` — optional last parameter so the ~17 existing callers compile unchanged.
  - `sealed record SessionLocked(DateTimeOffset At, bool KeepAwake) : EngineEvent(At)`; `sealed record SessionUnlocked(DateTimeOffset At) : EngineEvent(At)`.
  - `KeepAwakeEngine.IsSessionLocked : bool`; `void SetSessionLocked(bool locked, DateTimeOffset now)`; `EngineStep AdvanceLocked(DateTimeOffset now)`.
  - `EngineSnapshot` gains a last positional member `bool IsSessionLocked`.

- [ ] **Step 1: Write failing tests** in `KeepAwakeEngineLockTests` (use the existing engine test helpers/fakes in that folder):
  - `Lock_FromWaiting_EmitsSessionLocked_AndAdvanceLockedNeverMoves` — `SessionLocked(now, false)` drained; `AdvanceLocked(later)` returns `MoveCursorTo == null`, `Power == PowerRequest.None`, `WaitBeforeNext == CheckInterval`.
  - `LockWhileMoving_ThenUnlock_RestartsWaiting` — engine in Moving; lock → `State == Waiting`, events contain `SessionLocked` and no `UserReturned`; unlock → events `SessionUnlocked` then `WaitingStarted`.
  - `LockWhilePaused_Indefinitely_UnlockStaysPaused` — `State == Paused` after unlock; only `SessionUnlocked` emitted.
  - `TimedPauseExpiresWhileLocked_ResumesButDoesNotMove` — `AdvanceLocked` past `PauseEndsAt` emits `EngineResumed(PauseExpired)`, state Waiting, no move; further `AdvanceLocked` still no move.
  - `Locked_KeepAwake_RequestsSystemOnly_EvenWithDisplayOn` — settings with `keepDisplayOn: true, keepAwakeWhileLocked: true`: Waiting+locked → `PowerRequest.KeepSystemAwake`; Paused+locked → `PowerRequest.None`.
  - `SetSessionLocked_IsIdempotent` — lock twice / unlock when unlocked emit nothing extra and change nothing.
  - `Advance_WhileLocked_BehavesLikeAdvanceLocked` — calling `Advance(input)` with idle above threshold while locked does not enter Moving and does not move.
  - `Locked_DoesNotAccumulateMovingTime` — snapshot `Today.MovingTime` unchanged across locked advances.
  - `Snapshot_ReportsIsSessionLocked`.
  - In the EngineSettings tests: `Create_DefaultsKeepAwakeWhileLockedToFalse`, `Create_SetsKeepAwakeWhileLocked`, `Default_KeepAwakeWhileLocked_IsFalse`.
- [ ] **Step 2:** `dotnet test tests/GapMouse.Domain.Tests` → new tests fail to compile/fail.
- [ ] **Step 3: Implement.** `AdvanceLocked` handles day rollover like `Advance`, expires timed pauses via the existing `EnterWaitingFromPause`, returns Paused→`None` (wait `Min(CheckInterval, endsAt - now)` for timed pauses), Waiting→`KeepSystemAwake` if `KeepAwakeWhileLocked` else `None` (wait `CheckInterval`), sets `_lastAdvanceAt = now` and `_lastAdvanceWasMoving = false`. `Advance` delegates to `AdvanceLocked(input.Now)` when locked. Lock from Moving: drop `_path`, `State = Waiting`, no `UserReturned`. Unlock in Waiting emits `WaitingStarted(now, IdleThreshold)` after `SessionUnlocked`.
- [ ] **Step 4:** `dotnet test tests/GapMouse.Domain.Tests` → all pass; `dotnet build` 0 warnings (fix any `EngineSnapshot` constructor callers).
- [ ] **Step 5: Commit** `feat(domain): pause the engine while the session is locked`.

### Task 2: Host, activity log and log kinds (Application)

**Files:**
- Modify: `src/GapMouse.Application/Engine/IEngineController.cs`, `src/GapMouse.Application/Engine/EngineHost.cs`, `src/GapMouse.Application/Logging/ActivityLogger.cs`, `src/GapMouse.Application/Logging/LogKind.cs`, `src/GapMouse.Application/Logging/LogEntry.cs`, `src/GapMouse.Application/Logging/LogEntryFormatter.cs`; any `IEngineController` fakes in `tests/GapMouse.Testing` / `tests/*/Fakes`
- Test: `tests/GapMouse.Application.Tests/Engine/EngineHostTests.cs` (or a new `EngineHostLockTests.cs`), `tests/GapMouse.Application.Tests/Logging/*`

**Interfaces:**
- Consumes: Task 1 (`SetSessionLocked`, `IsSessionLocked`, `AdvanceLocked`, `SessionLocked`, `SessionUnlocked`).
- Produces: `IEngineController.SetSessionLocked(bool locked)`; `LogKind.Locked`, `LogKind.Unlocked` (appended after `Error`); tags `LOCKED`, `UNLOCKED`.

- [ ] **Step 1: Write failing tests:**
  - `SetSessionLocked_StopsCursorAndMonitorReads` — fake idle above threshold; after lock, counting fakes see no `GetPosition`/`GetBoundsAt`/`TrySetPosition` calls; power fake last applied `PowerRequest.None`.
  - `LockedWithKeepAwake_AppliesSystemOnly` — settings `keepAwakeWhileLocked: true`, `keepDisplayOn: true` → last applied `KeepSystemAwake`.
  - `LockedBeforeStart_FirstIterationReadsNoCursor` — `SetSessionLocked(true)` before `Start()`.
  - `Unlock_ResumesReadsAndMovement`.
  - `PortRecovery_IsWrittenToActivityLog` — cursor fake throws once then succeeds; activity log contains `WARN` `Port Cursor failed: …` then `WARN` `Port Cursor recovered`.
  - ActivityLogger: `Log_DoesNotRepeatExceptionMessageAlreadyInText` (formatted "Port Cursor failed: Acesso negado." + exception message "Acesso negado." → entry message exactly `Port Cursor failed: Acesso negado.`); `Log_AppendsExceptionMessageWhenAbsent` (existing behaviour kept).
  - Formatter: `SessionLocked(false)` → `LOCKED` / `Screen locked; movement stopped, power released`; `SessionLocked(true)` → `Screen locked; movement stopped, system kept awake`; `SessionUnlocked` → `UNLOCKED` / `Screen unlocked`; `LogEntry.Tag` for both kinds.
- [ ] **Step 2:** run `dotnet test tests/GapMouse.Application.Tests` → fail.
- [ ] **Step 3: Implement.** `EngineHost.SetSessionLocked` = `Enqueue((engine, now) => engine.SetSessionLocked(locked, now))`. In `Iterate`, after draining commands: if `engine.IsSessionLocked`, `step = engine.AdvanceLocked(now)`, apply power, no port reads, no move. Change `LogPortRecovered` to `LogLevel.Warning`. `ActivityLogger` appends `": " + exception.Message` only when the formatted message does not already contain `exception.Message` (ordinal). Update every `IEngineController` implementation in tests.
- [ ] **Step 4:** `dotnet test --filter-not-trait "Category=Win32"` → all pass (architecture tests included); `dotnet build` 0 warnings.
- [ ] **Step 5: Commit** `feat(app): lock-aware engine host and activity log lines` (scope may be `application`).

### Task 3: Settings persistence and session notifications (Infrastructure)

**Files:**
- Modify: `src/GapMouse.Infrastructure/Settings/SettingsDocument.cs`, `src/GapMouse.Infrastructure/Settings/JsonSettingsStore.cs`, `src/GapMouse.Infrastructure/Shell/ShellWindow.cs`, `src/GapMouse.Infrastructure/Shell/ShellNativeMethods.cs`
- Test: `tests/GapMouse.Infrastructure.Tests/Settings/JsonSettingsStoreTests.cs`, `tests/GapMouse.Infrastructure.Tests/Shell/ShellWindowTests.cs` (existing or new, Win32 trait)

**Interfaces:**
- Consumes: Task 1 `EngineSettings.Create(..., keepAwakeWhileLocked)`.
- Produces: JSON engine-section field `keepAwakeWhileLocked` (`bool?` in the document, default `false`); `ShellWindow.SessionLockChanged : event EventHandler<bool>?` (true = locked); `public static bool ShellWindow.QueryIsSessionLocked()` (false on any failure).

- [ ] **Step 1: Write failing tests:**
  - `Load_WithoutKeepAwakeWhileLocked_UsesDefaultWithoutWarning` (a 1.1-shaped file: `Engine.KeepAwakeWhileLocked == false`, no load warning, `FileExisted == true`).
  - `SaveThenLoad_RoundTripsKeepAwakeWhileLocked`.
  - Win32 trait: `ShellWindow_RegistersAndUnregistersSessionNotifications` (create + dispose without throwing; `HandlerFailed` not raised); `QueryIsSessionLocked_ReturnsFalseInInteractiveSession`.
  - Non-Win32 if the message handling is reachable through an internal seam (e.g. an internal `HandleMessage` test hook already used by existing shell tests): `WtsSessionLock_RaisesSessionLockChangedTrue`, `WtsSessionUnlock_RaisesFalse`, `OtherWtsCodes_RaiseNothing`. If no seam exists, make these Win32-trait tests that post `WM_WTSSESSION_CHANGE` to the window.
- [ ] **Step 2:** run the Infrastructure tests → fail.
- [ ] **Step 3: Implement.** Constants `WM_WTSSESSION_CHANGE = 0x02B1`, `WTS_SESSION_LOCK = 0x7`, `WTS_SESSION_UNLOCK = 0x8`, `NOTIFY_FOR_THIS_SESSION = 0`. `LibraryImport("wtsapi32.dll")` for `WTSRegisterSessionNotification`, `WTSUnRegisterSessionNotification`, `WTSQuerySessionInformationW`, `WTSFreeMemory`; `kernel32` `WTSGetActiveConsoleSessionId` is not needed — query the current session with `WTS_CURRENT_SESSION` (-1) and `WTSSessionInfoEx` (info class 25), level 1, read `SessionFlags`: `WTS_SESSIONSTATE_LOCK = 0` means locked, `WTS_SESSIONSTATE_UNLOCK = 1` unlocked (Windows 10+ semantics; note the Windows 7 inversion in a comment). Registration failure is reported through `HandlerFailed` and does not throw from the constructor. Unregister in `Dispose` before `DestroyWindow`.
- [ ] **Step 4:** `dotnet test` (including Win32 tests locally — they don't move the cursor here) → pass; `dotnet build` 0 warnings.
- [ ] **Step 5: Commit** `feat(infra): persist the lock setting and listen for session lock changes`.

### Task 4: App wiring, Settings UI, Logs and tray

**Files:**
- Modify: `src/GapMouse.App/App.axaml.cs`, `src/GapMouse.App/ViewModels/SettingsViewModel.cs`, `src/GapMouse.App/Views/SettingsView.axaml`, `src/GapMouse.App/Localization/Localizer.Settings.cs`, `src/GapMouse.App/Localization/Localizer.Logs.cs`, `src/GapMouse.App/ViewModels/LogMessageLocalizer.cs`, `src/GapMouse.App/ViewModels/LogsViewModel.cs`, `src/GapMouse.App/ViewModels/TrayCoordinator.cs`, `src/GapMouse.App/Views/LogKindBrushConverter.cs`, `src/GapMouse.App/Views/LogKindInkConverter.cs`, `tools/GapMouse.Screenshots/Scenarios.cs`
- Test: `tests/GapMouse.App.Tests/ViewModels/SettingsViewModelTests.cs`, `…/LogsViewModelTests.cs`, `…/TrayCoordinatorTests.cs`, `…/LogMessageLocalizerTests.cs` (or wherever these live), `tests/GapMouse.App.Tests/Localization/LocalizerTests.cs`, `tests/GapMouse.App.Tests/Views/LogKindConvertersTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces: `SettingsViewModel.KeepAwakeWhileLocked : bool`; `Localizer.SettingsKeepAwakeWhileLocked`, `Localizer.SettingsKeepAwakeWhileLockedCaption`, `Localizer.LogSessionLocked(bool keepAwake)`, `Localizer.LogSessionUnlocked`.

- [ ] **Step 1: Write failing tests:**
  - Settings VM: toggling `KeepAwakeWhileLocked` marks dirty; Save persists it in `AppSettings.Engine` and calls `ApplySettings` with it; loading reflects the stored value.
  - `LogMessageLocalizer`: `SessionLocked(false)` / `SessionLocked(true)` / `SessionUnlocked` → the PT strings from Global Constraints under pt-BR, EN equivalents under en.
  - Logs VM: the Pauses filter includes `Locked` and `Unlocked`; the System filter does not.
  - Converters: `Locked`/`Unlocked` → the same fill and ink as `Paused`.
  - Tray: `SessionLocked` after `MovingStarted` sets the tray state to Waiting with no balloon; `SessionUnlocked` shows no balloon.
  - `LocalizerTests`: one case per new string (EN and PT).
- [ ] **Step 2:** run `dotnet test tests/GapMouse.App.Tests` → fail.
- [ ] **Step 3: Implement.** Settings row in the Energia card directly under "Keep the display on": label + `TextBlock.caption` stacked in column 0, `ToggleSwitch` in column 1 with `AutomationProperties.Name` = the label, same row pattern. App wiring in `App.axaml.cs` next to the other shell handlers: subscribe `shell.SessionLockChanged += (_, locked) => host.SetSessionLocked(locked);` **before** `if (ShellWindow.QueryIsSessionLocked()) host.SetSessionLocked(true);` (subscribe first so no change is missed). Screenshot tool: add a `LOCKED` and an `UNLOCKED` entry to the sample log (e.g. 12:30:00 / 12:45:10) with the disk messages.
- [ ] **Step 4:** `dotnet test --filter-not-trait "Category=Win32"` pass; `dotnet build` 0 warnings; `dotnet run --project tools/GapMouse.Screenshots`; open `dark-pt/settings-min.png`, `light-en/settings.png`, `dark-pt/logs-min.png`, `light-en/logs.png`: the new row wraps without clipping at 640×520, `UNLOCKED` fits the 76 px tag. `dotnet publish src/GapMouse.App -c Release -r win-x64 -o <scratch>` warning-free.
- [ ] **Step 5: Commit** `feat(app): keep-awake-while-locked setting and lock log lines`.

### Task 5: Docs

**Files:**
- Create: `docs/adr/0008-pause-while-session-locked.md` (Portuguese, same format as existing ADRs)
- Modify: `CLAUDE.md` (Portuguese), `README.md` (English), `docs/images/settings-dark.png` (re-copy from `artifacts/screenshots/dark-en/settings.png`)

- [ ] **Step 1:** ADR 0008: context (Win+L kept moving, access denied on the secure desktop), decision (lock flag in the engine, orthogonal to pause; default releases power; keep-awake option is system-only), alternatives B and C from spec §4, consequences.
- [ ] **Step 2:** CLAUDE.md: add ADR 0008 to the list; "Pontos não óbvios": the lock is orthogonal to the manual pause, unlock restores the prior state, a timed pause can expire while locked without moving, locked power = `None` or `KeepSystemAwake` (never the display), the host reads no cursor while locked, `WTS` notifications arrive on the UI-thread `ShellWindow` and go to the engine via `SetSessionLocked`. Update the "Estado atual" line for 1.2 lock pause.
- [ ] **Step 3:** README: one Features bullet about pausing while the screen is locked and the option.
- [ ] **Step 4:** `dotnet format --verify-no-changes`, `dotnet build`.
- [ ] **Step 5: Commit** `docs: record the lock pause decision`.
