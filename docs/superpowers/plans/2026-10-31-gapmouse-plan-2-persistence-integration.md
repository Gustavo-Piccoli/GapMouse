# GapMouse — Plan 2: Persistence & System Integration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the headless engine everything it needs to behave like an installed app, still without a UI:
- settings saved as JSON;
- portable vs. installed data folders;
- activity logs on disk with retention;
- daily stats that survive restarts;
- start with Windows;
- an opt-out GitHub update check.

The dev console exercises all of these end to end.

**Architecture:** Each concern follows the Plan 1 pattern:
- policy and pure logic live in `GapMouse.Application`;
- the port is an interface in Application;
- the I/O adapter lives in `GapMouse.Infrastructure`.

`AppSettings` lives in Application, not Domain. It wraps the Domain `EngineSettings` and adds app preferences (theme, language, hotkey, logs), which are not keep-awake domain rules. Plan 3 (Avalonia app) only composes these pieces.

**Tech Stack:**
- .NET 10 and C#.
- `System.Text.Json` with source generators only.
- `Microsoft.Win32.Registry` (HKCU).
- `HttpClient`.
- `TimeProvider` / `FakeTimeProvider`.
- xUnit v3 + Shouldly on Microsoft.Testing.Platform.
- NetArchTest.

**Spec:** `docs/superpowers/specs/2026-10-31-gapmouse-design.md` (Portuguese). Read §2 (anti-goals), §5.3, §5.6 (Configurações, Logs, "Hoje"), §5.7 items 4 and 7, §6 (portable detection, Run key), §7 and §11 items 2–4 before starting any task.

**Roadmap after this plan:**
- Plan 3 is the desktop app: Avalonia, tray, hidden message window, single instance, hotkey, tabs, themes, i18n, balloon notifications. It is also where start-with-Windows and the update checker get wired into the real exe.
- Plan 4 is distribution.

## Global Constraints

- Everything in Plan 1's Global Constraints still applies. In particular:
  - English code and commits; Conventional Commits with no AI attribution trailer;
  - warnings as errors; `IsAotCompatible` on every `src/` project; `[LibraryImport]` only;
  - central package versions; `Method_Condition_ExpectedResult` test names;
  - all power calls on the engine thread.
- The test runner is Microsoft.Testing.Platform. Filter syntax is `--filter-not-trait "Category=Win32"`, not VSTest `--filter`. Run one project with `dotnet test --project <path>`.
- JSON goes **only** through `System.Text.Json` source-generated contexts (`JsonSerializerContext`). Every serialize and deserialize call passes a `JsonTypeInfo`; never use the reflection overloads.
- No new NuGet packages. `System.Text.Json`, `HttpClient` and `Microsoft.Win32.Registry` are in the shared framework for `net10.0-windows`.
- `GapMouse.Application` must not reference `System.Net.Http`, `System.Text.Json` or `Microsoft.Win32`; Task 1 adds an architecture test for this. Using `System.IO.Path` for path math is fine; file and registry access is not.
- Files on disk are UTF-8 **without BOM**.
- File names, exact:
  - settings `GapMouse.settings.json`;
  - state `GapMouse.state.json`;
  - portable marker `portable` (no extension);
  - daily log `gapmouse-yyyy-MM-dd.log`;
  - corrupt-settings backup `GapMouse.settings.json.bad`;
  - temp file `<target>.tmp`.
- Data locations (spec §6):
  - Portable when `<exe dir>\portable` or `<exe dir>\GapMouse.settings.json` exists. Config and state go in `<exe dir>`, logs in `<exe dir>\logs`.
  - Otherwise config and state go in `%AppData%\GapMouse` and logs in `%LocalAppData%\GapMouse\logs`.
- App setting defaults (spec §5.6, §11.2):

  | Setting | Default |
  |---|---|
  | engine | `EngineSettings.Default` |
  | start with Windows | **false** |
  | start minimized | **false** |
  | notifications | **false** |
  | pause hotkey | **`"Ctrl+Alt+P"`** |
  | check for updates | **true** |
  | theme | **Dark** |
  | language | **Auto** (follows Windows, fallback en) |
  | save logs to file | **true** |
  | log retention | **7 days**, valid range 1–365 inclusive |
  | tray hint shown | **false** |

- Log line format (spec §5.6): `HH:mm:ss | KIND | message`, with `InvariantCulture`. The kinds are START, WAITING, MOVING, RETURN, PAUSED, RESUMED, CONFIG, STOP, WARN and ERROR. Messages on disk are English. Plan 3 localizes the on-screen version.
- Network (spec §2, §5.7):
  - The only call is `GET https://api.github.com/repos/Gustavo-Piccoli/GapMouse/releases/latest`.
  - Scheduled checks happen at start and then every 24 h, only while "check for updates" is on. A manual "check now" ignores the setting.
  - Timeout is **10 s**.
  - Never download or install anything.
