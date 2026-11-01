# GapMouse: pausa com a tela bloqueada — design

Data: 2026-11-01. Release alvo: 1.2.0 (próximo `feat:` depois do v1.1.0).

## 1. Intenção

Hoje, com Win+L, o GapMouse continua mexendo o mouse na "cortina" da tela de bloqueio e, na tela do PIN (área de trabalho segura), as chamadas ao cursor falham e o log mostra `WARN Port Cursor failed: Acesso negado.: Acesso negado.`. Com a tela bloqueada não há motivo para mexer o mouse (o Teams fica "Ausente" de qualquer forma), e manter o PC acordado só faz sentido em casos específicos (um download longo, por exemplo).

O dono decidiu:

- Ao bloquear a sessão, o GapMouse para de mexer o mouse.
- Configuração nova para o tempo bloqueado: **pausar tudo** (padrão: solta o pedido de energia e o Windows segue o plano de energia normal) ou **manter o PC acordado** (não mexe o mouse, mas mantém o sistema acordado).
- Em "manter acordado", só o sistema fica acordado; a tela pode apagar, mesmo com "Manter a tela ligada" marcado.
- Linhas `LOCKED` / `UNLOCKED` no log de atividade.
- Corrigir a mensagem duplicada (`Acesso negado.: Acesso negado.`).
- Registrar no log de atividade a linha de recuperação da porta.
- Abordagem escolhida: **A** (o bloqueio é parte do motor), ver §4.

## 2. Objetivos, não objetivos, antiobjetivos

Objetivos:
- Bloquear (Win+L, bloqueio automático, tampa/suspensão que bloqueia, troca rápida de usuário que bloqueia) para o movimento na hora.
- Desbloquear volta exatamente ao estado de antes: aguardando, ou na pausa manual se ela ainda vale.
- Energia durante o bloqueio segue a configuração: `PowerRequest.None` (padrão) ou `PowerRequest.KeepSystemAwake`.
- Nenhuma linha WARN esperada durante o bloqueio por causa do cursor inacessível.

Não objetivos:
- Novo estado visual na tela Início ou no ícone da bandeja (ninguém vê a tela com a sessão bloqueada).
- Detecção de bloqueio no DevConsole (não tem janela de mensagens; continua como hoje).
- Migração para `SendInput` + hooks (dívida técnica do ADR 0002, continua registrada).

Antiobjetivos (seria falha mesmo funcionando):
- Desbloquear desfazer uma pausa manual do usuário (inclusive "até retomar").
- Uma pausa temporizada que expira durante o bloqueio fazer o mouse voltar a mexer antes do desbloqueio.
- Chamar a API de energia fora da thread do motor (regra de energia por thread, CLAUDE.md).
- Um arquivo de configurações antigo (sem o campo novo) ser tratado como inválido e trocado pelos padrões.

## 3. Restrições

- Camadas: Domain puro, sem I/O; Application só portas e lógica, sem `Microsoft.Win32`/`System.Text.Json`/`System.Net.Http` (teste de arquitetura); Win32 só na Infrastructure.
- Native AOT, avisos como erro, `dotnet format`.
- Localização: todo texto de UI novo é `T("English", "Português")` no parcial da área, com caso em `LocalizerTests`; tags e mensagens de log em inglês.
- Configurações: JSON com geração de código; campo ausente assume o padrão. (Dívida conhecida: campo com tipo errado invalida o arquivo inteiro; por isso o campo novo é `bool`.)
- Cores de tag só pela tabela fixa dos conversores (spec do visual refresh §6.1) e tokens.

## 4. Abordagens consideradas

- **A. Bloqueio no motor (escolhida).** `KeepAwakeEngine` ganha um indicador de sessão bloqueada, ortogonal ao estado (Waiting/Moving/Paused). Testável com testes puros, energia continua decidida pelo motor e aplicada na thread dele, eventos `LOCKED`/`UNLOCKED` seguem o fluxo normal (log, persister).
- **B. Bloqueio no `EngineHost`.** O host deixa de rodar o motor e ajusta a energia sozinho. Menos mudança no motor, mas a regra fica no código imperativo, mais difícil de testar, e estatísticas/eventos ficam fora do fluxo.
- **C. Reaproveitar Pausar/Retomar.** Descartada: desbloquear desfaria a pausa manual do usuário.

