# CLAUDE.md

Este arquivo orienta o Claude Code (claude.ai/code) ao trabalhar neste repositório.

## Visão geral

GapMouse é um utilitário .NET 10 para Windows que mantém o PC ativo (evita bloqueio de tela e suspensão) movendo o mouse quando o usuário está ocioso. É a reescrita do script PowerShell original, que continua em `legacy/` (`MOUSE.ps1`, `MOUSE.bat`) apenas como referência.

Estado atual: v1.2 (pausa com a tela bloqueada: o mouse para ao bloquear a sessão, com opção de manter o sistema acordado) sobre a v1.1 (refresh visual: navegação em trilho lateral, face da Home em formato de "door hanger" com cores por estado, temas claro/escuro baseados em tokens) sobre o Plano 4 (distribuição: instalador Inno Setup por usuário, zip portátil, checksums, pipeline de release x64 + ARM64 e release-please), sobre o app desktop do Plano 3 (Avalonia: bandeja, abas Início/Logs/Configurações/Sobre, temas claro/escuro, inglês e português, instância única, janela que esconde na bandeja). O console de desenvolvimento continua existindo. Roadmap: Plano 5 pós-v1 (envio ao winget, assinatura de código).

## Documentação

- Spec: `docs/superpowers/specs/2026-10-31-gapmouse-design.md`
- Planos: `docs/superpowers/plans/`
- ADRs: `docs/adr/` (0001 Avalonia + AOT, 0002 movimento do cursor e power requests, 0003 sem build lite dependente de framework, 0004 núcleo funcional em thread dedicada, 0005 configurações, estado e logs em disco, 0006 integração com o shell do Windows, 0007 distribuição e pipeline de release, 0008 pausa com a tela bloqueada)
- Design: `PRODUCT.md` (produto, público, tom) e `DESIGN.md` (sistema de design, com `.impeccable/design.json`) na raiz

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
  - `Shell/` — integração com o shell: `ShellWindow` (janela de nível superior oculta; recebe as notificações WTS de bloqueio da sessão), `Win32TrayIcon` (ícone e balões), `Win32GlobalHotkey` e `SingleInstance` (mutex + mensagem de ativação).
- `src/GapMouse.App` — app desktop Avalonia 12 (Native AOT, `GapMouse.exe`):
  - `Views/` e `ViewModels/` — telas e view models; os view models **não** referenciam Avalonia (namespaces `GapMouse.App.ViewModels*` e `GapMouse.App.Localization`);
  - `Styles/` — `Tokens.axaml` (tokens de tema `Gm…Brush`, tokens de estado, raios, fontes e overrides dos botões Fluent no tema claro) e `Controls.axaml` (classes de texto/card/linha, template do `TabControl` em trilho lateral, `DropDownButton` de destaque); conversores em `Views/` (`LogKindBrushConverter`, `LogKindInkConverter`, `ProgressToSweepConverter`, `UpperCaseConverter`);
  - `Localization/` — `Localizer` com um arquivo parcial por área;
  - `Services/` — portas de UI e adaptadores Avalonia, `AppShutdown`, `MainWindowService`, `ThemeApplier`;
  - `Composition/` — `AppComposition.Build` (raiz de composição, só fábricas singleton explícitas, seguras para AOT);
  - `StartupOptions` — argumentos de linha de comando (`--minimized`).