- Run key (spec §6): `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value name `GapMouse`, data `"<full exe path>" --minimized`.
- Tests that touch the file system use a fresh temp directory per test. Tests that touch the registry use a throwaway key `HKCU\Software\GapMouse.Tests\<guid>` and delete it. Neither kind needs the `Win32` trait; both run in CI.

## Review Focus

1. **A hand-edited or corrupt settings file**, meaning invalid JSON, an out-of-range number or an unknown enum string.
   - Expected: the app still starts, uses defaults only for the broken parts, keeps a corrupt file as `.bad` and reports a warning. It never crashes, and it never silently overwrites the user's file without a backup.
   - Owner: Task 2.
2. **Power loss or a crash in the middle of a save.**
   - Expected: the previous settings or state file stays intact, because writes go to a temp file first and then replace the target. A leftover `.tmp` from a crash is never read and doesn't block the next save.
   - Owner: Task 2.
3. **Portable folder full of the user's own files**, such as a pendrive or the Downloads folder.
   - Expected: log retention deletes **only** files named `gapmouse-yyyy-MM-dd.log` older than N days, decided by the date in the name. It never touches any other file, and a read-only folder disables file logging instead of crashing the engine.
   - Owner: Task 3.
4. **The installer already set the Run key, or the portable exe was moved.**
   - Expected: on a first launch with no settings file, "start with Windows" starts as **on**, because the Run key exists. With the option on, a stale exe path in the key is rewritten. With the option off, the key is removed.
   - Owner: Task 5.
5. **No network, or GitHub answers 403 (rate limit), 404 (no releases yet), malformed JSON, or a non-github.com URL.**
   - Expected: the check reports a quiet failure or "no releases" with no exception, no notification and no retry storm. A `v`-prefixed or pre-release tag is compared correctly, and a URL we would later open in the browser must be `https://github.com/...`.
   - Owner: Task 6.

---

### Task 1: Shared contracts — `AppSettings`, `AppState`, `AppPaths`, store ports, test helpers

**Files:**
- Create: `src/GapMouse.Application/Settings/AppSettings.cs`, `AppTheme.cs`, `UiLanguage.cs`, `ISettingsStore.cs`, `SettingsLoadResult.cs`
- Create: `src/GapMouse.Application/State/AppState.cs`, `IAppStateStore.cs`
- Create: `src/GapMouse.Application/Paths/AppPaths.cs`, `AppPathsResolver.cs`
- Create: `tests/GapMouse.Application.Tests/Fakes/FakeEngineController.cs`
- Create: `tests/GapMouse.Testing/IO/TempDirectory.cs`
- Modify: `tests/GapMouse.Infrastructure.Tests/GapMouse.Infrastructure.Tests.csproj`: add a ProjectReference to `GapMouse.Testing` and a PackageReference to `Microsoft.Extensions.TimeProvider.Testing`.
- Modify: `tests/GapMouse.Architecture.Tests/LayeringTests.cs`
- Test: `tests/GapMouse.Application.Tests/Settings/AppSettingsTests.cs`, `tests/GapMouse.Application.Tests/State/AppStateTests.cs`, `tests/GapMouse.Application.Tests/Paths/AppPathsResolverTests.cs`

**Interfaces:**
- Consumes: `EngineSettings` (with `Default`), `DailyStats(DateOnly Day, TimeSpan MovingTime, int MovingActivations)` and `IEngineController` from Plan 1.
- Produces, all `public`:
  - `enum AppTheme { Dark, Light }` and `enum UiLanguage { Auto, Portuguese, English }`.
  - `sealed record AppSettings` with `init` properties:
    - `EngineSettings Engine`
    - `bool StartWithWindows`
    - `bool StartMinimized`
    - `bool NotificationsEnabled`
    - `string PauseHotkey`
    - `bool CheckForUpdates`
    - `AppTheme Theme`
    - `UiLanguage Language`
    - `bool SaveLogsToFile`
    - `int LogRetentionDays`
    - `bool TrayHintShown`

    It also has `static AppSettings Default`, `const int MinLogRetentionDays = 1` and `const int MaxLogRetentionDays = 365`. The `LogRetentionDays` init throws `ArgumentOutOfRangeException` outside 1–365. `PauseHotkey` is stored as-is; Plan 3 validates it. Its init throws `ArgumentException` when the value is null or whitespace.
  - `sealed record SettingsLoadResult(AppSettings Settings, bool FileExisted, IReadOnlyList<string> Warnings)`.
  - `interface ISettingsStore { SettingsLoadResult Load(); void Save(AppSettings settings); }`.
  - `sealed record AppState(DailyStats Today)` with `DailyStats? TodayOrNull(DateOnly today)`, which returns `Today` only when `Today.Day == today`.
  - `interface IAppStateStore { AppState? Load(); void Save(AppState state); }`. `Load` returns `null` when the file is missing or unreadable.
  - `sealed record AppPaths(string ConfigDirectory, string LogsDirectory, bool IsPortable)`, with:
    - `string SettingsFile` (= `ConfigDirectory\GapMouse.settings.json`);
    - `string StateFile` (= `ConfigDirectory\GapMouse.state.json`);
    - `static AppPaths Portable(string exeDirectory)`.
  - `static class AppPathsResolver` with `static AppPaths Resolve(string exeDirectory, string roamingAppData, string localAppData, Func<string, bool> fileExists)`.
  - Test helpers:
    - `FakeEngineController : IEngineController`. It has a settable `Snapshot`, `void Raise(EngineEvent e)` (invokes `EventRaised` synchronously), and records `Pause`, `Resume` and `ApplySettings` calls in public lists.
    - `TempDirectory : IDisposable` (`string Path`, created under `System.IO.Path.GetTempPath()`, deleted recursively on Dispose, best effort).

