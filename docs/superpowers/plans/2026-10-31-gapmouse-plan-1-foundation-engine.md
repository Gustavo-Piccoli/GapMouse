# GapMouse — Plan 1: Foundation & Engine — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the public `GapMouse` repository with CI, and deliver a fully tested, Native-AOT-compatible keep-awake engine (Domain + Application + Win32 Infrastructure) that runs headless through a dev console with behavior parity to `MOUSE.ps1`.

**Architecture:** Clean Architecture with a *functional core / imperative shell* engine. `KeepAwakeEngine` (Domain) is a pure, synchronous state machine: each `Advance(input)` call receives the current time, system idle time, cursor position and monitor bounds, and returns one `EngineStep` (where to move the cursor, which power request to hold, how long to wait). `EngineHost` (Application) is the thin shell that runs that loop on one dedicated thread and talks to Win32 through ports implemented in Infrastructure.

**Tech Stack:** .NET 10 (SDK 10.0.300), C#, `LibraryImport` P/Invoke, xUnit v3 + Shouldly, `FakeTimeProvider`, NetArchTest, coverlet + ReportGenerator, GitHub Actions, CodeQL, Dependabot.

**Spec:** `docs/superpowers/specs/2026-10-31-gapmouse-design.md` (Portuguese). Read §5.1, §5.2, §5.3, §5.4, §7, §8, §9 and §11 items 3–6 before starting any task.

**Roadmap (later plans, written after this one ships):** Plan 2 — persistence & system integration (settings JSON, portable mode, file logs with retention, start with Windows, daily-stats persistence, update checker). Plan 3 — desktop app (Avalonia, tray, hidden message window, single instance, hotkey, tabs, themes, i18n, notifications). Plan 4 — distribution (release-please, CD for x64/ARM64, Inno Setup, portable zip, checksums, final README with GIF).

## Global Constraints