- `tools/GapMouse.DevConsole` — console Native AOT só para desenvolvedores (não é distribuído). Guarda configurações, estado (`GapMouse.state.json`) e logs (`logs/`) ao lado do próprio exe (modo portátil fixo), nunca no perfil do usuário. Flags: `--idle`, `--check`, `--min`, `--max`, `--amplitude`, `--display on|off` (padrões de `EngineSettings.Default`: 120/1/10/25/120/on; flag inválida sai com código 2). Teclas: `p` pausa, `t` pausa de 1 min, `r` retoma, `q` sai; Ctrl+C encerra liberando a energia.
- `tools/GapMouse.Screenshots` — Avalonia headless só para desenvolvedores (não é distribuído): renderiza todas as telas nos temas claro e escuro, em inglês e português, a 760×600 e 640×520, em `artifacts/screenshots/`. As imagens do README (`docs/images/`) vêm daqui.
- `build/` — scripts PowerShell de empacotamento (rodam em Windows PowerShell 5.1 e pwsh 7; os `Test-*` servem como testes): `Publish-App.ps1` (publica em AOT um runtime, grava a versão e copia só `GapMouse.exe`, `*.dll` e `LICENSE.txt`), `New-PortableZip.ps1`, `New-Checksums.ps1`, `New-Installer.ps1` (localiza o `ISCC.exe`, nunca instala o Inno), `Test-Package.ps1`, `Test-Portable.ps1` (abre o exe com `--minimized` e exige a linha START no log) e `Test-Installer.ps1` (instalação e desinstalação silenciosas em 3 fases). Saída em `artifacts/` (ignorada pelo git).
- `installer/GapMouse.iss` — Inno Setup 6, por usuário (`{autopf}\GapMouse`), `AppMutex=GapMouse.SingleInstance`, atalho sempre no Menu Iniciar, tarefa "Criar atalho na área de trabalho" (`desktopicon`, marcada), tarefa opcional "Iniciar com o Windows" (desmarcada), inglês e português do Brasil; a desinstalação só apaga o valor Run se ele apontar para o próprio exe.
- `tests/` — `*.Tests` por camada, `GapMouse.Architecture.Tests` (regras de dependência entre camadas) e `GapMouse.Testing` (biblioteca com dublês de teste compartilhados).

## Design: núcleo funcional + host

`KeepAwakeEngine` é puro: recebe `EngineInput` e devolve `EngineStep` (novo estado, eventos, `PowerRequest`). `EngineHost` o executa em **uma única thread dedicada**, faz as chamadas às portas e publica os eventos.

**Regra de energia por thread:** `SetThreadExecutionState` vale por thread. Todas as chamadas de energia (pedir e liberar) devem ocorrer nessa mesma thread dedicada; nunca a partir de outra thread ou de continuações assíncronas.

## Pontos não óbvios

- Mover o cursor (`SetCursorPos` / `Cursor.Position`) **não** zera o contador de ociosidade do Windows (só input real ou `SendInput` zera). O motor depende disso: a ociosidade continua subindo durante o movimento sintético, e uso real é detectado quando ela cai abaixo do tempo decorrido desde o início do movimento (com tolerância de 0,5 s) ou fica < 1 s durante a espera.
- O movimento sintético não evita suspensão; por isso o motor mantém um pedido de energia ativo (`PowerRequest`) enquanto roda.
- Dívida técnica registrada: migrar para `SendInput` + hooks (ver ADR 0002).
- O movimento se limita ao monitor onde o cursor está no início de cada movimento (spec §11.5, implementado no Plano 1).
- `GapMouse.Application` não faz I/O nem referencia `System.Text.Json`, `System.Net.Http` ou `Microsoft.Win32` (garantido por teste de arquitetura); todo acesso a arquivo, rede e registro fica na Infrastructure.
- As flags do DevConsole sobrepõem as configurações salvas apenas naquela execução e **não** são gravadas.
- O DevConsole nunca grava a chave Run do registro (`StartWithWindowsService` não é ligado nele).
- Retenção de logs: um arquivo é apagado quando a data no nome é anterior a hoje − N; com N = 7 ficam hoje e os 7 dias anteriores.
- O descarte do app segue a ordem de `AppShutdown` (ADR 0006): view models, bandeja, atalho, host, `AppStatePersister`, `ActivityLogWriter`, `UpdateService`, `HttpClient`, janela do shell e, por fim, `desktop.Shutdown()` (o persister lê o snapshot, publicado depois que os eventos são disparados). Não salve as configurações após um carregamento que não conseguiu ler o arquivo (`FileExisted=true` com aviso de leitura), senão os padrões sobrescrevem o arquivo do usuário. `UpdateFound` e `EventRaised` disparam fora da thread de UI.
- A janela do shell (`ShellWindow`) roda na mesma thread e no loop de mensagens do Avalonia (thread de UI); não há thread de mensagens separada. Eventos do motor e de atualização chegam aos view models só via `IUiDispatcher`, e todo `Save` de configurações fica na thread de UI.
- Localização: cada texto é `T("English", "Português")` no arquivo parcial `Localizer.<Área>.cs`; o idioma troca ao vivo. Tags de log (START…ERROR) e mensagens em disco ficam em inglês.
- `--minimized` (ou `StartMinimized`) inicia só com a bandeja, sem janela. O registro de "iniciar com o Windows" usa `--minimized`.
- A instância única bloqueia um segundo `dotnet run` enquanto o app estiver aberto: a segunda execução só ativa a janela existente e sai com código 0.
- O encerramento passa sempre por `AppShutdown.Exit()` (uma vez, na ordem do ADR 0006); a janela principal só esconde ao fechar (`MainWindowService`).
- O publish AOT local pode precisar do `vswhere` no PATH; o `Publish-App.ps1` adiciona a pasta do Visual Studio Installer quando falta.
- O valor Run (`HKCU\...\Run`, nome `GapMouse`) gravado pelo instalador deve ser idêntico ao de `StartupCommand.For` (`"<dir>\GapMouse.exe" --minimized`); senão o app o reescreve ao iniciar.
- O publish entrega `GapMouse.exe` mais 3 DLLs nativas, não um único exe (ADR 0007).
- PRs do release-please não rodam o CI (tags e PRs criados com `GITHUB_TOKEN` não disparam workflows); o `release.yml` é chamado por `workflow_call`.
- Pausa com a tela bloqueada (ADR 0008): o bloqueio é um indicador do motor, ortogonal à pausa manual; desbloquear restaura o estado anterior e nunca desfaz uma pausa manual. Uma pausa temporizada pode expirar bloqueada, sem mover o mouse.
- Energia bloqueado: `PowerRequest.None` ou `KeepSystemAwake` (com `KeepAwakeWhileLocked`), nunca a tela; em pausa continua `None`. `SessionLocked.KeepAwake` só é verdadeiro sem pausa e com a opção ligada.
- Bloqueado, o host usa `AdvanceLocked` e não lê cursor nem monitor. As notificações WTS chegam no `ShellWindow` (thread de UI) via `SessionLockChanged` e vão ao motor por `IEngineController.SetSessionLocked`; `ShellWindow.QueryIsSessionLocked` dá o estado inicial.
- O `.gitignore` padrão ignora pastas `Win32/`; há negações (`!src/**/Win32/`, `!tests/**/Win32/`). Ao criar pastas `Win32/` fora de `src/` e `tests/`, ou se arquivos "sumirem" do `git status`, verifique isso.