- [ ] **Step 1: Write the failing tests.**
  - `AppSettingsTests`:
    - `Default_MatchesSpecDefaults`: assert every default listed in Global Constraints, including `Engine == EngineSettings.Default`.
    - `LogRetentionDays_OutsideRange_Throws`: `[Theory]` over 0, 366 and -1; `Should.Throw<ArgumentOutOfRangeException>(() => AppSettings.Default with { LogRetentionDays = v })`.
    - `LogRetentionDays_AtBounds_Accepted`: 1 and 365.
    - `PauseHotkey_Blank_Throws`.
  - `AppStateTests`:
    - `TodayOrNull_SameDay_ReturnsStats`.
    - `TodayOrNull_OtherDay_ReturnsNull`. Stats from 2026-10-30 asked for 2026-10-31 give `null`.
  - `AppPathsResolverTests`, with `exe = @"C:\Apps\GapMouse"`, `roaming = @"C:\Users\u\AppData\Roaming"` and `local = @"C:\Users\u\AppData\Local"`:
    - `Resolve_PortableMarkerPresent_UsesExeFolder`: `fileExists` is true only for `C:\Apps\GapMouse\portable`. Expect config `C:\Apps\GapMouse`, logs `C:\Apps\GapMouse\logs`, `IsPortable` true.
    - `Resolve_SettingsBesideExe_UsesExeFolder`: same expectations, with only `C:\Apps\GapMouse\GapMouse.settings.json` existing.
    - `Resolve_NoMarker_UsesProfileFolders`: expect config `C:\Users\u\AppData\Roaming\GapMouse`, logs `C:\Users\u\AppData\Local\GapMouse\logs`, `IsPortable` false.
    - `SettingsFileAndStateFile_AreInConfigDirectory`.
  - `LayeringTests` gets the new test `Application_DoesNotDependOnIoLibraries`: `Types.InAssembly(ApplicationAssembly.Reference).ShouldNot().HaveDependencyOnAny("System.Net.Http", "System.Text.Json", "Microsoft.Win32")` is successful.

- [ ] **Step 2: Run `dotnet test --project tests/GapMouse.Application.Tests`.** Expected: the build fails because the types are not defined.

- [ ] **Step 3: Implement the types listed in Interfaces.**
  - `AppSettings` uses plain `init` properties (not `required`) with backing fields, so the validation runs inside `init` and `with` expressions are validated too.
  - `Default` is built from literal values.
  - Add the helpers and csproj changes too.

- [ ] **Step 4: Run `dotnet test`.** Expected: all tests pass, including the existing 87, with 0 warnings.

- [ ] **Step 5: Commit.** Message: `feat(application): add app settings, state and data path contracts`.

---

### Task 2: JSON settings and state stores with atomic writes

**Files:**
- Create: `src/GapMouse.Infrastructure/IO/AtomicFile.cs`
- Create: `src/GapMouse.Infrastructure/Settings/SettingsDocument.cs` (the DTOs), `SettingsJsonContext.cs`, `JsonSettingsStore.cs`
- Create: `src/GapMouse.Infrastructure/State/StateDocument.cs`, `StateJsonContext.cs`, `JsonAppStateStore.cs`
- Test: `tests/GapMouse.Infrastructure.Tests/IO/AtomicFileTests.cs`, `Settings/JsonSettingsStoreTests.cs`, `State/JsonAppStateStoreTests.cs`

**Interfaces:**
- Consumes: Task 1 (`AppSettings`, `AppTheme`, `UiLanguage`, `ISettingsStore`, `SettingsLoadResult`, `AppState`, `IAppStateStore`, `TempDirectory`), plus `EngineSettings.Create(...)` and `DailyStats`.
- Produces:
  - `internal static class AtomicFile { static void WriteAllText(string path, string contents); }`.
  - `public sealed class JsonSettingsStore(string settingsFilePath) : ISettingsStore`.
  - `public sealed class JsonAppStateStore(string stateFilePath) : IAppStateStore`.

**Disk format (decided here):**
- Use camelCase, indented output, and `"schemaVersion": 1`.
- Settings have the sections `engine`, `system`, `appearance`, `logs` and `ui`.

  | Section | Fields |
  |---|---|
  | `engine` | `idleThresholdSeconds`, `checkIntervalSeconds`, `movementIntervalMinSeconds`, `movementIntervalMaxSeconds`, `amplitudePixels`, `keepDisplayOn` |
  | `system` | `startWithWindows`, `startMinimized`, `notifications`, `pauseHotkey`, `checkForUpdates` |
  | `appearance` | `theme`, `language` (strings, e.g. `"Dark"`, `"Auto"`) |
  | `logs` | `saveToFile`, `retentionDays` |
  | `ui` | `trayHintShown` |

- State is `{"schemaVersion":1,"today":{"day":"2026-10-31","movingSeconds":1234.5,"movingActivations":3}}`.
- Every DTO member is nullable, so a missing field means "use the default".
- Enums are DTO **strings**, parsed with `Enum.TryParse(ignoreCase: true)`. An unknown enum value only resets that one field; it doesn't make the whole file unreadable.
- Reader options: `ReadCommentHandling = Skip` and `AllowTrailingCommas = true`, to be friendly to hand edits.

