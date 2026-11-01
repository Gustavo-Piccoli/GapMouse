# GapMouse Visual Refresh (v1.1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the GapMouse Avalonia app its own design system ("door hanger" direction) across every screen, with real light/dark tokens, a left navigation rail, and a headless screenshot tool, shipped as v1.1.0.

**Architecture:** Fluent stays the control set; `FluentTheme.Palettes` recolours it and our own theme-aware tokens (`Styles/Tokens.axaml`) plus class-based styles (`Styles/Controls.axaml`) restyle the views. View models stay Avalonia-free; the only new C# in the app is two small value converters. A console tool renders the real views headlessly with test doubles and writes PNGs.

**Tech Stack:** .NET 10, Avalonia 12.1.4 (Fluent, Headless, Skia), CommunityToolkit.Mvvm, xUnit v3 + Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-01-gapmouse-visual-refresh-design.md` (all colour, type, geometry and layout values live there, §6 and §7; copy them verbatim). Direction contract: `.impeccable/surfaces/src-gapmouse-app-views-homeview-axaml.md`. UI quality floor every UI task must read first: `C:\Users\Gustavo\.claude\skills\impeccable\reference\craft-floor.md`.

## Global Constraints

- Avalonia 12.1.4; package versions only in `Directory.Packages.props`; no new package in `src/` (only the screenshot tool may add `Avalonia.Headless`, `Avalonia.Skia` is transitive).
- Native AOT app: compiled bindings (`x:DataType` everywhere), no reflection, `dotnet publish src/GapMouse.App -c Release -r win-x64` must stay warning-free (warnings are errors).
- View models (`GapMouse.App.ViewModels*`, `GapMouse.App.Localization`) must not reference Avalonia (architecture tests). Converters live in `GapMouse.App.Views`.
- Every new UI string is `T("English", "Português")` in the matching `Localizer.<Area>.cs`; log tags stay English; existing strings are not reworded.
- Colours only through tokens (`DynamicResource Gm…Brush`) or the converters' fixed state/tag tables from spec §6.1; no other hex literal in any `.axaml`.
- Window 760×600 default, 640×520 minimum: nothing may clip or overflow at the minimum in English or Portuguese.
- One motion only: the Home face flip (scaleX 0.3→1, 220 ms, CubicEaseOut).
- Code, names and commits in English, Conventional Commits, **no AI attribution trailers**. `dotnet format --verify-no-changes` must pass.
- Tests: `dotnet test --filter-not-trait "Category=Win32"` (Microsoft.Testing.Platform syntax).

## Review Focus

1. Portuguese copy at 640×520 (longest labels: "Intervalo mínimo entre movimentos (s)", "Sair do aplicativo") must fit without clipping: the screenshot tool renders every scenario at the minimum size (Task 1), and each UI task checks its PT `-min` images.
2. Light theme: any surface still painted with a dark-only colour becomes unreadable; every UI task checks its `light-*` images.
3. Keyboard: rail items, toggles and buttons must stay reachable with Tab/arrow keys; rail items remain `TabItem`s (Task 3) and settings toggles stay standard `ToggleSwitch` (Task 6).
4. The hotkey capture box (`x:Name="HotkeyBox"`, code-behind `KeyDown`) must keep working after the Settings rewrite: the name and `IsReadOnly="True"` stay (Task 6 build proves the code-behind still compiles against it).
5. Long values (Windows paths in About, long log messages) wrap inside their row instead of overflowing horizontally (Tasks 5 screenshots with the sample long path and long WARN line).

---

### Task 1: Headless screenshot tool

**Files:**
- Create: `tools/GapMouse.Screenshots/GapMouse.Screenshots.csproj`, `tools/GapMouse.Screenshots/Program.cs`, `tools/GapMouse.Screenshots/Scenarios.cs`, `tools/GapMouse.Screenshots/Doubles.cs`
- Modify: `GapMouse.slnx` (add under `/tools/`), `Directory.Packages.props` (add `Avalonia.Headless` 12.1.4)

**Interfaces:**
- Consumes: `MainWindow`, `CustomPauseWindow`, all view models and `Localizer` from `GapMouse.App` (InternalsVisibleTo is not needed: use public constructors); `FakeEngineController` from `tests/GapMouse.Testing`; `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`).
- Produces: `dotnet run --project tools/GapMouse.Screenshots -- [--out <dir>]` writing `<out>/<theme>-<lang>/<scenario>[-min].png`, theme ∈ {dark, light}, lang ∈ {en, pt}, scenario ∈ {home-waiting, home-moving, home-paused, logs, settings, about, custom-pause}; default `<out>` = `artifacts/screenshots`. Later tasks rely on this exact layout and on `MainWindow` containing exactly one `TabControl` whose items are, in order, Home, Logs, Settings, About.

- [ ] **Step 1: Project.** `net10.0-windows` console `Exe`, not AOT, not packable; references `src/GapMouse.App`, `tests/GapMouse.Testing`, packages `Avalonia.Headless`, `Microsoft.Extensions.TimeProvider.Testing`. Add `Avalonia.Headless` `12.1.4` to `Directory.Packages.props` and the project to `GapMouse.slnx`.
- [ ] **Step 2: Doubles (`Doubles.cs`).** In-memory `ISettingsService` (Current = `AppSettings` defaults, `Save` stores and returns success, `IsHotkeyRegistered = true`), immediate `IUiDispatcher`, no-op `IPausePrompt`/`IAppLifetime`/`IShellLauncher`/`IClipboardService`, an `IUpdateChecker` that never finds an update.
- [ ] **Step 3: Scenarios (`Scenarios.cs`).** Fixed clock `2026-11-01 14:32:10` local. Snapshots via `FakeEngineController.Snapshot with { … }`: waiting = idle 45 s of 120 s; moving = `State = Moving`, idle 130 s, last movement 14:30:02; paused = `State = Paused`, `PauseEndsAt` now + 9 min 41 s. All three: started 08:57:13, last return 14:12:40, last pause 12:01:05, last resume 13:02:11, today 1 h 12 min 30 s active, 9 activations. Logs: an `InMemoryActivityLog` with 14 realistic entries covering every `LogKind`, including `WARN "Port Cursor failed: Access denied."` and one message longer than 120 characters. About: `AppPaths(@"C:\Users\gustavo\AppData\Roaming\GapMouse", @"C:\Users\gustavo\AppData\Local\GapMouse\logs", false)`, version `1.1.0`.
- [ ] **Step 4: Program.** `AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting()`. For each theme (`Application.Current.RequestedThemeVariant`), language (`Localizer.SetLanguage`) and size (760×600, then 640×520 with `-min`): build the view models, show `MainWindow` (DataContext = `MainWindowViewModel`) at the size, select the tab by index, run `Dispatcher.UIThread.RunJobs()` and `AvaloniaHeadlessPlatform.ForceRenderTimerTick(30)` (so the Task 4 flip animation has finished), `CaptureRenderedFrame()` and save PNG; close the window. `custom-pause` shows `CustomPauseWindow` with its view model. Print each written path; exit code 1 if any capture is null.
- [ ] **Step 5: Run it.** `dotnet run --project tools/GapMouse.Screenshots`. Expected: 56 PNG paths printed, files non-empty, and opening `artifacts/screenshots/dark-en/home-waiting.png` shows the current (pre-redesign) Home tab with readable text. This is the baseline.
- [ ] **Step 6: Verify and commit.** `dotnet build -c Release`, `dotnet format --verify-no-changes`, tests green. `git commit -m "build: add headless screenshot tool"`.

### Task 2: Design tokens, Fluent palette and base styles

**Files:**
- Create: `src/GapMouse.App/Styles/Tokens.axaml` (ResourceDictionary with `ThemeDictionaries` Dark/Light: every `Gm…Brush` of spec §6.1, plus `GmControlRadius` 6, `GmCardRadius` 12, `GmFontText`, `GmFontDisplay`, `GmFontMono` FontFamily resources), `src/GapMouse.App/Styles/Controls.axaml` (Styles)
- Create: `src/GapMouse.App/Views/LogKindInkConverter.cs`; Modify: `src/GapMouse.App/Views/LogKindBrushConverter.cs` (returns the tag **fill** from spec §6.1 table)
- Modify: `src/GapMouse.App/App.axaml` (merge Tokens, include Controls, `FluentTheme.Palettes` Dark/Light with `Accent`/`RegionColor` per spec, `ControlCornerRadius` 6, `OverlayCornerRadius` 8, Window default `FontFamily` = text font, Window `Background` = `GmGroundBrush`), every view `.axaml` (replace each hard-coded hex with its token: errors → `GmCriticalTextBrush`, warning → `GmWarningTextBrush`, state dot → state fills via styles)
- Test: `tests/GapMouse.App.Tests/Views/LogKindConvertersTests.cs`

**Interfaces:**
- Produces (used by Tasks 3–6): style classes `TextBlock.title` (20 SemiBold), `TextBlock.group` (16 SemiBold), `TextBlock.muted` (`GmTextMutedBrush`), `TextBlock.caption` (12, muted), `TextBlock.error`, `TextBlock.warning`, `Border.card` (`GmSurfaceBrush`, 1 px `GmHairlineBrush`, radius 12), `Border.divider` (1 px high, `GmHairlineBrush`), `Grid.row` (padding via Margin 16,12; `MinHeight` 44); converters `LogKindBrushConverter` (fill) and `LogKindInkConverter` (ink).

- [ ] **Step 1: Failing tests** `LogKindConvertersTests`: `Convert(LogKind.Moving)` fill is `#17804A` (became `#157545` in the finish review), ink `#FFFFFF`; `Paused` and `Resumed` fill `#E3A21A`, ink `#1F1503`; `Warn` fill `#C2410C`; `Stop` and `Error` fill `#B42318`; `Start` `#475569`; `Waiting` `#2F5FD0`; `Return` `#5B6474`; `Config` `#6B5BD6`; a non-`LogKind` value returns the `Return` pair. Assert via `((ISolidColorBrush)result).Color == Color.Parse(...)`.
- [ ] **Step 2:** Run `dotnet test --project tests/GapMouse.App.Tests --filter-not-trait "Category=Win32"` → the new tests fail.
- [ ] **Step 3:** Implement the converters, tokens, styles and `App.axaml`; swap every hex literal in the views for tokens (layouts unchanged in this task).
- [ ] **Step 4:** Tests green; run the screenshot tool; `light-en/settings.png` and `light-en/home-waiting.png` show a light ground with dark text and no dark-only surfaces; `dark-en/*` unchanged in layout.
- [ ] **Step 5:** `git commit -m "feat(app): add design tokens, themed Fluent palette and base styles"`.