## Build e testes

```
dotnet build
dotnet test                                              # tudo, incluindo testes Win32 reais
dotnet test --filter-not-trait "Category=Win32"          # como no CI
dotnet format --verify-no-changes
dotnet run --project src/GapMouse.App                  # app desktop (use -- --minimized para só a bandeja)
dotnet run --project tools/GapMouse.DevConsole -- --idle 10 --min 3 --max 5
dotnet run --project tools/GapMouse.Screenshots        # gera os PNGs em artifacts/screenshots/
dotnet publish tools/GapMouse.DevConsole -c Release -r win-x64 -o artifacts/devconsole
```

- O runner é Microsoft.Testing.Platform (`global.json`), então a sintaxe de filtro é `--filter-not-trait`, não o `--filter` do VSTest.
- Testes que usam a API Win32 de verdade levam `[Trait("Category","Win32")]` e movem o cursor 1 px (não mexa no mouse por ~2 s). O CI os exclui.
- CI (`.github/workflows/ci.yml`): `build-test` (format, build Release, testes com cobertura) e `aot-publish` (publica em Native AOT, com avisos como erro, o DevConsole como artefato `devconsole-win-x64` e o app `src/GapMouse.App` como artefato `gapmouse-win-x64`). Há também CodeQL e Dependabot. `release.yml` (tag `v*`, `workflow_dispatch` com tag vazia = ensaio, `workflow_call` e PRs que mexem em arquivos de empacotamento = ensaio): version, build (x64 e ARM64 nativos), installer, installer-test, package (SHA256SUMS), publish e winget. `release-please.yml` abre o PR de release e chama o `release.yml`. Passo a passo em `docs/RELEASING.md`.

## Convenções

- Código, nomes e commits em inglês; esta documentação em português.
- Conventional Commits (`feat:`, `fix:`, `docs:`...). Sem trailers de atribuição de IA nos commits.
- Versões de pacotes centralizadas em `Directory.Packages.props`.
- Avisos tratados como erro (`Directory.Build.props`); `dotnet format` deve passar.
- Código em `src/` deve ser compatível com Native AOT.
- Dependências entre camadas: Domain não depende de nada; Application só de Domain; Infrastructure implementa as portas (verificado pelos testes de arquitetura).