- [ ] **Step 1: Write the failing tests**, each using a `TempDirectory`.
  - `AtomicFileTests`:
    - `WriteAllText_CreatesMissingDirectory`.
    - `WriteAllText_ReplacesExistingContent`.
    - `WriteAllText_StaleTmpFromCrash_IsOverwritten`: pre-create `<target>.tmp` with garbage; after the write, the target has the new text and no `.tmp` remains.
    - `WriteAllText_WritesUtf8WithoutBom`: the first bytes are not `EF BB BF`.
  - `JsonSettingsStoreTests`:
    - `Load_MissingFile_ReturnsDefaultsAndFileExistedFalse`: `Warnings` is empty.
    - `SaveThenLoad_RoundTripsEveryField`: use a non-default value for every property.
    - `Load_InvalidJson_ReturnsDefaultsKeepsBadCopyAndWarns`:
      - the file contains `{ not json`;
      - expect `Settings == AppSettings.Default`, `FileExisted` true and one warning;
      - `GapMouse.settings.json.bad` exists with the original bytes, and the original path no longer exists.
    - `Load_InvalidEngineSection_UsesEngineDefaultsKeepsOtherFields`: `movementIntervalMinSeconds` 30 and max 10, with theme `"Light"`. Expect `Engine == EngineSettings.Default`, `Theme == Light` and one warning. No `.bad` file is created.
    - `Load_UnknownTheme_ResetsOnlyTheme`: `"theme": "Purple"` gives `Dark` and one warning, and the other fields are kept.
    - `Load_RetentionOutOfRange_UsesSeven`: 0 gives 7 and one warning.
    - `Load_MissingSections_UsesDefaultsWithoutWarnings`: the file is `{"schemaVersion":1}`.
    - `Load_CommentsAndTrailingCommas_Accepted`.
    - `Load_NeverReadsTmpFile`: a valid target plus a garbage `.tmp` loads the target.
  - `JsonAppStateStoreTests`:
    - `Load_MissingFile_ReturnsNull`.
    - `SaveThenLoad_RoundTrips`: `DailyStats(2026-10-31, 20m34.5s, 3)`.
    - `Load_Corrupt_ReturnsNull`: no exception and no `.bad` file.

- [ ] **Step 2: Run `dotnet test --project tests/GapMouse.Infrastructure.Tests`.** Expected: the build fails.

- [ ] **Step 3: Implement.**
  - `AtomicFile.WriteAllText`:
    1. Create the directory.
    2. Write `path + ".tmp"` through a `FileStream` (`FileMode.Create`) with `new UTF8Encoding(false)`.
    3. Call `Flush(flushToDisk: true)`.
    4. Call `File.Move(tmp, path, overwrite: true)`.
  - Stores:
    - Catch `JsonException` for corrupt input. Settings move the file to `.bad` with `File.Move(..., overwrite: true)`; state returns `null`.
    - Build the engine part through `EngineSettings.Create`.
    - Warnings are short English sentences that name the field, for example `"Settings: theme 'Purple' is not valid; using Dark."`.
    - Both source-gen contexts are `internal partial` with `[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]`.

- [ ] **Step 4: Run the project tests, then the full `dotnet test`.** Expected: all pass, with 0 warnings.

- [ ] **Step 5: Commit.** Message: `feat(infrastructure): add JSON settings and state stores with atomic writes`.

---

### Task 3: Activity log — entries, engine-event formatting, warning capture, daily files with retention

**Files:**
- Create: `src/GapMouse.Application/Logging/LogKind.cs`, `LogEntry.cs`, `LogEntryFormatter.cs`, `IActivityLog.cs`, `ActivityLogWriter.cs`, `ActivityLogger.cs`
- Create: `src/GapMouse.Infrastructure/Logging/FileActivityLog.cs`
- Test: `tests/GapMouse.Application.Tests/Logging/LogEntryFormatterTests.cs`, `ActivityLogWriterTests.cs`, `ActivityLoggerTests.cs`
- Test: `tests/GapMouse.Infrastructure.Tests/Logging/FileActivityLogTests.cs`

**Interfaces:**
- Consumes: the Plan 1 engine events (`EngineStarted`, `WaitingStarted(IdleThreshold)`, `MovingStarted(IntervalMin, IntervalMax)`, `UserReturned`, `EnginePaused(Duration, ResumesAt)`, `EngineResumed(Reason)`, `SettingsChanged`, `EngineStopped`), `IEngineController.EventRaised`, and Task 1's `FakeEngineController` and `TempDirectory`.
- Produces:
  - `public enum LogKind { Start, Waiting, Moving, Return, Paused, Resumed, Config, Stop, Warn, Error }`.
  - `public sealed record LogEntry(DateTimeOffset At, LogKind Kind, string Message)`, with `string Tag` (`"START"` through `"ERROR"`) and `string ToLine()`, which returns `HH:mm:ss | TAG | message`, using `At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)`. Engine events already carry local time.
  - `public static class LogEntryFormatter { static LogEntry FromEvent(EngineEvent e); }`.
  - `public interface IActivityLog { void Append(LogEntry entry); }`.
  - `public sealed class ActivityLogWriter(IEngineController engine, IActivityLog log) : IDisposable`. The constructor subscribes, and `Dispose` unsubscribes.
  - `public sealed class ActivityLogger<T>(IActivityLog log, TimeProvider time, ILogger<T>? inner = null) : ILogger<T>`. It forwards **every** call to `inner`. It also appends Warning as `Warn`, and Error or Critical as `Error`. The message is `formatter(state, exception)`, plus `": " + exception.Message` when there is an exception.
  - `public sealed class FileActivityLog(string logsDirectory, TimeProvider time) : IActivityLog`, with `void Configure(bool enabled, int retentionDays)`. The default before `Configure` is enabled with 7 days.

**Exact messages** (seconds use format `0.##`, InvariantCulture):