### Task 3: Navigation rail

**Files:** Modify `src/GapMouse.App/Views/MainWindow.axaml`; Modify `src/GapMouse.App/Styles/Controls.axaml` (rail styles/templates).

**Interfaces:** Consumes Task 2 tokens. Produces the rail layout; keeps one `TabControl` with `TabItem`s in order Home, Logs, Settings, About (Task 1 relies on it).

- [ ] **Step 1:** `TabControl` with `TabStripPlacement="Left"` and a custom `ControlTemplate` (rail `Border` 184 px in `GmRailBrush` holding a brand header — `gapmouse.ico` 20 px + `L.AppName` SemiBold — and the items presenter; content host on `GmGroundBrush`). Each `TabItem` header = icon `TextBlock` (`Segoe Fluent Icons, Segoe MDL2 Assets`, glyphs `E80F`, `E9F9`, `E713`, `E946`) + label bound to `L.TabHome/TabLogs/TabSettings/TabAbout`. Item: height 40, radius 6, hover `GmSurfaceHoverBrush`, selected `GmSurfaceHoverBrush` + 3×16 px `GmAccentBrush` indicator left of the icon, keyboard focus ring kept.
- [ ] **Step 2:** Build; screenshots: rail visible in all four themes/languages, PT labels ("Configurações") fit in 184 px at `-min`.
- [ ] **Step 3:** `git commit -m "feat(app): replace top tabs with a navigation rail"`.

