# Product

<!-- impeccable:product-schema 1 -->

> Interview substitution: the owner asked for a fully automatic run while away, so every fact below is
> inferred from the repository (spec, README, code) and the owner's earlier messages. Inferred facts are
> marked *(inferred)*; facts the owner stated are unmarked.

## Platform

windows

Native Windows desktop app (Avalonia 12, Fluent controls, Native AOT), x64 and ARM64. Not a web app: the
controls are the platform's desktop controls, and the app lives mostly in the notification area (tray).

## Users

- The owner, Gustavo, a developer who works remotely and needs the PC to stay unlocked and "Available" in
  Teams while he reads, thinks, or steps away briefly.
- People who download it from GitHub Releases or winget: office and remote workers on Windows laptops and
  desktops with the same need *(inferred from the README and the distribution plan)*.
- Visitors evaluating the repository as a portfolio piece, who look at screenshots and open the app once
  *(inferred: the project is a portfolio showcase)*.

## Product Purpose

GapMouse keeps a Windows PC awake (no screen lock, no sleep) by moving the mouse cursor slightly only while
the user is idle, and getting out of the way the instant real input returns. It replaces the owner's
original PowerShell script. Success is that the user forgets it exists: it starts with Windows, sits in the
tray, and the PC never locks during a working session.

## Positioning

It only acts in the gaps: nothing happens while you work; after a configurable idle time it drifts the
cursor along a gentle randomized path and stops the moment you touch the mouse or keyboard. It holds a
power request while running, pauses on demand (10 min, 30 min, 1 h, custom, until resumed), and keeps a
plain activity log of every start, movement, return and pause.

## Operating Context

- Lives in the notification area; the tray icon shows the state (waiting, moving, paused). A global hotkey
  toggles pause. The main window is opened occasionally to check status, read the log, or change settings,
  then closed back to the tray.
- Glanced at, not studied: the Home screen must answer "is it doing its job right now?" in one look.
- Runs all day on work machines, often on a secondary monitor, in offices and home offices, in both light
  and dark Windows themes *(inferred)*.

## Capabilities and Constraints

- Screens: Home (state, idle progress, recent activity, today's totals, pause/resume/exit), Logs (filterable
  activity log, copy, open folder, clear), Settings (movement timing, power, system, appearance, logs),
  About (version, license, paths, update check), and a custom-pause dialog.
- Two themes (dark default, light) and two languages (English, Brazilian Portuguese) switch live; every
  string has an English and a Portuguese form of different lengths.
- Native AOT: no runtime reflection-based UI tricks; compiled bindings; views reference only view models.
- Log tags (START, WAITING, MOVING, RETURN, PAUSED, RESUMED, CONFIG, STOP, WARN, ERROR) stay in English.
- Window: 760×600 default, 640×520 minimum.

## Brand Commitments

- Name: GapMouse (GAP = Gustavo Auad Piccoli). Existing app and tray icons in
  `src/GapMouse.App/Assets/Icons/` (gapmouse, tray-waiting, tray-moving, tray-paused).
- State colours already used by the tray and Home: waiting = blue, moving = green, paused = amber
  *(inferred as a commitment: the tray icons encode them)*.

## Evidence on Hand

- Real behaviour and copy in the app and its localizer; real log format (`HH:mm:ss | TAG | message`).
- No user counts, testimonials or benchmarks exist; none may be invented.

## Product Principles

1. Invisible when working: the app never competes with the user's real work for attention.
2. State at a glance: waiting, moving or paused must be unmistakable from across the desk.
3. Honest record: the log tells exactly what happened and when, in plain words.
4. Native and light: it feels like a well-made Windows utility, starts fast, and stays small.

## Accessibility & Inclusion

- State is never carried by colour alone (text label plus colour) *(inferred, required by principle 2)*.
- Keyboard reachable: every action works without a mouse, since the app's subject is the mouse
  *(inferred)*.
- Both themes meet WCAG AA contrast for text *(inferred)*.