| Event | Kind | Message |
|---|---|---|
| EngineStarted | Start | `GapMouse started` |
| WaitingStarted | Waiting | `Waiting for {idle} s of inactivity` |
| MovingStarted | Moving | `Moving the mouse every {min}–{max} s` |
| UserReturned | Return | `User activity detected` |
| EnginePaused, timed | Paused | `Paused until {HH:mm:ss}` |
| EnginePaused, indefinite | Paused | `Paused indefinitely` |
| EngineResumed | Resumed | `Resumed (manual)` / `Resumed (pause expired)` |
| SettingsChanged | Config | `Settings changed` |
| EngineStopped | Stop | `GapMouse stopped` |

- [ ] **Step 1: Write the failing tests.**
  - `LogEntryFormatterTests`: one `[Fact]` per row of the table, each asserting `Kind` and the exact `Message`. Add:
    - `ToLine_UsesPipeFormat`: an entry at 09:05:03 of kind Moving gives `"09:05:03 | MOVING | x"`.
    - `FromEvent_UnknownEventType_Throws` (`ArgumentOutOfRangeException`). Use a private `sealed record TestEvent(DateTimeOffset At) : EngineEvent(At)` declared in the test.
  - `ActivityLogWriterTests`:
    - `EngineEvent_AppendsFormattedEntry`: a recording `IActivityLog` fake, local to the test file, receives a `UserReturned` raised on `FakeEngineController` as a `Return` entry.
    - `Dispose_Unsubscribes`.
  - `ActivityLoggerTests`:
    - `LogWarning_AppendsWarnEntry`.
    - `LogError_WithException_AppendsErrorWithExceptionMessage`.
    - `LogInformation_DoesNotAppend`.
    - `AnyLevel_ForwardsToInner`.
  - `FileActivityLogTests`, using a `FakeTimeProvider` set to 2026-10-31 12:00 local and a `TempDirectory`:
    - `Append_WritesLineToDailyFile`: the file `gapmouse-2026-10-31.log` contains `entry.ToLine()` plus a newline. Appending twice gives two lines.
    - `Append_EntryOnNextDay_GoesToNewFile`.
    - `Configure_Disabled_WritesNothing`.
    - `Retention_DeletesOnlyOldGapMouseLogs`:
      - pre-create `gapmouse-2026-10-20.log` (old), `gapmouse-2026-10-25.log` (inside 7 days), `notes.txt`, `gapmouse-backup.log` and `other-2026-01-01.log`;
      - call `Configure(true, 7)`;
      - only `gapmouse-2026-10-20.log` is gone.
      - The rule: delete when the name date is earlier than `today − retentionDays`.
    - `Retention_RunsAgainOnFirstAppendOfNewDay`.
    - `Append_DirectoryNotWritable_DoesNotThrow`: point the log directory at a path whose parent is a *file*, so directory creation fails. `Append` returns normally.

- [ ] **Step 2: Run both test projects.** Expected: the build fails.

- [ ] **Step 3: Implement.**
  - `FileActivityLog` serializes writes with a `lock`.
  - Each `Append` opens the file, writes and closes it (`File.AppendAllText` with UTF-8 without BOM), because events are infrequent and other tools can then read the file live.
  - The file date is the local date of `entry.At`.
  - Retention runs in `Configure` and on the first append of each new local day (per `time.GetLocalNow()`). It only deletes names matching `^gapmouse-(\d{4}-\d{2}-\d{2})\.log$` (use `[GeneratedRegex]`, which is AOT-safe).
  - Catch `IOException` and `UnauthorizedAccessException` in `Append` and in retention, and drop them. Logging the failure would recurse; the ruling is that file logging is best effort.

- [ ] **Step 4: Run both test projects, then the full `dotnet test`.** Expected: all pass.

- [ ] **Step 5: Commit.** Message: `feat: add activity log with engine event formatting and daily files with retention`.

---

### Task 4: Daily-stats persistence across restarts

**Files:**
- Create: `src/GapMouse.Application/State/AppStatePersister.cs`
- Test: `tests/GapMouse.Application.Tests/State/AppStatePersisterTests.cs`

**Interfaces:**
- Consumes: Task 1 (`AppState`, `IAppStateStore`, `FakeEngineController`), plus `EngineSnapshot.Today`, `EngineSnapshot.State`, and the events `UserReturned`, `EnginePaused` and `EngineStopped`.
- Produces: `public sealed class AppStatePersister(IEngineController engine, IAppStateStore store, TimeProvider time, ILogger<AppStatePersister> logger) : IDisposable`. It has `void Start()`. `Dispose()` unsubscribes, stops the timer, and saves one last time. It is idempotent.

**Behavior** (spec §5.6 "Hoje" and §11.4): `new AppState(engine.Snapshot.Today)` is saved:
- when `UserReturned`, `EnginePaused` or `EngineStopped` is raised;
- every **60 s** while `Snapshot.State == EngineState.Moving` (the timer comes from `time.CreateTimer`, period 60 s);
- once on `Dispose`.

Store failures (`IOException`, `UnauthorizedAccessException`) are logged as a Warning, once per failure streak, and never thrown. Restoring is the composer's job: `store.Load()?.TodayOrNull(today)` is passed as `restoredStats` to `EngineHost`.