## 5. Comportamento (o que o dono decidiu, mais as decisões marcadas)

### 5.1 Motor (Domain)

- `EngineSettings` ganha `KeepAwakeWhileLocked` (`bool`, padrão `false`), também em `Create(...)`.
- `KeepAwakeEngine.SetSessionLocked(bool locked, DateTimeOffset now)`: idempotente (repetir o mesmo valor não gera evento).
  - Ao bloquear: se estiver em Moving, abandona o caminho e vai para Waiting **sem** `UserReturned` (não houve retorno do usuário); emite `SessionLocked(At, KeepAwake)`.
  - Ao desbloquear: emite `SessionUnlocked(At)`; se o estado é Waiting, emite também `WaitingStarted` (o contador recomeça). Se é Paused, continua pausado.
- Enquanto bloqueado, `Advance`:
  - nunca move o cursor nem entra em Moving;
  - em Waiting, devolve `PowerRequest.KeepSystemAwake` se `KeepAwakeWhileLocked`, senão `PowerRequest.None`;
  - em Paused, a energia é `None` como hoje, bloqueado ou não. **Decisão do controlador:** a pausa manual vence, porque pausar significa "deixe o Windows agir";
  - continua expirando pausas temporizadas (emite `EngineResumed(PauseExpired)`), mas o estado resultante (Waiting) segue sem mexer até desbloquear;
  - não soma tempo de movimento.
- O snapshot ganha `IsSessionLocked`.
- O motor não depende de cursor/monitor enquanto bloqueado: deve existir um caminho de avanço que só precisa do horário (por exemplo, `EngineInput` com cursor/monitor opcionais ou um método `AdvanceLocked(now)` — escolha do builder), para o host não precisar ler o cursor durante o bloqueio.

### 5.2 Host (Application)

- `IEngineController`/`EngineHost` ganham `SetSessionLocked(bool)`, enfileirado como os demais comandos (executa na thread do motor).
- Enquanto o motor está bloqueado, o host não lê cursor nem monitor (só idle, se o caminho escolhido precisar, ou só o horário). Resultado: nada de `WARN Port Cursor failed` esperado durante o bloqueio. Se alguma leitura ainda falhar, a energia continua sendo a do bloqueio (não cai para "nenhuma" nem fica presa em "sistema e tela").
- Recuperação de porta: a linha `Port {Port} recovered` passa a aparecer no log de atividade. **Decisão do controlador:** subir o nível para Warning (vira `WARN Port Cursor recovered`, par da falha). Alternativa aceitável ao builder, se mais limpa: um `EventId` específico que o `ActivityLogger` espelha mesmo em Information.

### 5.3 Log de atividade

- `LogKind` ganha `Locked` e `Unlocked`; tags `LOCKED` e `UNLOCKED` (inglês).
- Mensagens (inglês, decisão do controlador):
  - `Screen locked; movement stopped, power released` (padrão) ou `Screen locked; movement stopped, system kept awake`;
  - `Screen unlocked`.
- Leitura de logs antigos/novos: o parser de linhas deve reconhecer as tags novas; uma tag desconhecida continua tratada como hoje.
- Cor da tag (decisão do controlador): família âmbar da pausa (mesma tinta de `PAUSED`/`RESUMED`), e o filtro "Pausas" passa a incluir `Locked`/`Unlocked`. A tag precisa caber nos 76 px em 11 px SemiBold (verificar na captura).
- `ActivityLogger`: só acrescenta `": " + exception.Message` quando a mensagem formatada ainda não contém esse texto. Resultado: `WARN Port Cursor failed: Acesso negado.` uma vez.

### 5.4 Shell e composição (Infrastructure + App)