### Task 4: Home — the door-hanger face

**Files:** Modify `src/GapMouse.App/Views/HomeView.axaml`, `src/GapMouse.App/Localization/Localizer.Home.cs`; Create `src/GapMouse.App/Views/ProgressToSweepConverter.cs`; Test `tests/GapMouse.App.Tests/Views/ProgressToSweepConverterTests.cs`, `tests/GapMouse.App.Tests/Localization/LocalizerTests.cs` (add case).

**Interfaces:** Consumes `HomeViewModel` as is (`StateText`, `IdleText`, `IdleProgress`, `PauseText`, `HasPauseText`, `IsWaiting/IsMoving/IsPaused`, times, `TodayActiveTime`, `TodayActivations`, commands) and Task 2 classes. Produces `Localizer.HomeRecentTitle` and `ProgressToSweepConverter`.

- [ ] **Step 1: Failing tests.** `ProgressToSweepConverter.Convert(0.375)` → `135.0`; `Convert(1.5)` → `360.0`; `Convert(-1.0)` → `0.0`; non-double → `0.0`. `LocalizerTests`: `HomeRecentTitle` is `"Recent"` in English and `"Recentes"` in Portuguese.
- [ ] **Step 2:** Run → fail.
- [ ] **Step 3:** Implement converter and string; rewrite `HomeView.axaml` per spec §7 "Início": left face `Border` 220 px wide, radius 12, background by state class (`face.waiting/.moving/.paused` → state fills; ink and secondary ink per spec table), knob ring 56 px (`Ellipse` track at ink 25 % + `Arc` with `SweepAngle` from `IdleProgress` through the converter, `StartAngle` -90, hidden sweep when paused), `StateText` in caps (`GmFontDisplay`, 30 Bold), idle and pause text in secondary ink; the flip `Animation` (RenderTransform `scaleX(0.3)`→`scaleX(1)`, 220 ms, `CubicEaseOut`, `RenderTransformOrigin` centre) declared on each state-class style so it plays when the class changes. Right column: `Today` and `Recent` cards with label/value rows, values SemiBold with `FontFeatures="tnum"`, dividers. Bottom action bar: Pause `DropDownButton` (`accent`, same five menu items and parameters) / Resume (`accent`), Exit discreet and right-aligned. Remove the `ProgressBar`.
- [ ] **Step 4:** Tests green; screenshots `home-waiting`, `home-moving`, `home-paused` are distinct at a glance, PT `-min` fits, amber face uses dark ink.
- [ ] **Step 5:** `git commit -m "feat(app): redesign Home as the door-hanger state face"`.