- [ ] **Step 1: Write the failing tests.** Use a recording fake store local to the test file, a `FakeTimeProvider` and a `FakeEngineController`.
  - `UserReturned_SavesCurrentStats`.
  - `Paused_SavesCurrentStats`.
  - `WhileMoving_SavesEverySixtySeconds`: State is Moving; advance 59 s for 0 saves, then 1 s for 1 save, then 60 s for 2 saves.
  - `WhileWaiting_TimerDoesNotSave`.
  - `Dispose_SavesOnceAndStopsSaving`: after Dispose, raising events or advancing time adds no saves. A second Dispose adds none either.
  - `StoreThrows_LogsOneWarningPerStreak`: the store throws `IOException` on three saves, then succeeds, then throws again. Expect 2 warnings.

- [ ] **Step 2: Run `dotnet test --project tests/GapMouse.Application.Tests`.** Expected: the build fails.

- [ ] **Step 3: Implement `AppStatePersister`.** Event handlers run on the engine thread and the timer runs on the thread pool, so guard the store calls and the failure flag with a `lock`.

- [ ] **Step 4: Run the tests.** Expected: they pass.

- [ ] **Step 5: Commit.** Message: `feat(application): persist daily stats across restarts`.

---

### Task 5: Start with Windows

**Files:**
- Create: `src/GapMouse.Application/Startup/IStartupRegistrar.cs`, `StartupCommand.cs`, `StartWithWindowsService.cs`
- Create: `src/GapMouse.Infrastructure/Startup/RegistryStartupRegistrar.cs`
- Test: `tests/GapMouse.Application.Tests/Startup/StartWithWindowsServiceTests.cs`, `tests/GapMouse.Infrastructure.Tests/Startup/RegistryStartupRegistrarTests.cs`

**Interfaces:**
- Consumes: Task 1 `SettingsLoadResult.FileExisted` (passed in by the composer as a `bool`).
- Produces:
  - `public interface IStartupRegistrar { string? GetCommand(); void SetCommand(string command); void Remove(); }`.
  - `public static class StartupCommand { static string For(string exePath); }`, which returns `"\"" + exePath + "\" --minimized"`.
  - `public sealed class StartWithWindowsService(IStartupRegistrar registrar, string exePath, ILogger<StartWithWindowsService> logger)`, with:
    - `bool InitialPreference(bool settingsFileExisted, bool savedPreference)`: returns `savedPreference` when the file existed; otherwise whether `GetCommand()` is not null.
    - `bool Apply(bool enabled)`: when enabled, calls `SetCommand(StartupCommand.For(exePath))` unless the current command already equals it (case-insensitive ordinal). When disabled, calls `Remove()` if a command exists. It returns `false` and logs a Warning when the registrar throws `UnauthorizedAccessException`, `IOException` or `System.Security.SecurityException`.
  - `public sealed class RegistryStartupRegistrar(string subKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run", string valueName = "GapMouse") : IStartupRegistrar`. It works under `Registry.CurrentUser` and stores a `REG_SZ` value. `Remove` is a no-op when the value is absent.

- [ ] **Step 1: Write the failing tests.**
  - `StartWithWindowsServiceTests` (in-memory fake registrar, `exe = @"C:\Apps\GapMouse\GapMouse.exe"`):
    - `For_QuotesPathAndAddsMinimized`: gives `"\"C:\\Apps\\GapMouse\\GapMouse.exe\" --minimized"`.
    - `InitialPreference_NoSettingsFileAndRunKeyPresent_ReturnsTrue`. This is the case where the installer ticked the box.
    - `InitialPreference_NoSettingsFileAndNoRunKey_ReturnsFalse`.
    - `InitialPreference_SettingsFileExists_ReturnsSavedValue`: saved `false` with the key present still gives `false`.
    - `Apply_EnabledAndMissing_WritesCommand`.
    - `Apply_EnabledAndStalePath_RewritesCommand`. This is the case of a moved portable exe.
    - `Apply_EnabledAndCurrent_DoesNotWrite`: the fake counts writes.
    - `Apply_DisabledAndPresent_Removes`.
    - `Apply_DisabledAndAbsent_DoesNothing`.
    - `Apply_RegistrarThrowsUnauthorized_ReturnsFalseAndLogsWarning`.
  - `RegistryStartupRegistrarTests`: uses key `Software\GapMouse.Tests\<guid>` and deletes the subtree in `Dispose`.
    - `SetThenGet_RoundTrips`.
    - `Remove_DeletesValue`.
    - `Remove_WhenAbsent_DoesNotThrow`.
    - `Get_WhenKeyMissing_ReturnsNull`.

- [ ] **Step 2: Run both projects.** Expected: the build fails.

- [ ] **Step 3: Implement as specified in Interfaces.** `Registry.CurrentUser.CreateSubKey` is used in `SetCommand`; `OpenSubKey(writable: true)` is used in `Remove`.

- [ ] **Step 4: Run both projects, then the full `dotnet test`.** Expected: all pass.

- [ ] **Step 5: Commit.** Message: `feat: add start with Windows through the HKCU Run key`.

---

### Task 6: Update checker (GitHub Releases)