- `ShellWindow` registra `WTSRegisterSessionNotification(hwnd, NOTIFY_FOR_THIS_SESSION)` ao criar, cancela no `Dispose` (`WTSUnRegisterSessionNotification`), trata `WM_WTSSESSION_CHANGE` com `WTS_SESSION_LOCK`/`WTS_SESSION_UNLOCK` e expõe um evento `SessionLockChanged(bool locked)`. O handler segue o padrão atual (exceções contidas, `HandlerFailed`).
- Estado inicial: ao iniciar, consulta se a sessão já está bloqueada (`WTSQuerySessionInformation` com `WTSSessionInfoEx`, campo de lock state; atenção à inversão conhecida desse campo no Windows 7/Server 2008 R2 — irrelevante aqui, o app exige Windows 10+, mas documente). Se a consulta falhar, assume desbloqueado.
- `AppComposition` liga `SessionLockChanged` a `EngineHost.SetSessionLocked`. Isso não passa por `IUiDispatcher` (é comando para o motor, não atualização de view model).
- Ordem de descarte (ADR 0006) não muda; o cancelamento do registro WTS acontece no `Dispose` da `ShellWindow`, que já está no fim.

### 5.5 Configurações (App)

- `AppSettings`/`SettingsDocument`/`JsonSettingsStore`: campo `keepAwakeWhileLocked` (`bool`, padrão `false`). Arquivo sem o campo carrega com o padrão e sem aviso.
- Tela Configurações, grupo Energia, logo abaixo de "Manter a tela ligada": `ToggleSwitch` com rótulo T("Keep the PC awake while the screen is locked", "Manter o PC acordado com a tela bloqueada") e legenda `caption` T("The mouse stays still while locked; the screen can turn off.", "O mouse fica parado com a tela bloqueada; a tela pode desligar."). Segue o padrão de linha e o `AutomationProperties.Name` dos demais toggles. Mudar a opção aplica ao motor como as outras (`ApplySettings`) e gera a linha `CONFIG` de sempre.
- A linha `CONFIG` (formato atual de `SettingsChanged`) inclui o valor novo.

## 6. Verificação

- Testes de domínio: bloquear em Waiting/Moving/Paused; desbloquear volta ao estado certo; pausa manual "até retomar" sobrevive a bloquear+desbloquear; pausa temporizada expira bloqueada e não move até desbloquear; energia bloqueada com cada valor da opção; idempotência; sem tempo de movimento bloqueado; sem `UserReturned` ao bloquear.
- Testes de Application: host não lê cursor/monitor bloqueado (dublês contam chamadas); energia correta mesmo com falha de leitura; recuperação aparece no log de atividade; `ActivityLogger` não duplica a mensagem; formatter gera as mensagens de §5.3; parser reconhece as tags.
- Testes de Infrastructure: JSON sem o campo → padrão; ida e volta com o campo. A `ShellWindow` com WTS pode ter teste `Category=Win32` de registro/cancelamento; o bloqueio real fica para verificação manual.
- App: `LocalizerTests` para as strings novas; conversores de tag cobrem `Locked`/`Unlocked`; filtro "Pausas" inclui as tags novas; capturas (`tools/GapMouse.Screenshots`) mostram a linha nova em Configurações e as tags nos Logs, sem cortar a 640×520 em EN/PT (incluir linhas LOCKED/UNLOCKED nos dados de exemplo).
- Manual (dono): Win+L com cada opção; suspender/retomar o notebook; pausa manual + bloqueio; log sem a mensagem duplicada.
- Docs: CLAUDE.md (ponto não óbvio: bloqueio é ortogonal à pausa; energia bloqueada), README (feature), ADR curto `0008` sobre a decisão da abordagem A e do padrão "pausar tudo".

## 7. Deixado ao builder

- A forma exata do avanço sem cursor (§5.1, último item).
- Warning vs `EventId` espelhado para a recuperação (§5.2).
- Nomes internos de tipos e métodos, desde que os de §5 que outros componentes consomem (`SetSessionLocked`, `KeepAwakeWhileLocked`, `IsSessionLocked`, `SessionLocked`/`SessionUnlocked`, `LogKind.Locked`/`Unlocked`) fiquem como estão.
- Divisão em tarefas e commits (Conventional Commits; pelo menos um `feat:` para o release 1.2.0).