- Code, identifiers, comments, commit messages, README and ADRs in **English**.
- Commits follow **Conventional Commits** (`feat:`, `fix:`, `test:`, `chore:`, `ci:`, `docs:`, `refactor:`). No `Co-Authored-By` or other AI attribution lines (Gustavo's standing commit preference).
- Target frameworks: `net10.0` for `GapMouse.Domain` and `GapMouse.Application`; `net10.0-windows` for `GapMouse.Infrastructure`, its tests and `tools/GapMouse.DevConsole`.
- Every `src/` project sets `<IsAotCompatible>true</IsAotCompatible>`. No reflection-based APIs in `src/`. P/Invoke **only** through `[LibraryImport]` (never `[DllImport]`).
- `TreatWarningsAsErrors=true`, `Nullable=enable`, `ImplicitUsings=enable`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, all set in `Directory.Build.props`.
- Package versions live only in `Directory.Packages.props` (central package management). Pinned versions: `xunit.v3` 4.0.2, `xunit.runner.visualstudio` 4.0.1, `Microsoft.NET.Test.Sdk` 18.11.0, `Shouldly` 4.3.0, `Microsoft.Extensions.TimeProvider.Testing` 10.11.0, `NetArchTest.Rules` 1.3.2, `coverlet.collector` 10.1.0, `Microsoft.Extensions.Logging.Abstractions` 10.0.14. If one of them fails to restore, use the newest stable version with the same major, and note it in the commit body.
- Do **not** use FluentAssertions (license change in v8).
- Engine defaults (from spec §5.6): idle threshold **120 s**, check interval **1 s**, movement interval **10–25 s**, amplitude **120 px**, keep display on **true**.
- Settings ranges (plan decision; all inclusive): idle threshold 10–3600 s; check interval 1–60 s; movement interval min and max 1–3600 s with min ≤ max; amplitude 10–500 px. Custom pause: 1–1440 minutes.
- All random ranges are **inclusive** on both ends (spec §11.6).
- `SetThreadExecutionState` is per-thread. Every call to `IPowerRequestService.Apply` must happen on the `EngineHost` engine thread, including the final release on shutdown.
- Executables (`tools/GapMouse.DevConsole`) declare **PerMonitorV2** DPI awareness in `app.manifest`, so cursor and monitor coordinates share the same physical-pixel space.
- Tests that touch real Win32 state carry `[Trait("Category", "Win32")]` and are excluded in CI (`--filter "Category!=Win32"`). They run locally.
- Test method names use `Method_Condition_ExpectedResult`. For `tests/**`, `.editorconfig` disables `CA1707` and `CA1515`.

## Review Focus

1. **Pause pressed in the middle of a movement.** The cursor must not move again after `Pause` returns, and the power request must be released on the next loop iteration. Owner: Task 5 (engine) and Task 6 (host).
2. **Monitors with negative coordinates**, such as a secondary monitor to the left of or above the primary. Clamping must keep every point inside that monitor; no point may be pushed to `0`. Owner: Task 2 (`ScreenBounds.Clamp`) and Task 3 (planner).
3. **`Environment.TickCount` wrap-around** after ~49.7 days of uptime. Idle time must stay small and positive across the wrap, never huge or negative. Owner: Task 7 (`IdleTime.FromTicks`).
4. **Locked workstation or disconnected RDP session.** `SetCursorPos` fails or throws there. The engine loop must keep running and keep the power request, and must neither crash nor busy-loop. Owner: Task 6.
5. **Settings changed during a movement.** The in-flight path and the current wait keep their old values; the new values apply from the next decision point (spec §5.6). Owner: Task 5.

---

### Task 1: Repository bootstrap, CI and layering guard-rails

**Files:**
- Move: `MOUSE.ps1` → `legacy/MOUSE.ps1`, `MOUSE.bat` → `legacy/MOUSE.bat`
- Modify: `legacy/MOUSE.bat` (line 5: make `PS1_PATH` relative, `set "PS1_PATH=%~dp0MOUSE.ps1"`, and drop the old commented line 4, so no personal path is published)
- Create: `legacy/README.md`, `.gitignore`, `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `GapMouse.slnx`, `LICENSE`, `README.md`
- Create: `src/GapMouse.Domain/GapMouse.Domain.csproj`, `src/GapMouse.Domain/DomainAssembly.cs`
- Create: `src/GapMouse.Application/GapMouse.Application.csproj`, `src/GapMouse.Application/ApplicationAssembly.cs`
- Create: `src/GapMouse.Infrastructure/GapMouse.Infrastructure.csproj`, `src/GapMouse.Infrastructure/InfrastructureAssembly.cs`
- Create: `tests/GapMouse.Domain.Tests/GapMouse.Domain.Tests.csproj`, `tests/GapMouse.Application.Tests/GapMouse.Application.Tests.csproj`, `tests/GapMouse.Architecture.Tests/GapMouse.Architecture.Tests.csproj`
- Test: `tests/GapMouse.Architecture.Tests/LayeringTests.cs`
- Create: `.github/workflows/ci.yml`, `.github/workflows/codeql.yml`, `.github/dependabot.yml`, `.config/dotnet-tools.json`
- Create: `docs/adr/0001-avalonia-with-native-aot.md`, `docs/adr/0002-cursor-movement-and-power-requests.md`, `docs/adr/0003-no-framework-dependent-lite-build.md`
- Keep: `docs/superpowers/specs/…`, `docs/superpowers/plans/…`, `CLAUDE.md`

**Interfaces:**
- Produces: `GapMouse.Domain.DomainAssembly.Reference`, `GapMouse.Application.ApplicationAssembly.Reference`, `GapMouse.Infrastructure.InfrastructureAssembly.Reference` (each `public static System.Reflection.Assembly Reference => typeof(X).Assembly;` in a `public static class`), used by the architecture tests.
- Project references: Application → Domain; Infrastructure → Application, Domain; each test project → the project it tests; Architecture.Tests → all three `src` projects.

- [ ] **Step 1: Initialize git and lay out the tree.** Run `git init -b main`. Move the legacy files and fix `legacy/MOUSE.bat` as listed. `legacy/README.md` should say, in 3–5 lines, that these are the original PowerShell scripts GapMouse was born from, kept for history. Generate `.gitignore` with `dotnet new gitignore`, then append `.superpowers/`, `TestResults/` and `coverage/`.

- [ ] **Step 2: Build configuration.**
  - `global.json`: `{"sdk": {"version": "10.0.300", "rollForward": "latestFeature"}}`.
  - `Directory.Build.props`: the global-constraint properties plus `<Deterministic>true</Deterministic>`, and `<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>` when `'$(GITHUB_ACTIONS)' == 'true'`.
  - `Directory.Packages.props`: `ManagePackageVersionsCentrally=true` with the pinned versions.
  - `.editorconfig`: standard C# conventions (file-scoped namespaces, `var` when apparent, 4-space indent), plus the `[tests/**.cs]` section disabling CA1707/CA1515.

- [ ] **Step 3: Create projects and solution.** Use `dotnet new classlib` for the three `src` projects and `dotnet new xunit` for the three test projects. Swap the test templates to `xunit.v3` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` + `Shouldly` + `coverlet.collector`, with versions coming from central management. Delete the template `Class1.cs`/`UnitTest1.cs` files and add the three `*Assembly.cs` markers. Create `GapMouse.slnx` (`dotnet new sln --format slnx`) and add every project.

- [ ] **Step 4: Write the failing architecture tests** in `tests/GapMouse.Architecture.Tests/LayeringTests.cs`:

```csharp
[Fact]
public void Domain_DependsOnNoOtherLayer() =>
    Types.InAssembly(DomainAssembly.Reference).ShouldNot()
        .HaveDependencyOnAny("GapMouse.Application", "GapMouse.Infrastructure")
        .GetResult().IsSuccessful.ShouldBeTrue();

[Fact]
public void Application_DoesNotDependOnInfrastructure() =>
    Types.InAssembly(ApplicationAssembly.Reference).ShouldNot()
        .HaveDependencyOn("GapMouse.Infrastructure")
        .GetResult().IsSuccessful.ShouldBeTrue();

[Fact]
public void Domain_DoesNotUseInterop() =>
    Types.InAssembly(DomainAssembly.Reference).ShouldNot()
        .HaveDependencyOn("System.Runtime.InteropServices")
        .GetResult().IsSuccessful.ShouldBeTrue();
```

- [ ] **Step 5: Run `dotnet build` and `dotnet test`.** Expected: build succeeds with 0 warnings and 3 tests pass. A test that "passes vacuously" on an empty assembly is fine at this point; these tests guard later tasks.

- [ ] **Step 6: CI and repository hygiene.**
  - `ci.yml` (on `push` to `main` and on `pull_request`), runner `windows-latest`. Job **`build-test`** runs: `actions/checkout@v7` → `actions/setup-dotnet@v6` with `global-json-file: global.json` → `dotnet tool restore` → `dotnet restore` → `dotnet format --verify-no-changes --no-restore` → `dotnet build -c Release --no-restore` → `dotnet test -c Release --no-build --filter "Category!=Win32" --collect:"XPlat Code Coverage" --results-directory TestResults` → `dotnet reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:coverage -reporttypes:"MarkdownSummaryGithub;Cobertura"` → append `coverage/SummaryGithub.md` to `$GITHUB_STEP_SUMMARY` → `actions/upload-artifact@v7` (name `coverage`). A second job, **`aot-publish`**, is added in Task 8.
  - `.config/dotnet-tools.json`: `dotnet-reportgenerator-globaltool` 5.5.12.
  - `codeql.yml`: `github/codeql-action/init@v4` + `analyze@v4`, language `csharp`, `build-mode: none`, on push/PR to `main` plus a weekly schedule.
  - `dependabot.yml`: ecosystems `nuget` (directory `/`) and `github-actions`, both weekly.

- [ ] **Step 7: Docs.**
  - `LICENSE`: MIT, "Copyright (c) 2026 Gustavo Auad Piccoli".
  - `README.md` skeleton: name and one-line pitch ("Keeps your Windows PC awake and your status available while you're away — light, private, no admin required."), a "🚧 Under development" status line, a **Disclaimer** section (using it on corporate machines is the user's responsibility; check your company's policies), a "Privacy" line (no telemetry; the only network call is an optional update check), and a License section.
  - ADRs in Context / Decision / Consequences format, ≤ 40 lines each, with content taken from spec §4 (0001, 0003) and §5.2 + §9 (0002).

- [ ] **Step 8: Commit.**

```bash
git add -A
git commit -m "chore: bootstrap GapMouse repository with CI and layering rules"
```

- [ ] **Step 9: Publish to GitHub — ⚠ ask Gustavo for a go-ahead in the conversation before running.** Run `gh repo create Gustavo-Piccoli/GapMouse --public --description "Keeps your Windows PC awake and your status available while you're away." --source . --remote origin --push`. Then confirm the `build-test` and CodeQL runs on `main` are green (`gh run list --limit 3`). Then create the working branch: `git switch -c feat/foundation-engine`.

---

### Task 2: Domain primitives — geometry, randomness, settings, pause duration

**Files:**
- Create: `src/GapMouse.Domain/Geometry/PixelPoint.cs`, `src/GapMouse.Domain/Geometry/ScreenBounds.cs`
- Create: `src/GapMouse.Domain/Randomness/IRandomSource.cs`
- Create: `src/GapMouse.Domain/Settings/EngineSettings.cs`, `SettingsField.cs`, `SettingsErrorCode.cs`, `SettingsError.cs`, `ValidationResult.cs` (all under `src/GapMouse.Domain/Settings/`)
- Create: `src/GapMouse.Domain/Pausing/PauseDuration.cs`
- Test: `tests/GapMouse.Domain.Tests/Geometry/ScreenBoundsTests.cs`, `tests/GapMouse.Domain.Tests/Settings/EngineSettingsTests.cs`, `tests/GapMouse.Domain.Tests/Pausing/PauseDurationTests.cs`
- Create (test helpers): `tests/GapMouse.Domain.Tests/TestDoubles/QueueRandom.cs`, `MinRandom.cs`, `MaxRandom.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct PixelPoint(int X, int Y);`
  - `public readonly record struct ScreenBounds` with constructor `(int Left, int Top, int Right, int Bottom)`. Right and Bottom are **exclusive**, as in Win32 `RECT`. The constructor throws `ArgumentException` unless `Right > Left && Bottom > Top`. It also exposes `PixelPoint Clamp(PixelPoint p)`, which clamps X to `[Left, Right-1]` and Y to `[Top, Bottom-1]`.
  - `public interface IRandomSource { int NextInclusive(int minInclusive, int maxInclusive); }`
  - `public enum SettingsField { IdleThreshold, CheckInterval, MovementIntervalMin, MovementIntervalMax, Amplitude }`
  - `public enum SettingsErrorCode { OutOfRange, MinGreaterThanMax }`
  - `public readonly record struct SettingsError(SettingsField Field, SettingsErrorCode Code);`
  - `public sealed class ValidationResult<T>` exposing `bool IsValid`, `T Value` (throws `InvalidOperationException` when invalid) and `IReadOnlyList<SettingsError> Errors`.
  - `public sealed record EngineSettings` (private constructor) with:
    - properties `TimeSpan IdleThreshold`, `TimeSpan CheckInterval`, `TimeSpan MovementIntervalMin`, `TimeSpan MovementIntervalMax`, `int AmplitudePixels`, `bool KeepDisplayOn`;
    - `public static EngineSettings Default { get; }`;
    - `public static ValidationResult<EngineSettings> Create(int idleThresholdSeconds, int checkIntervalSeconds, int movementIntervalMinSeconds, int movementIntervalMaxSeconds, int amplitudePixels, bool keepDisplayOn)`, which reports **all** errors, not just the first.
  - `public sealed record PauseDuration` exposing `TimeSpan? Length` and `bool IsIndefinite`. It has statics `TenMinutes`, `ThirtyMinutes`, `OneHour` and `Indefinite`, plus `static PauseDuration Custom(int minutes)`, which throws `ArgumentOutOfRangeException` outside 1–1440.
  - Test doubles (in the Domain.Tests project, also referenced by later tasks):
    - `QueueRandom(params int[] values)` returns the values in order and throws `InvalidOperationException` when empty. It also throws if a value falls outside the requested range, which catches call-order mistakes.
    - `MinRandom` always returns `minInclusive`.
    - `MaxRandom` always returns `maxInclusive`.

- [ ] **Step 1: Write the failing tests.**

```csharp
// ScreenBoundsTests
[Theory]
[InlineData(-1920, 0, -1920, 0)]      // secondary monitor left of primary: point at its left edge stays put
[InlineData(-5000, 500, -1920, 500)]  // far left → clamped to Left, not to 0
[InlineData(100, 2000, 100, 1079)]    // below bottom → Bottom-1
public void Clamp_KeepsPointInsideMonitor(int x, int y, int ex, int ey) =>
    new ScreenBounds(-1920, 0, 0, 1080).Clamp(new PixelPoint(x, y)).ShouldBe(new PixelPoint(ex, ey));

[Fact] public void Ctor_EmptyRectangle_Throws() => Should.Throw<ArgumentException>(() => new ScreenBounds(0, 0, 0, 10));

// EngineSettingsTests
[Fact]
public void Default_MatchesLegacyBatValues()
{
    var s = EngineSettings.Default;
    s.IdleThreshold.ShouldBe(TimeSpan.FromSeconds(120));
    s.CheckInterval.ShouldBe(TimeSpan.FromSeconds(1));
    s.MovementIntervalMin.ShouldBe(TimeSpan.FromSeconds(10));
    s.MovementIntervalMax.ShouldBe(TimeSpan.FromSeconds(25));
    s.AmplitudePixels.ShouldBe(120);
    s.KeepDisplayOn.ShouldBeTrue();
}

[Theory]
[InlineData(9, 1, 10, 25, 120, SettingsField.IdleThreshold)]
[InlineData(3601, 1, 10, 25, 120, SettingsField.IdleThreshold)]
[InlineData(120, 0, 10, 25, 120, SettingsField.CheckInterval)]
[InlineData(120, 61, 10, 25, 120, SettingsField.CheckInterval)]
[InlineData(120, 1, 0, 25, 120, SettingsField.MovementIntervalMin)]
[InlineData(120, 1, 10, 3601, 120, SettingsField.MovementIntervalMax)]
[InlineData(120, 1, 10, 25, 9, SettingsField.Amplitude)]
[InlineData(120, 1, 10, 25, 501, SettingsField.Amplitude)]
public void Create_OutOfRange_ReportsField(int idle, int check, int min, int max, int amp, SettingsField field)
{
    var r = EngineSettings.Create(idle, check, min, max, amp, true);
    r.IsValid.ShouldBeFalse();
    r.Errors.ShouldContain(new SettingsError(field, SettingsErrorCode.OutOfRange));
}

[Fact]
public void Create_MinGreaterThanMax_ReportsMinGreaterThanMax() =>
    EngineSettings.Create(120, 1, 30, 20, 120, true).Errors
        .ShouldContain(new SettingsError(SettingsField.MovementIntervalMin, SettingsErrorCode.MinGreaterThanMax));

[Fact]
public void Create_SeveralErrors_ReportsAll() =>
    EngineSettings.Create(0, 0, 10, 25, 0, true).Errors.Count.ShouldBe(3);

[Fact] public void Create_MinEqualsMax_IsValid() => EngineSettings.Create(120, 1, 15, 15, 120, false).IsValid.ShouldBeTrue();

// PauseDurationTests
[Fact] public void Presets_HaveExpectedLengths()
{
    PauseDuration.TenMinutes.Length.ShouldBe(TimeSpan.FromMinutes(10));
    PauseDuration.ThirtyMinutes.Length.ShouldBe(TimeSpan.FromMinutes(30));
    PauseDuration.OneHour.Length.ShouldBe(TimeSpan.FromHours(1));
    PauseDuration.Indefinite.IsIndefinite.ShouldBeTrue();
    PauseDuration.Indefinite.Length.ShouldBeNull();
}
[Theory, InlineData(0), InlineData(1441)]
public void Custom_OutOfRange_Throws(int m) => Should.Throw<ArgumentOutOfRangeException>(() => PauseDuration.Custom(m));
[Theory, InlineData(1), InlineData(1440)]
public void Custom_InRange_HasLength(int m) => PauseDuration.Custom(m).Length.ShouldBe(TimeSpan.FromMinutes(m));
```

- [ ] **Step 2: Run `dotnet test tests/GapMouse.Domain.Tests`.** Expected: compile failure, because the types don't exist yet.
- [ ] **Step 3: Implement the types listed in Interfaces**, plus the three test doubles.
- [ ] **Step 4: Run `dotnet test`.** Expected: all tests pass, including the architecture tests.
- [ ] **Step 5: Commit** with `feat(domain): add geometry, settings and pause duration value objects`.

---

### Task 3: Movement path planner (Bézier, faithful port)

**Files:**
- Create: `src/GapMouse.Domain/Movement/MovementStep.cs`, `MovementPath.cs`, `MovementPathPlanner.cs`
- Test: `tests/GapMouse.Domain.Tests/Movement/MovementPathPlannerTests.cs`

**Interfaces:**
- Consumes: `PixelPoint`, `ScreenBounds`, `IRandomSource`, `QueueRandom`, `MinRandom`, `MaxRandom` (Task 2).
- Produces:
  - `public readonly record struct MovementStep(PixelPoint Position, TimeSpan DelayAfter);`
  - `public sealed record MovementPath(IReadOnlyList<MovementStep> Steps, TimeSpan SettleDelay);`
  - `public static class MovementPathPlanner { public static MovementPath Plan(PixelPoint origin, ScreenBounds bounds, int amplitudePixels, IRandomSource random); }`

- [ ] **Step 1: Write the failing tests.** Random calls are consumed **in exactly this order**: `dx`, `dy`, `ctrlOffsetX`, `ctrlOffsetY`, `steps`, then one delay jitter per point (`steps + 1` calls), then `settle`.

```csharp
static readonly ScreenBounds Primary = new(0, 0, 1920, 1080);

[Fact]
public void Plan_StepCountIsStepsPlusOne_AndEndsAtDestination()
{
    // dx=+100, dy=-50, ctrl offsets 0/0, steps=20, 21 jitters of 0, settle 300
    var values = new[] { 100, -50, 0, 0, 20 }.Concat(Enumerable.Repeat(0, 21)).Append(300).ToArray();
    var path = MovementPathPlanner.Plan(new PixelPoint(500, 500), Primary, 120, new QueueRandom(values));
    path.Steps.Count.ShouldBe(21);
    path.Steps[0].Position.ShouldBe(new PixelPoint(500, 500));
    path.Steps[^1].Position.ShouldBe(new PixelPoint(600, 450));
    path.SettleDelay.ShouldBe(TimeSpan.FromMilliseconds(300));
}

[Fact]
public void Plan_DelayFollowsLegacyFormula()
{
    var values = new[] { 0, 0, 0, 0, 20 }.Concat(Enumerable.Repeat(4, 21)).Append(100).ToArray();
    var path = MovementPathPlanner.Plan(new PixelPoint(500, 500), Primary, 120, new QueueRandom(values));
    path.Steps[0].DelayAfter.ShouldBe(TimeSpan.FromMilliseconds(12));   // 8 + 4 - sin(0)*6
    path.Steps[10].DelayAfter.ShouldBe(TimeSpan.FromMilliseconds(6));   // t=0.5 → 8 + 4 - 6
}

[Fact]
public void Plan_RequestsLegacyRanges()
{
    // MinRandom: dx=dy=-120, ctrl=-60, steps=20, jitter=0, settle=100
    var minPath = MovementPathPlanner.Plan(new PixelPoint(500, 500), Primary, 120, new MinRandom());
    minPath.Steps.Count.ShouldBe(21);
    minPath.Steps[^1].Position.ShouldBe(new PixelPoint(380, 380));
    minPath.SettleDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
    var maxPath = MovementPathPlanner.Plan(new PixelPoint(500, 500), Primary, 120, new MaxRandom());
    maxPath.Steps.Count.ShouldBe(41);
    maxPath.SettleDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
}

[Fact]
public void Plan_NearEdgeOfNegativeMonitor_EveryPointStaysInside()
{
    var left = new ScreenBounds(-1920, -200, 0, 880);
    var path = MovementPathPlanner.Plan(new PixelPoint(-1915, -195), left, 120, new MinRandom());
    path.Steps.ShouldAllBe(s => s.Position.X >= -1920 && s.Position.X <= -1 && s.Position.Y >= -200 && s.Position.Y <= 879);
    path.Steps[^1].Position.ShouldBe(new PixelPoint(-1920, -200));
}

[Fact]
public void Plan_ControlPointOutsideMonitor_CurvePointsAreClamped()
{
    // origin at the right edge, dx=0, ctrl offset +60 pushes the curve past Right
    var values = new[] { 0, 0, 60, 0, 20 }.Concat(Enumerable.Repeat(0, 21)).Append(100).ToArray();
    var path = MovementPathPlanner.Plan(new PixelPoint(1919, 500), Primary, 120, new QueueRandom(values));
    path.Steps.ShouldAllBe(s => s.Position.X <= 1919);
}
```

- [ ] **Step 2: Run the tests.** Expected: compile failure.
- [ ] **Step 3: Implement `MovementPathPlanner.Plan`.** The algorithm must match `legacy/MOUSE.ps1` lines 104–138, with inclusive ranges and per-point clamping (spec §11.5):

```
half = amplitude / 2  (integer division)
dx = r(-A, A); dy = r(-A, A)
dest = bounds.Clamp(origin + (dx, dy))
ctrl = ((origin.X + dest.X) / 2.0 + r(-half, half), (origin.Y + dest.Y) / 2.0 + r(-half, half))
steps = r(20, 40)
for i in 0..steps:
    t = i / (double)steps
    p = (1-t)²·origin + 2(1-t)t·ctrl + t²·dest
    position = bounds.Clamp((int)Math.Round(p.X), (int)Math.Round(p.Y))      // banker's rounding, like PowerShell [int]
    delayMs = Math.Round(8 + r(0, 4) - Math.Sin(t·π)·6)
settle = r(100, 500) ms
```

- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit** with `feat(domain): port natural Bézier movement path planner`.

---

### Task 4: Engine core — Waiting ⇄ Moving

**Files:**
- Create: `src/GapMouse.Domain/Engine/EngineState.cs`, `PowerRequest.cs`, `EngineInput.cs`, `EngineStep.cs`, `EngineEvents.cs`, `ResumeReason.cs`, `DailyStats.cs`, `EngineSnapshot.cs`, `KeepAwakeEngine.cs` (all under `src/GapMouse.Domain/Engine/`)
- Test: `tests/GapMouse.Domain.Tests/Engine/KeepAwakeEngineMovementTests.cs`, `tests/GapMouse.Domain.Tests/Engine/EngineTestContext.cs` (shared builder)

**Interfaces:**
- Consumes: Tasks 2–3.
- Produces (all `public`, namespace `GapMouse.Domain.Engine`):
  - `enum EngineState { Waiting, Moving, Paused }`
  - `enum PowerRequest { None, KeepSystemAwake, KeepSystemAndDisplayAwake }`
  - `enum ResumeReason { Manual, PauseExpired }`
  - `readonly record struct EngineInput(DateTimeOffset Now, TimeSpan Idle, PixelPoint Cursor, ScreenBounds Monitor);` (`Now` is **local** time)
  - `readonly record struct EngineStep(PixelPoint? MoveCursorTo, PowerRequest Power, TimeSpan WaitBeforeNext);`
  - `abstract record EngineEvent(DateTimeOffset At);` and its sealed records:
    - `EngineStarted(At)`
    - `WaitingStarted(At, TimeSpan IdleThreshold)`
    - `MovingStarted(At, TimeSpan IntervalMin, TimeSpan IntervalMax)`
    - `UserReturned(At)`
    - `EnginePaused(At, PauseDuration Duration, DateTimeOffset? ResumesAt)`
    - `EngineResumed(At, ResumeReason Reason)`
    - `SettingsChanged(At, EngineSettings Settings)`
    - `EngineStopped(At)`
  - `readonly record struct DailyStats(DateOnly Day, TimeSpan MovingTime, int MovingActivations)` with `static DailyStats Empty(DateOnly day)`
  - `sealed record EngineSnapshot(EngineState State, DateTimeOffset StartedAt, TimeSpan LastIdle, TimeSpan IdleThreshold, DateTimeOffset? LastMovingStartedAt, DateTimeOffset? LastUserReturnedAt, DateTimeOffset? LastPausedAt, DateTimeOffset? LastResumedAt, DateTimeOffset? PauseEndsAt, DailyStats Today)`
  - `sealed class KeepAwakeEngine` (not thread-safe; the host serializes calls):
    - `KeepAwakeEngine(EngineSettings settings, IRandomSource random, DateTimeOffset startedAt, DailyStats? restoredStats = null)`. The constructor queues `EngineStarted` and then `WaitingStarted`.
    - `EngineState State { get; }`, `EngineSettings Settings { get; }`, `bool IsStopped { get; }`
    - `EngineStep Advance(EngineInput input)`
    - `IReadOnlyList<EngineEvent> DrainEvents()` returns the queued events in order and clears the queue.
    - `EngineSnapshot GetSnapshot()`
    - Also `Pause`, `Resume`, `ApplySettings` and `Stop`, whose signatures are fixed in Task 5. In this task, declare them as `throw new NotImplementedException()`.

**Behavior to implement** (spec §5.1–5.2; legacy lines 115–124 and 166–182):
- Waiting/Moving hold power `KeepSystemAndDisplayAwake` when `Settings.KeepDisplayOn`, else `KeepSystemAwake`.
- **Waiting:** if `Idle >= IdleThreshold`, the engine enters Moving: `MovingActivations++`, record `LastMovingStartedAt`, queue `MovingStarted`, plan a path from `input.Cursor`/`input.Monitor`, set `movementStartedAt = Now`, and return the **first** path step. Otherwise it returns `(null, power, CheckInterval)`.
- **Moving, executing a path:** it checks for user return **before** each step: `Idle < (Now - movementStartedAt) - 0.5 s`. If so, it goes to Waiting: queue `UserReturned` + `WaitingStarted`, record `LastUserReturnedAt`, return `(null, power, CheckInterval)`. Otherwise it returns `(step.Position, power, step.DelayAfter)`. After the last step is emitted, the next call returns `(null, power, SettleDelay)` and enters the inter-move wait with `waitEndsAt = Now + SettleDelay + r(min, max) seconds`. The interval random is drawn at that moment, after all of the path's draws.
- **Moving, inter-move wait:** if `Idle < 1 s`, the user returned (same handling as above). If `Now >= waitEndsAt`, a new movement starts (new path, new `movementStartedAt`; it does **not** count as an activation). Otherwise it returns `(null, power, min(CheckInterval, waitEndsAt - Now))`.
- **Daily stats:** if `DateOnly.FromDateTime(Now.Date) != Today.Day`, reset to `DailyStats.Empty(today)` before anything else. Add `Now - lastAdvanceAt` to `MovingTime` only when the engine was in Moving at both calls, capped at 60 s per call. `Advance` always updates `LastIdle`.

- [ ] **Step 1: Write the failing tests.** `EngineTestContext` builds an engine with `EngineSettings.Default` and `MinRandom`, with `T0 = 2026-11-02 09:00 -03:00` on a 1920×1080 primary monitor. It exposes `Input(TimeSpan at, TimeSpan idle)`, where `at` is the offset from `T0`. It also exposes `StartedMoving()`, which returns an engine that entered Moving at `at: 120 s, idle: 120 s` with its events drained, and `RunUntilPathFinishes(engine)`, which feeds growing-idle inputs until the path is exhausted and returns the settle step.

```csharp
[Fact] public void Ctor_QueuesStartedThenWaiting() =>
    Engine().DrainEvents().Select(e => e.GetType()).ShouldBe([typeof(EngineStarted), typeof(WaitingStarted)]);

[Fact] public void Advance_IdleBelowThreshold_StaysWaitingAndHoldsDisplayAwake()
{
    var step = Engine().Advance(Input(at: 1.s(), idle: 119.s()));
    step.ShouldBe(new EngineStep(null, PowerRequest.KeepSystemAndDisplayAwake, 1.s()));
}

[Fact] public void Advance_IdleReachesThreshold_StartsMovingAndMovesCursor()
{
    var e = Engine();
    var step = e.Advance(Input(at: 120.s(), idle: 120.s()));
    e.State.ShouldBe(EngineState.Moving);
    step.MoveCursorTo.ShouldNotBeNull();
    e.DrainEvents().OfType<MovingStarted>().Single().IntervalMin.ShouldBe(10.s());
    e.GetSnapshot().Today.MovingActivations.ShouldBe(1);
}

[Fact] public void Advance_IdleDropsDuringPath_ReturnsToWaitingWithoutMoving()
{
    var e = StartedMoving();                         // entered Moving at t=120 s, idle 120 s
    var step = e.Advance(Input(at: 120.2.s(), idle: 0.1.s()));
    step.MoveCursorTo.ShouldBeNull();
    e.State.ShouldBe(EngineState.Waiting);
    e.DrainEvents().ShouldContain(ev => ev is UserReturned);
}

[Fact] public void Advance_IdleKeepsGrowingDuringPath_DoesNotAbort()
{
    var e = StartedMoving();
    e.Advance(Input(at: 120.3.s(), idle: 120.3.s())).MoveCursorTo.ShouldNotBeNull();
}

[Fact] public void Advance_AfterPath_SettlesThenWaitsMinInterval()
{
    var e = StartedMoving();
    var settle = RunUntilPathFinishes(e);            // returns the settle step
    settle.ShouldBe(new EngineStep(null, PowerRequest.KeepSystemAndDisplayAwake, 100.ms()));
    // MinRandom → interval 10 s; at +5 s still waiting, at +10.1 s a new path starts
}

[Fact] public void Advance_InputDuringInterMoveWait_ReturnsToWaiting() { /* idle 0.5 s during wait → State Waiting, UserReturned queued */ }

[Fact] public void Advance_NewMovementAfterWait_IsNotANewActivation() { /* MovingActivations stays 1 */ }

[Fact] public void Advance_KeepDisplayOff_HoldsSystemOnly() { /* settings KeepDisplayOn=false → PowerRequest.KeepSystemAwake */ }

[Fact] public void Advance_CrossingMidnight_ResetsDailyStats() { /* Moving at 23:59:30, Advance at 00:00:10 next day → Today.Day is the new date, MovingTime < 60 s */ }

[Fact] public void Advance_ClockMovesBackwards_DoesNotAbortOrThrow() { /* during path: Now earlier than movementStartedAt, idle large → still moves */ }
```

(`1.s()` and `100.ms()` are `TimeSpan` helpers in `EngineTestContext`. Every placeholder comment above must become real assertions that check exactly the stated outcome.)

- [ ] **Step 2: Run the tests.** Expected: compile failure.
- [ ] **Step 3: Implement** the types and the behavior above. Keep the private movement phases as a small private enum (`ExecutingPath`, `InterMoveWait`) inside `KeepAwakeEngine`.
- [ ] **Step 4: Run `dotnet test`.** Expected: PASS, including the architecture tests.
- [ ] **Step 5: Commit** with `feat(domain): add keep-awake engine core with waiting and moving states`.

---

### Task 5: Engine core — pause, resume, settings changes, stop

**Files:**
- Modify: `src/GapMouse.Domain/Engine/KeepAwakeEngine.cs`
- Test: `tests/GapMouse.Domain.Tests/Engine/KeepAwakeEnginePauseTests.cs`, `KeepAwakeEngineSettingsTests.cs`

**Interfaces:**
- Produces (replacing the Task 4 stubs):
  - `void Pause(PauseDuration duration, DateTimeOffset now)`: valid from any state. If already paused, it replaces the duration. It queues `EnginePaused(now, duration, now + duration.Length)`, discards any in-flight path or wait, and sets `LastPausedAt`.
  - `bool Resume(DateTimeOffset now)`: returns `false` and does nothing unless paused. Otherwise it goes to Waiting, queues `EngineResumed(now, Manual)` + `WaitingStarted`, and sets `LastResumedAt`.
  - `void ApplySettings(EngineSettings settings, DateTimeOffset now)`: replaces `Settings` and queues `SettingsChanged`. A path or inter-move wait already in progress keeps its planned values. New values apply from the next decision: the next Waiting threshold check, the next path planned and the next interval drawn. The power request reflects the new `KeepDisplayOn` from the next `Advance`.
  - `void Stop(DateTimeOffset now)`: idempotent. It queues `EngineStopped` once and sets `IsStopped`. After that, `Advance` returns `(null, PowerRequest.None, CheckInterval)`.
- **Paused** behavior in `Advance`:
  - If `PauseEndsAt <= Now`, it auto-resumes: queue `EngineResumed(Now, PauseExpired)` + `WaitingStarted`, then evaluate Waiting in the **same** call using the real idle (spec §11.3).
  - Otherwise it returns `(null, PowerRequest.None, CheckInterval)` when the pause is indefinite, or `(null, PowerRequest.None, min(CheckInterval, PauseEndsAt - Now))` when it is timed.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public void Pause_DuringPath_NoFurtherCursorMovesAndPowerReleased()
{
    var e = StartedMoving();
    e.Pause(PauseDuration.Indefinite, T0 + 120.1.s());
    var step = e.Advance(Input(at: 120.2.s(), idle: 500.s()));
    step.ShouldBe(new EngineStep(null, PowerRequest.None, 1.s()));
    e.State.ShouldBe(EngineState.Paused);
    e.GetSnapshot().PauseEndsAt.ShouldBeNull();
}

[Fact] public void Pause_Timed_ExpiresAndResumesWithReasonPauseExpired()
{
    var e = Engine();
    e.Pause(PauseDuration.TenMinutes, T0);
    e.Advance(Input(at: 599.s(), idle: 5.s())).Power.ShouldBe(PowerRequest.None);
    e.Advance(Input(at: 600.s(), idle: 5.s())).Power.ShouldBe(PowerRequest.KeepSystemAndDisplayAwake);
    e.DrainEvents().OfType<EngineResumed>().Single().Reason.ShouldBe(ResumeReason.PauseExpired);
}

[Fact] public void Pause_ExpiresWhileUserAway_MovesInSameAdvance()
{
    var e = Engine();
    e.Pause(PauseDuration.TenMinutes, T0);
    e.Advance(Input(at: 600.s(), idle: 600.s())).MoveCursorTo.ShouldNotBeNull();
    e.State.ShouldBe(EngineState.Moving);
}

[Fact] public void Pause_TimedNearEnd_WaitIsShortenedToRemaining() { /* paused 10 min at T0; Advance at 599.6 s → WaitBeforeNext == 0.4 s */ }

[Fact] public void Pause_WhilePaused_ReplacesDuration() { /* Indefinite then TenMinutes → PauseEndsAt = second call time + 10 min */ }

[Fact] public void Resume_WhenNotPaused_ReturnsFalseAndQueuesNothing() { }

[Fact] public void Resume_Manual_GoesToWaitingWithReasonManual() { }

[Fact] public void ApplySettings_DuringPath_RemainingStepsUnchanged()
{
    // Compare two engines with the same MinRandom and the same inputs; one gets amplitude 500 mid-path.
    // The remaining steps of the current path must be identical; the next path (after the wait) must differ.
}

[Fact] public void ApplySettings_NewThreshold_UsedOnNextWaitingCheck() { /* threshold 300 s; idle 200 s → stays Waiting */ }

[Fact] public void ApplySettings_DisplayOff_PowerChangesOnNextAdvance() { }

[Fact] public void Stop_IsIdempotentAndReleasesPower() { /* Stop twice → one EngineStopped; Advance → Power None, no move */ }

[Fact] public void PausedTime_IsNotCountedInMovingTime() { }
```

Every placeholder comment must become real assertions that check exactly the stated outcome.

- [ ] **Step 2: Run the tests.** Expected: FAIL (`NotImplementedException`).
- [ ] **Step 3: Implement** the four methods and the Paused branch of `Advance`.
- [ ] **Step 4: Run `dotnet test`.** Expected: PASS (all Domain, Architecture and Task 4 tests).
- [ ] **Step 5: Commit** with `feat(domain): add pause, resume, live settings and stop to engine`.

---

### Task 6: Application — ports and `EngineHost` (imperative shell)

**Files:**
- Create: `src/GapMouse.Application/Abstractions/IIdleTimeProvider.cs`, `ICursorController.cs`, `IMonitorBoundsProvider.cs`, `IPowerRequestService.cs`
- Create: `src/GapMouse.Application/Engine/IEngineController.cs`, `src/GapMouse.Application/Engine/EngineHost.cs`
- Create: `docs/adr/0004-functional-core-engine-on-dedicated-thread.md`
- Modify: `Directory.Packages.props` usage — `GapMouse.Application.csproj` references `Microsoft.Extensions.Logging.Abstractions`; `GapMouse.Application.Tests.csproj` references `Microsoft.Extensions.TimeProvider.Testing` and `GapMouse.Domain.Tests` (for test doubles)
- Test: `tests/GapMouse.Application.Tests/Engine/EngineHostTests.cs`, `tests/GapMouse.Application.Tests/Fakes/FakeIdle.cs`, `FakeCursor.cs`, `FakeMonitors.cs`, `RecordingPower.cs`

**Interfaces:**
- Consumes: Task 4/5 engine API.
- Produces:
  - `public interface IIdleTimeProvider { TimeSpan GetIdleTime(); }`
  - `public interface ICursorController { PixelPoint GetPosition(); bool TrySetPosition(PixelPoint position); }`
  - `public interface IMonitorBoundsProvider { ScreenBounds GetBoundsAt(PixelPoint point); }`
  - `public interface IPowerRequestService { void Apply(PowerRequest request); }`. The XML doc must state: "must be called from a single thread; Win32 execution state is per-thread".
  - `public interface IEngineController` with:
    - `EngineSnapshot Snapshot { get; }` (thread-safe; returns the latest published copy)
    - `event EventHandler<EngineEventArgs>? EventRaised;` (raised on the engine thread)
    - `void Pause(PauseDuration duration);`
    - `void Resume();`
    - `void ApplySettings(EngineSettings settings);`
  - `public sealed class EngineEventArgs(EngineEvent engineEvent) : EventArgs { public EngineEvent Event { get; } = engineEvent; }`
  - `public sealed class EngineHost : IEngineController, IDisposable` with:
    - constructor `(EngineSettings initialSettings, IIdleTimeProvider idle, ICursorController cursor, IMonitorBoundsProvider monitors, IPowerRequestService power, IRandomSource random, TimeProvider time, ILogger<EngineHost> logger, DailyStats? restoredStats = null)`
    - `void Start()`, which throws if called twice
    - `Dispose()`, which signals stop, waits for the thread (≤ 5 s) and is idempotent

**Behavior:**
- One `Thread` (`IsBackground = true`, `Name = "GapMouse.Engine"`). The engine is created on that thread with `time.GetLocalNow()`.
- Each loop iteration:
  1. Drain the command queue (`ConcurrentQueue<Action<KeepAwakeEngine, DateTimeOffset>>`).
  2. Read `now = time.GetLocalNow()`, idle, cursor and monitor bounds at the cursor.
  3. Call `engine.Advance`.
  4. If `step.Power` differs from the last applied value, call `power.Apply`. The very first iteration always applies.
  5. If `MoveCursorTo` has a value, call `cursor.TrySetPosition`.
  6. Drain and raise the engine events.
  7. Publish a new snapshot (`Volatile.Write`).
  8. Block on `AutoResetEvent.WaitOne(step.WaitBeforeNext)`.
- `Pause`, `Resume` and `ApplySettings` enqueue a command and `Set()` the event, so they take effect within one loop iteration, never after a full check interval.
- **Port failures:** an exception from any port, or `TrySetPosition` returning `false`, is logged at Warning. Log once per failure streak, plus a single Information line when the port recovers. The loop continues: when the failure aborts that iteration, the next one waits `Settings.CheckInterval`. The host never busy-loops.
- **Shutdown:** a `finally` block on the engine thread calls `engine.Stop(now)`, raises the final events and calls `power.Apply(PowerRequest.None)`, all on the engine thread.

- [ ] **Step 1: Write the failing tests.** Use `FakeTimeProvider` frozen at a fixed local time, and `SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)).ShouldBeTrue()` to wait. `RecordingPower` records `(PowerRequest, ManagedThreadId)` pairs.

```csharp
[Fact] public void Start_AppliesDisplayAwakeAndRaisesStarted() { /* first Apply == KeepSystemAndDisplayAwake; events include EngineStarted, WaitingStarted */ }

[Fact] public void Pause_WhileMoving_StopsCursorAndReleasesPower()
{
    // idle fake = 1 h → engine moves immediately
    // wait until FakeCursor.SetCount > 0; host.Pause(PauseDuration.Indefinite)
    // wait until power.Last == PowerRequest.None; record SetCount; Thread.Sleep(300); SetCount unchanged
}

[Fact] public void Dispose_ReleasesPowerOnEngineThreadAndRaisesStopped()
{
    // every recorded Apply has the same ManagedThreadId, which is not the test thread's id; last Apply == None; EngineStopped raised
}

[Fact] public void CursorThrows_LoopKeepsRunningAndLogsOncePerStreak()
{
    // FakeCursor.TrySetPosition throws InvalidOperationException; wait 1.5 s;
    // host.Snapshot.State still Moving; logger has exactly 1 Warning; power still applied
}

[Fact] public void ApplySettings_TakesEffectWithoutWaitingFullInterval() { /* settings with KeepDisplayOn=false → power switches to KeepSystemAwake within 500 ms */ }

[Fact] public void Start_Twice_Throws() { }
```

Use a minimal in-test `ListLogger<T> : ILogger<T>` that captures levels. Every placeholder comment must become real assertions that check exactly the stated outcome.

- [ ] **Step 2: Run the tests.** Expected: compile failure.
- [ ] **Step 3: Implement** the ports and `EngineHost` as specified. ADR 0004 records why: a pure core gives deterministic tests, and the dedicated thread exists because `SetThreadExecutionState` is per-thread.
- [ ] **Step 4: Run `dotnet test`.** Expected: PASS, with the full suite green. Run the host tests 3 times in a row (`dotnet test tests/GapMouse.Application.Tests` ×3) to catch flakiness.
- [ ] **Step 5: Commit** with `feat(application): add engine host running the core on a dedicated thread`.

---

### Task 7: Infrastructure — Win32 adapters

**Files:**
- Create: `src/GapMouse.Infrastructure/Win32/NativeMethods.cs` (`internal static partial class`, `[LibraryImport]` only; `AllowUnsafeBlocks` enabled in the csproj)
- Create: `src/GapMouse.Infrastructure/Win32/IdleTime.cs`, `Win32IdleTimeProvider.cs`, `Win32CursorController.cs`, `Win32MonitorBoundsProvider.cs`, `Win32PowerRequestService.cs`
- Create: `src/GapMouse.Infrastructure/Randomness/SystemRandomSource.cs`
- Modify: `src/GapMouse.Infrastructure/GapMouse.Infrastructure.csproj` (`InternalsVisibleTo GapMouse.Infrastructure.Tests`, `AllowUnsafeBlocks`)
- Create: `tests/GapMouse.Infrastructure.Tests/GapMouse.Infrastructure.Tests.csproj` (`net10.0-windows`; add it to `GapMouse.slnx`)
- Test: `tests/GapMouse.Infrastructure.Tests/Win32/IdleTimeTests.cs`, `Win32AdaptersTests.cs`, `tests/GapMouse.Infrastructure.Tests/Randomness/SystemRandomSourceTests.cs`

**Interfaces:**
- Consumes: the Task 6 ports and the Task 2 `IRandomSource`.
- Produces (all `public sealed`, each with a parameterless constructor):
  - `Win32IdleTimeProvider : IIdleTimeProvider`
  - `Win32CursorController : ICursorController`
  - `Win32MonitorBoundsProvider : IMonitorBoundsProvider`
  - `Win32PowerRequestService : IPowerRequestService`
  - `SystemRandomSource : IRandomSource` (uses `Random.Shared.Next(min, max + 1)`)
  - `internal static class IdleTime { internal static TimeSpan FromTicks(uint tickNow, uint lastInputTick); }` computes `unchecked(tickNow - lastInputTick)` milliseconds.

**Win32 details:**
- `GetLastInputInfo(ref LASTINPUTINFO)` with `cbSize = 8`. Compare against `(uint)Environment.TickCount`. On failure, throw `Win32Exception`.
- `GetCursorPos(out POINT)` throws `Win32Exception` on failure. `SetCursorPos(int, int)` returns its bool; it never throws for a `false` result.
- `MonitorFromPoint(POINT, MONITOR_DEFAULTTONEAREST = 2)` + `GetMonitorInfoW(nint, ref MONITORINFO)` with `cbSize = 40`. Use `rcMonitor` (the full monitor area, matching the legacy `Screen.Bounds`).
- `SetThreadExecutionState(uint)` with `ES_CONTINUOUS = 0x80000000u`, `ES_SYSTEM_REQUIRED = 0x1u` and `ES_DISPLAY_REQUIRED = 0x2u`. Mapping: `None` → `ES_CONTINUOUS`; `KeepSystemAwake` → `ES_CONTINUOUS | ES_SYSTEM_REQUIRED`; `KeepSystemAndDisplayAwake` → all three. A return of `0` throws `Win32Exception`. (C# `uint` literals are safe; the PowerShell-era decimal workaround does not apply.)
- Bool returns use `[return: MarshalAs(UnmanagedType.Bool)]`.

- [ ] **Step 1: Write the failing tests.**

```csharp
// IdleTimeTests (pure, runs in CI)
[Fact] public void FromTicks_Normal() => IdleTime.FromTicks(10_000, 7_500).ShouldBe(TimeSpan.FromMilliseconds(2_500));
[Fact] public void FromTicks_AcrossWrapAround_IsSmallPositive() => IdleTime.FromTicks(5, uint.MaxValue - 4).ShouldBe(TimeSpan.FromMilliseconds(10));

// SystemRandomSourceTests (CI)
[Fact] public void NextInclusive_CanReturnBothBounds()
{
    var r = new SystemRandomSource();
    var seen = Enumerable.Range(0, 2000).Select(_ => r.NextInclusive(10, 12)).ToHashSet();
    seen.ShouldBe(new HashSet<int> { 10, 11, 12 }, ignoreOrder: true);
}

// Win32AdaptersTests — [Trait("Category", "Win32")], local only
[Fact] public void Idle_IsNonNegative() => new Win32IdleTimeProvider().GetIdleTime().ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
[Fact] public void MonitorBounds_ContainCursor() { /* b.Left <= p.X < b.Right && b.Top <= p.Y < b.Bottom */ }
[Fact] public void Cursor_SetToCurrentPosition_RoundTrips() { /* get p; TrySetPosition(p) true; get == p */ }
[Fact] public void Cursor_SetDoesNotResetSystemIdle()   // pins the legacy assumption (spec §5.2)
{
    // Thread.Sleep(1500); idleBefore = idle; move +1 px and back; idleAfter >= idleBefore
}
[Theory, InlineData(PowerRequest.KeepSystemAndDisplayAwake), InlineData(PowerRequest.KeepSystemAwake), InlineData(PowerRequest.None)]
public void Power_Apply_DoesNotThrow(PowerRequest r) => Should.NotThrow(() => new Win32PowerRequestService().Apply(r));
```

The placeholder comments must become real assertions. `Cursor_SetDoesNotResetSystemIdle` must pass with the mouse and keyboard untouched; its doc comment says so.

- [ ] **Step 2: Run the tests.** Expected: compile failure.
- [ ] **Step 3: Implement** the adapters.
- [ ] **Step 4: Run `dotnet test`.** Locally all tests run and PASS (keep hands off the mouse). Also run `dotnet test --filter "Category!=Win32"`, which must PASS, as in CI.
- [ ] **Step 5: Commit** with `feat(infrastructure): add Win32 adapters for idle, cursor, monitors and power`.

---

### Task 8: Dev console host, AOT publish gate and parity check

**Files:**
- Create: `tools/GapMouse.DevConsole/GapMouse.DevConsole.csproj` (`OutputType Exe`, `net10.0-windows`, `PublishAot true`, `ApplicationManifest app.manifest`; add to `GapMouse.slnx`)
- Create: `tools/GapMouse.DevConsole/app.manifest` (PerMonitorV2 `dpiAwareness` + `dpiAware true/pm`)
- Create: `tools/GapMouse.DevConsole/Program.cs`, `tools/GapMouse.DevConsole/ConsoleOptions.cs`, `tools/GapMouse.DevConsole/ConsoleEventPrinter.cs`, `tools/GapMouse.DevConsole/ConsoleLogger.cs`
- Modify: `.github/workflows/ci.yml` (add job `aot-publish`)

**Interfaces:**
- Consumes: `EngineHost` (Task 6), the Win32 adapters and `SystemRandomSource` (Task 7), `EngineSettings.Create` (Task 2).
- Produces: `ConsoleOptions.Parse(string[] args) -> ValidationResult<EngineSettings>`. Flags: `--idle <s>`, `--check <s>`, `--min <s>`, `--max <s>`, `--amplitude <px>`, `--display on|off`. Defaults come from `EngineSettings.Default`. Unknown flags or invalid values print the errors and exit with code 2.

**Behavior:**
- The printer writes one line per event, in the legacy format and colors:
  - `[HH:mm:ss] | [START]   | Engine started (p = pause, t = pause 1 min, r = resume, q = quit)` (Yellow)
  - `[WAITING] | Waiting for {threshold} s of inactivity` (Cyan)
  - `[MOVING]  | Moving the mouse every {min}–{max} s` (Green)
  - `[RETURN]  | User activity detected` (Gray)
  - `[PAUSED]  | Paused (until HH:mm:ss | indefinitely)` (Magenta)
  - `[RESUMED] | Resumed (manual | pause expired)` (Magenta)
  - `[CONFIG]  | Settings changed` (DarkGray)
  - `[STOP]    | Engine stopped` (Red)
- Keys (read on the main thread with `Console.KeyAvailable` polling every 100 ms): `p` → `Pause(Indefinite)`, `t` → `Pause(Custom(1))`, `r` → `Resume()`, `q` → quit.
- `Console.CancelKeyPress` sets `e.Cancel = true` and triggers the same graceful quit, which disposes the host so power is released.
- `ConsoleLogger<T>` is a tiny `ILogger<T>` that writes Warning+ lines in Yellow. No logging package beyond Abstractions.

- [ ] **Step 1: Implement the dev console** as specified.
- [ ] **Step 2: AOT gate locally.** Run `dotnet publish tools/GapMouse.DevConsole -c Release -r win-x64 -o artifacts/devconsole`. Expected: success with **0 warnings** (IL2xxx/IL3xxx count as errors through `TreatWarningsAsErrors`). Report the size of `artifacts/devconsole/GapMouse.DevConsole.exe` in the task report. Add `artifacts/` to `.gitignore`.
- [ ] **Step 3: Add the CI job `aot-publish`** to `ci.yml` (`windows-latest`, `needs: build-test`): checkout → setup-dotnet → the same publish command → `actions/upload-artifact@v7` (name `devconsole-win-x64`, retention 7 days).
- [ ] **Step 4: Parity smoke test (manual, local).** Run `artifacts/devconsole/GapMouse.DevConsole.exe --idle 10 --min 3 --max 5`, keep hands off the input devices, and check each item:
  - (a) `[MOVING]` appears ≈10 s after the last input;
  - (b) the cursor moves along a curve every 3–5 s;
  - (c) touching the mouse prints `[RETURN]` + `[WAITING]`;
  - (d) `t` prints `[PAUSED] until …`, then `[RESUMED] (pause expired)` after ~1 min;
  - (e) while paused, `powercfg /requests` (in an elevated prompt run by Gustavo, or skipped if unavailable) lists no GapMouse request, and while waiting it lists one under DISPLAY and SYSTEM;
  - (f) `q` prints `[STOP]` and exits.

  Record the results in the task report. If (e) can't run, say so explicitly.
- [ ] **Step 5: Commit** with `feat(tools): add dev console host and native AOT publish gate`.

---

### Task 9: Wrap-up — docs, PR and branch protection

**Files:**
- Modify: `README.md` (add a "Build from source" section with `dotnet build`, `dotnet test` and the dev console command; add CI and CodeQL badges and a License badge)
- Modify: `CLAUDE.md` (rewrite for the new layout: projects and responsibilities, functional-core/host design, the per-thread power rule, the `Category=Win32` test convention, where the spec/plans live, and that `legacy/` holds the original scripts. Remove the obsolete `PS1_PATH` warning. Keep it in Portuguese, as today.)

- [ ] **Step 1: Update `README.md` and `CLAUDE.md`** as listed. Commit with `docs: document build, architecture and conventions`.
- [ ] **Step 2: Full verification.** Run `dotnet format --verify-no-changes`, `dotnet build -c Release` (0 warnings), `dotnet test -c Release` (all green locally, Win32 tests included) and the AOT publish. All must pass before continuing.
- [ ] **Step 3: Open the PR** with `git push -u origin feat/foundation-engine`, then `gh pr create --base main --title "feat: foundation and keep-awake engine" --body …`. The body summarizes Tasks 2–8 and the parity check results, and ends with "🤖 Generated with [Claude Code](https://claude.com/claude-code)". Wait for `build-test`, `aot-publish` and CodeQL to go green (`gh pr checks --watch`).
- [ ] **Step 4: Branch protection — ⚠ confirm with Gustavo before running.** Run `gh api -X PUT repos/Gustavo-Piccoli/GapMouse/branches/main/protection`, sending JSON on stdin (`--input -`):

```json
{
  "required_status_checks": { "strict": true, "contexts": ["build-test", "aot-publish"] },
  "enforce_admins": false,
  "required_pull_request_reviews": { "required_approving_review_count": 0 },
  "restrictions": null
}
```

- [ ] **Step 5: Hand back to Gustavo.** Give him the PR link and let him merge it (or ask before merging on his behalf).
