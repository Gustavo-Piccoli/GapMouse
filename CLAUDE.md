# CLAUDE.md

Este arquivo orienta o Claude Code (claude.ai/code) ao trabalhar neste repositório.

## Visão geral

GapMouse é um utilitário .NET 10 para Windows que mantém o PC ativo (evita bloqueio de tela e suspensão) movendo o mouse quando o usuário está ocioso. É a reescrita do script PowerShell original, que continua em `legacy/` (`MOUSE.ps1`, `MOUSE.bat`) apenas como referência.

Estado atual: Plano 2 (persistência de configurações e estado, logs em arquivo, início com o Windows, verificação de atualização; o app ainda é só o console de desenvolvimento). Roadmap: Plano 3 app desktop Avalonia (bandeja, abas, temas, i18n); Plano 4 distribuição (instalador, portátil, winget).

## Documentação

- Spec: `docs/superpowers/specs/2026-10-31-gapmouse-design.md`
- Planos: `docs/superpowers/plans/`
- ADRs: `docs/adr/` (0001 Avalonia + AOT, 0002 movimento do cursor e power requests, 0003 sem build lite dependente de framework, 0004 núcleo funcional em thread dedicada, 0005 configurações, estado e logs em disco)

## Projetos e responsabilidades

- `src/GapMouse.Domain` — núcleo puro e síncrono: `KeepAwakeEngine` (máquina de estados), planejamento de trajetória (`MovementPathPlanner`), `EngineSettings` e validação. Sem I/O.
- `src/GapMouse.Application` — portas em `Abstractions/` (cursor, ociosidade, limites de monitor, power requests) e `EngineHost`, que executa o motor. Pastas do Plano 2 (sem I/O, só portas e lógica):
  - `Settings/` — `AppSettings` e `ISettingsStore`;
  - `State/` — `AppState`, `IAppStateStore` e `AppStatePersister`;
  - `Paths/` — `AppPaths` (portátil vs. instalado);
  - `Logging/` — `IActivityLog`, `LogEntry`, `LogEntryFormatter`, `ActivityLogger<T>`, `ActivityLogWriter`;
  - `Startup/` — `IStartupRegistrar` e `StartWithWindowsService`;
  - `Updates/` — `IReleaseSource` e `UpdateService`.
- `src/GapMouse.Infrastructure` — adaptadores Win32 em `Win32/`, fonte de aleatoriedade e implementações das portas do Plano 2: `Settings/` e `State/` (JSON com geração de código), `Logging/` (`FileActivityLog`), `Startup/` (chave Run do registro) e `Updates/` (`GitHubReleaseSource`).
- `tools/GapMouse.DevConsole` — console Native AOT só para desenvolvedores (não é distribuído). Guarda configurações, estado (`GapMouse.state.json`) e logs (`logs/`) ao lado do próprio exe (modo portátil fixo), nunca no perfil do usuário. Flags: `--idle`, `--check`, `--min`, `--max`, `--amplitude`, `--display on|off` (padrões de `EngineSettings.Default`: 120/1/10/25/120/on; flag inválida sai com código 2). Teclas: `p` pausa, `t` pausa de 1 min, `r` retoma, `q` sai; Ctrl+C encerra liberando a energia.
- `tests/` — `*.Tests` por camada, `GapMouse.Architecture.Tests` (regras de dependência entre camadas) e `GapMouse.Testing` (biblioteca com dublês de teste compartilhados).

## Design: núcleo funcional + host

`KeepAwakeEngine` é puro: recebe `EngineInput` e devolve `EngineStep` (novo estado, eventos, `PowerRequest`). `EngineHost` o executa em **uma única thread dedicada**, faz as chamadas às portas e publica os eventos.

**Regra de energia por thread:** `SetThreadExecutionState` vale por thread. Todas as chamadas de energia (pedir e liberar) devem ocorrer nessa mesma thread dedicada; nunca a partir de outra thread ou de continuações assíncronas.

## Pontos não óbvios

- Mover o cursor (`SetCursorPos` / `Cursor.Position`) **não** zera o contador de ociosidade do Windows (só input real ou `SendInput` zera). O motor depende disso: a ociosidade continua subindo durante o movimento sintético, e uso real é detectado quando ela cai abaixo do tempo decorrido desde o início do movimento (com tolerância de 0,5 s) ou fica < 1 s durante a espera.
- O movimento sintético não evita suspensão; por isso o motor mantém um pedido de energia ativo (`PowerRequest`) enquanto roda.
- Dívida técnica registrada: migrar para `SendInput` + hooks (ver ADR 0002).
- O movimento se limita ao monitor principal.
- `GapMouse.Application` não faz I/O nem referencia `System.Text.Json`, `System.Net.Http` ou `Microsoft.Win32` (garantido por teste de arquitetura); todo acesso a arquivo, rede e registro fica na Infrastructure.
- As flags do DevConsole sobrepõem as configurações salvas apenas naquela execução e **não** são gravadas.
- O DevConsole nunca grava a chave Run do registro (`StartWithWindowsService` não é ligado nele).
- Retenção de logs: um arquivo é apagado quando a data no nome é anterior a hoje − N; com N = 7 ficam hoje e os 7 dias anteriores.
- Para quem compuser o app (Plano 3): descarte na ordem host → `AppStatePersister` → `ActivityLogWriter` → `UpdateService` → `HttpClient` (o persister lê o snapshot, publicado depois que os eventos são disparados). Não salve as configurações após um carregamento que não conseguiu ler o arquivo (`FileExisted=true` com aviso de leitura), senão os padrões sobrescrevem o arquivo do usuário. `UpdateFound` e `EventRaised` disparam fora da thread de UI.
- O `.gitignore` padrão ignora pastas `Win32/`; há negações (`!src/**/Win32/`, `!tests/**/Win32/`). Ao criar pastas `Win32/` fora de `src/` e `tests/`, ou se arquivos "sumirem" do `git status`, verifique isso.

## Build e testes

```
dotnet build
dotnet test                                              # tudo, incluindo testes Win32 reais
dotnet test --filter-not-trait "Category=Win32"          # como no CI
dotnet format --verify-no-changes
dotnet run --project tools/GapMouse.DevConsole -- --idle 10 --min 3 --max 5
dotnet publish tools/GapMouse.DevConsole -c Release -r win-x64 -o artifacts/devconsole
```

- O runner é Microsoft.Testing.Platform (`global.json`), então a sintaxe de filtro é `--filter-not-trait`, não o `--filter` do VSTest.
- Testes que usam a API Win32 de verdade levam `[Trait("Category","Win32")]` e movem o cursor 1 px (não mexa no mouse por ~2 s). O CI os exclui.
- CI (`.github/workflows/ci.yml`): `build-test` (format, build Release, testes com cobertura) e `aot-publish` (publica o DevConsole em Native AOT com avisos como erro e envia o artefato `devconsole-win-x64`). Há também CodeQL e Dependabot.

## Convenções

- Código, nomes e commits em inglês; esta documentação em português.
- Conventional Commits (`feat:`, `fix:`, `docs:`...). Sem trailers de atribuição de IA nos commits.
- Versões de pacotes centralizadas em `Directory.Packages.props`.
- Avisos tratados como erro (`Directory.Build.props`); `dotnet format` deve passar.
- Código em `src/` deve ser compatível com Native AOT.
- Dependências entre camadas: Domain não depende de nada; Application só de Domain; Infrastructure implementa as portas (verificado pelos testes de arquitetura).