### Task 5: Logs, About and custom pause

**Files:** Modify `src/GapMouse.App/Views/LogsView.axaml`, `AboutView.axaml`, `CustomPauseWindow.axaml`, `src/GapMouse.App/Localization/Localizer.Logs.cs`; Test `LocalizerTests.cs` (add case).

**Interfaces:** Consumes `LogsViewModel` (`Lines`, `FilterIndex`, commands), `AboutViewModel`, `CustomPauseViewModel` unchanged; Task 2 converters/classes. Produces `Localizer.LogsEmpty`.

- [ ] **Step 1: Failing test.** `LogsEmpty` = `"No activity yet. Every start, movement, return and pause will appear here."` / `"Nenhuma atividade ainda. Cada início, movimento, retorno e pausa aparece aqui."`
- [ ] **Step 2:** Run → fail; implement the string.
- [ ] **Step 3: Logs.** Toolbar (filter left; Copy, Open folder, Clear right, each with a `Segoe Fluent Icons` glyph: `E8C8`, `E838`, `E74D`); list inside `Border.card`; row = time (`GmFontMono`, muted, tnum) · stamped tag (`Border` 76 px, radius 6, fill/ink from the two converters, 11 SemiBold centred) · message (wraps). Empty state `TextBlock.muted` bound to `L.LogsEmpty`, visible when `Lines.Count` is 0.
- [ ] **Step 4: About.** Header (app icon 48 px, `L.AppName` as `title`, version muted); one `Border.card` with rows License, Repository (+ open button), Settings file and Logs folder (`SelectableTextBlock` path, wraps, + open button), Updates (check button, status, open-release button when available).
- [ ] **Step 5: Custom pause.** Ground background, prompt, TextBox, `TextBlock.error`, OK `accent` + Cancel right-aligned; `IsDefault`/`IsCancel`/click handlers unchanged.
- [ ] **Step 6:** Tests green; screenshots `logs`, `about`, `custom-pause` in all variants: long path and long log line wrap, no horizontal scroll.
- [ ] **Step 7:** `git commit -m "feat(app): restyle Logs, About and the custom pause dialog"`.

### Task 6: Settings as Windows-style setting cards

**Files:** Modify `src/GapMouse.App/Views/SettingsView.axaml` only.

**Interfaces:** Consumes `SettingsViewModel` bindings unchanged and Task 2 classes; keeps `x:Name="HotkeyBox"` + `IsReadOnly="True"` (code-behind `SettingsView.axaml.cs` must compile untouched).

- [ ] **Step 1:** `DockPanel`: bottom bar (outside the scroll) with Save (`accent`), Restore defaults and the status message; `ScrollViewer` with `MaxWidth` 640 column: one `Border.card` per group (Movement, Power, System, Appearance, Logs) under a `TextBlock.group` heading; rows divided by `Border.divider`; label left (with hint/error/warning below it), control right: TextBox 96 px, `ToggleSwitch` (empty On/Off content) for each former CheckBox, ComboBox 180 px; hotkey row keeps its hint, error and warning texts.
- [ ] **Step 2:** Build (proves the code-behind still binds `HotkeyBox`); tests green; screenshots `settings` all variants, PT `-min` labels wrap rather than clip.
- [ ] **Step 3:** `git commit -m "feat(app): redesign Settings as grouped setting cards"`.

### Task 7: Docs and README screenshots

**Files:** Create `docs/images/` PNGs (copied from the tool: `home-dark.png` = dark-en/home-moving, `home-light.png` = light-en/home-waiting, `logs-dark.png`, `settings-dark.png`); Modify `README.md` (Screenshots section near the top using those images; mention `tools/GapMouse.Screenshots`), `CLAUDE.md` (state line: v1.1 visual refresh; `Styles/` folder; `tools/GapMouse.Screenshots`; DESIGN.md/PRODUCT.md pointers; command `dotnet run --project tools/GapMouse.Screenshots`), `.gitignore` only if `artifacts/` is not already ignored.

- [ ] **Step 1:** Regenerate screenshots, copy the four images, update README and CLAUDE.md (CLAUDE.md in Portuguese).
- [ ] **Step 2:** `dotnet format --verify-no-changes`, build, tests green.
- [ ] **Step 3:** `git commit -m "docs: add app screenshots and document the design system"`.
