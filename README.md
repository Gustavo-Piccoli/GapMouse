# GapMouse

[![CI](https://github.com/Gustavo-Piccoli/GapMouse/actions/workflows/ci.yml/badge.svg)](https://github.com/Gustavo-Piccoli/GapMouse/actions/workflows/ci.yml)
[![CodeQL](https://github.com/Gustavo-Piccoli/GapMouse/actions/workflows/codeql.yml/badge.svg)](https://github.com/Gustavo-Piccoli/GapMouse/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Keeps your Windows PC awake and your status available while you're away — light, private, no admin required.

> 🚧 **Under development.** The keep-awake engine and a developer console are working; the desktop app (tray, settings UI) is next. See the [roadmap](#roadmap).

## Build from source

Prerequisite: the [.NET SDK](https://dotnet.microsoft.com/download) 10.0.300 or newer (pinned in `global.json`), on Windows.

```powershell
dotnet build
dotnet test
```

`dotnet test` runs everything, including the tests that call the real Win32 APIs. These move the cursor by 1 px, so keep your hands off the mouse for a couple of seconds. To skip them (as CI does):

```powershell
dotnet test --filter-not-trait "Category=Win32"
```

Tests run on Microsoft.Testing.Platform, hence the `--filter-not-trait` syntax.

### Developer console

A small Native AOT console app that drives the engine and prints its events. It is a developer tool and is not shipped.

```powershell
dotnet run --project tools/GapMouse.DevConsole -- --idle 10 --min 3 --max 5
dotnet publish tools/GapMouse.DevConsole -c Release -r win-x64 -o artifacts/devconsole
```

Flags: `--idle <s>`, `--check <s>`, `--min <s>`, `--max <s>`, `--amplitude <px>`, `--display on|off`. Keys: `p` pause, `t` pause for 1 minute, `r` resume, `q` quit (Ctrl+C also exits cleanly).

## Architecture

Clean Architecture with a functional core:

- `GapMouse.Domain` — pure logic: the `KeepAwakeEngine` state machine, movement planning, settings validation. No I/O.
- `GapMouse.Application` — ports (cursor, idle time, monitor bounds, power requests) and `EngineHost`, which runs the engine on a dedicated thread.
- `GapMouse.Infrastructure` — Win32 adapters for those ports.

Design decisions are recorded as ADRs in [`docs/adr/`](docs/adr/). The original PowerShell script lives in [`legacy/`](legacy/).

## Roadmap

1. Foundation and headless engine (current).
2. Persistence and system integration.
3. Avalonia desktop app: tray, tabs, themes, i18n.
4. Distribution: installer, portable build, winget.

## Disclaimer

Using this tool on a corporate machine is your own responsibility. Check your company's policies before running it.

## Privacy

No telemetry. The only network call is an optional update check.

## License

[MIT](LICENSE)