**Files:**
- Create: `src/GapMouse.Application/Updates/ReleaseInfo.cs`, `IReleaseSource.cs`, `ReleaseVersion.cs`, `UpdateCheckResult.cs`, `UpdateService.cs`
- Create: `src/GapMouse.Infrastructure/Updates/GitHubReleaseSource.cs`, `GitHubReleaseJsonContext.cs`
- Test: `tests/GapMouse.Application.Tests/Updates/ReleaseVersionTests.cs`, `UpdateServiceTests.cs`, `tests/GapMouse.Infrastructure.Tests/Updates/GitHubReleaseSourceTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record ReleaseInfo(string TagName, Uri PageUrl)`.
  - `public interface IReleaseSource { Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken); }`. It returns `null` when the repository has no release (HTTP 404). It throws `HttpRequestException` for any other non-success status, and `JsonException` or `InvalidDataException` for unusable payloads.
  - `public static class ReleaseVersion`:
    - `static bool TryParse(string text, [NotNullWhen(true)] out Version? version)` trims an optional leading `v`/`V` and drops everything from the first `-` or `+`. It requires 2–3 numeric parts and normalizes to `major.minor.build`, with a missing build counting as 0.
    - `static Version Normalize(Version v)`.
  - `public enum UpdateCheckOutcome { UpdateAvailable, UpToDate, NoReleases, Failed }` and `public sealed record UpdateCheckResult(UpdateCheckOutcome Outcome, Version? LatestVersion, Uri? ReleaseUrl)`.
  - `public sealed class UpdateService(IReleaseSource source, Version currentVersion, TimeProvider time, ILogger<UpdateService> logger) : IDisposable`, with:
    - `Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken)`: times out after **10 s** via `new CancellationTokenSource(TimeSpan.FromSeconds(10), time)` linked with the caller's token. A caller cancellation rethrows `OperationCanceledException`. A timeout, `HttpRequestException`, `JsonException`, `InvalidDataException` or an unparsable tag gives `Failed`, logged at **Information** so it is not written to the activity file.
    - `void Start(Func<bool> isEnabled)`: a `time.CreateTimer` with due time 0 and a 24 h period. Each tick calls `isEnabled()` and does nothing (no network) when it returns false. Ticks never overlap; a tick is skipped while a check is running.
    - `event EventHandler<UpdateCheckResult>? UpdateFound`: raised only for `UpdateAvailable`.
  - `public sealed class GitHubReleaseSource(HttpClient http, string productVersion) : IReleaseSource`. Each request is a `GET` to the URL in Global Constraints with these headers:
    - `User-Agent: GapMouse/{productVersion}`
    - `Accept: application/vnd.github+json`
    - `X-GitHub-Api-Version: 2022-11-28`

    It reads `tag_name` and `html_url` via `GitHubReleaseJsonContext`. It throws `InvalidDataException` when either is missing, or when `html_url` is not `https` with host `github.com`.

- [ ] **Step 1: Write the failing tests.**
  - `ReleaseVersionTests` (`[Theory]`):
    - `"v1.2.3"`, `"1.2.3"`, `"V1.2"` (normalizes to 1.2.0), `"v1.2.3-beta.1"` (1.2.3) and `"1.2.3+abc"` parse.
    - `""`, `"latest"`, `"v1"` and `"1.2.3.4.5"` don't parse.
    - `Normalize_TwoPartVersion_EqualsThreePart`.
  - `UpdateServiceTests`, with a fake `IReleaseSource` and current version 1.0.0:
    - `CheckNow_NewerTag_ReturnsUpdateAvailableWithUrl`: `v1.1.0`.
    - `CheckNow_SameTag_ReturnsUpToDate`.
    - `CheckNow_OlderTag_ReturnsUpToDate`.
    - `CheckNow_NullRelease_ReturnsNoReleases`.
    - `CheckNow_SourceThrowsHttpRequest_ReturnsFailed`.
    - `CheckNow_SourceThrowsJson_ReturnsFailed`.
    - `CheckNow_UnparsableTag_ReturnsFailed`.
    - `CheckNow_SourceHangs_TimesOutAfterTenSeconds`: the fake awaits `Task.Delay(Timeout.Infinite, ct)`. Advance the fake clock 10 s, and the result is `Failed`.
    - `CheckNow_CallerCancels_Throws`.
    - `Start_Enabled_ChecksImmediatelyAndEvery24Hours`: 1 call at start, 2 after advancing 24 h.
    - `Start_Disabled_NeverCallsSource`.
    - `Start_DisabledThenEnabled_ChecksOnNextTick`.
    - `UpdateFound_RaisedOnlyForNewerVersion`.
  - `GitHubReleaseSourceTests`, with a stub `HttpMessageHandler` local to the test file that records the request and returns a canned response:
    - `GetLatest_Ok_ParsesTagAndUrl`.
    - `GetLatest_SendsRequiredHeaders`: the UA is `GapMouse/1.0.0`, plus the Accept and API-version headers, with the exact URL.
    - `GetLatest_NotFound_ReturnsNull`.
    - `GetLatest_RateLimited403_ThrowsHttpRequestException`.
    - `GetLatest_MalformedJson_ThrowsJsonException`.
    - `GetLatest_MissingTag_ThrowsInvalidData`.
    - `GetLatest_NonGitHubUrl_ThrowsInvalidData`: `html_url` is `https://evil.example/x`.
    - `GetLatest_HttpUrl_ThrowsInvalidData`: `http://github.com/...`.

- [ ] **Step 2: Run both projects.** Expected: the build fails.

- [ ] **Step 3: Implement as specified in Interfaces.**
  - The timer callback starts `CheckScheduledAsync()` and observes its task; an unobserved exception would crash the process. Use an `Interlocked` flag for the no-overlap rule.
  - `Dispose` disposes the timer.

- [ ] **Step 4: Run both projects, then the full `dotnet test`.** Expected: all pass.

- [ ] **Step 5: Commit.** Message: `feat: add opt-out GitHub release update checker`.

---

### Task 7: Dev console composition, docs and AOT gate

**Files:**
- Modify: `tools/GapMouse.DevConsole/Program.cs`, `ConsoleOptions.cs`, `ConsoleEventPrinter.cs`
- Create: `docs/adr/0005-settings-state-and-logs-on-disk.md`
- Modify: `README.md`, `CLAUDE.md`

**Interfaces:**
- Consumes: everything from Tasks 1–6.
- Changes: `ConsoleOptions.Parse(string[] args, EngineSettings baseline, out IReadOnlyList<string> usageErrors)`. It parses flags over `baseline` instead of `EngineSettings.Default`, so flags override the saved settings for this run only and are **not** saved. `ConsoleEventPrinter.Print(LogEntry entry)` takes a `LogEntry` instead of an event.

- [ ] **Step 1: Compose `Program.cs`** in this order:
  1. Set `paths = AppPaths.Portable(AppContext.BaseDirectory)`. The dev console always keeps its data next to its own exe, so it never touches the real user profile.
  2. Load settings with `new JsonSettingsStore(paths.SettingsFile).Load()`, and print each warning as a yellow `WARN` line.
  3. Parse the flags over `loaded.Settings.Engine`; keep exit code 2 for bad flags.
  4. Create `fileLog = new FileActivityLog(paths.LogsDirectory, TimeProvider.System)` and call `Configure(settings.SaveLogsToFile, settings.LogRetentionDays)`.
  5. Restore stats with `new JsonAppStateStore(paths.StateFile).Load()?.TodayOrNull(today)` and pass the result to `EngineHost` as `restoredStats`.
  6. Use `new ActivityLogger<EngineHost>(fileLog, TimeProvider.System, new ConsoleLogger<EngineHost>())` as the host logger.
  7. Create `ActivityLogWriter(host, fileLog)` and `AppStatePersister(...)`, and call `.Start()`.
  8. For the console, map `host.EventRaised` to `ConsoleEventPrinter.Print(LogEntryFormatter.FromEvent(e.Event))`. Print the key hint line (`p`/`t`/`r`/`q`) once in gray after START.
  9. If `settings.CheckForUpdates` is on, call `UpdateService.CheckNowAsync` once. Use `new HttpClient()` and `productVersion` = the entry assembly's `InformationalVersion` (fall back to `"0.0.0"`). Print one `[UPDATE]` line with the outcome (and the URL when available). Don't let it block startup.
  10. Dispose in this order: host, persister, writer, update service, HttpClient.

  Colors per `LogKind`:

  | Kind | Color |
  |---|---|
  | Start | Yellow |
  | Waiting | Cyan |
  | Moving | Green |
  | Return | Gray |
  | Paused, Resumed | Magenta |
  | Config | DarkGray |
  | Stop | Red |
  | Warn | Yellow |
  | Error | Red |

  Do **not** wire `StartWithWindowsService` here, because it would register the dev tool to start with Windows.

- [ ] **Step 2: Verify locally.**
  - Run `dotnet build -c Release`. Expected: 0 warnings.
  - Run `dotnet test --filter-not-trait "Category=Win32"`. Expected: all pass.
  - Run `dotnet format --verify-no-changes`. Expected: no changes.
  - Run `dotnet publish tools/GapMouse.DevConsole -c Release -r win-x64 -o artifacts/devconsole`. Expected: succeeds with **0 AOT/trim warnings**; record the exe size in the report.
  - Smoke-run the published exe with `echo q | artifacts\devconsole\GapMouse.DevConsole.exe --idle 10`. Expected:
    - exit code 0;
    - `artifacts\devconsole\logs\gapmouse-<today>.log` contains START and STOP lines in the pipe format;
    - `artifacts\devconsole\GapMouse.state.json` exists.

- [ ] **Step 3: Docs.**
  - ADR 0005 (Context / Decision / Consequences, ≤ 40 lines) covers:
    - the portable detection rule;
    - JSON via source generation;
    - temp-file-then-replace writes;
    - the corrupt-file policy (`.bad` backup and per-field defaults);
    - log retention by the date in the file name, restricted to the `gapmouse-*.log` pattern;
    - `AppSettings` living in Application.
  - Add a README section "Your data" listing:
    - the file locations for installed and portable mode, with the `portable` marker;
    - what is stored;
    - that logs are kept 7 days by default;
    - the only network call (the endpoint) and how to turn it off.
  - Update `CLAUDE.md` (Portuguese, with accents):
    - the current state becomes Plan 2;
    - list the new folders (`Settings/`, `State/`, `Paths/`, `Logging/`, `Startup/`, `Updates/`) and ports;
    - add the dev console's data location;
    - add ADR 0005 to the list;
    - add the non-obvious points: Application does not do I/O (architecture test); flags don't persist; the dev console never touches the Run key.

- [ ] **Step 4: Commit.** Message: `feat(tools): wire persistence, file logs and update check into the dev console`. Put the docs in the same commit, or in a second `docs:` commit.

---

## Execution notes

- Branch: `feat/persistence-integration` from `main`, delivered through a PR. `main` is protected and needs `build-test` and `aot-publish`.
- Parallelism: after Task 1, Tasks 2–6 touch disjoint folders and can run in separate worktrees. Task 7 runs last. Tasks 3 and 4 both read `FakeEngineController`, which Task 1 owns, so neither of them modifies it.
- Every agent is dispatched with model **sonnet** (spec §3).
