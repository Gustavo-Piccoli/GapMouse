# 8. Pausa com a tela bloqueada

## Context

Com Win+L o GapMouse continuava movendo o mouse na cortina da tela de bloqueio e, na tela do
PIN (área de trabalho segura), as chamadas ao cursor falhavam com acesso negado, gerando
linhas `WARN Port Cursor failed` no log. Com a sessão bloqueada não há motivo para mexer o
mouse; manter o PC acordado só faz sentido em casos específicos (um download longo, por
exemplo).

## Decision

- **O bloqueio é um indicador do motor, ortogonal ao estado.** `KeepAwakeEngine` ganha
  `SetSessionLocked(bool, now)` (idempotente) e `AdvanceLocked(now)`, que só precisa do
  horário: o host não lê cursor nem monitor enquanto bloqueado. Waiting, Moving e Paused
  continuam existindo; desbloquear volta ao estado de antes, e uma pausa manual nunca é
  desfeita.
- **Energia bloqueado.** Por padrão o pedido de energia é solto (`PowerRequest.None`) e o
  Windows segue o plano normal. Com `EngineSettings.KeepAwakeWhileLocked` (JSON
  `keepAwakeWhileLocked`, padrão `false`) o motor pede `KeepSystemAwake`, nunca a tela. Em
  pausa a energia continua `None`.
- **Fluxo normal de eventos.** `SessionLocked(At, KeepAwake)` e `SessionUnlocked(At)` viram
  as linhas `LOCKED` e `UNLOCKED` (`LogKind.Locked/Unlocked`). `KeepAwake` só é verdadeiro
  quando não está pausado e a opção está ligada.
- **Notificação no shell.** `ShellWindow` assina as notificações WTS de sessão, expõe
  `SessionLockChanged` e `QueryIsSessionLocked()` (estado inicial); o app as encaminha a
  `IEngineController.SetSessionLocked`, que é enfileirado para a thread do motor.
- **Log.** A recuperação de porta passa a ser registrada em Warning (par da falha) e o
  `ActivityLogger` deixou de repetir a mensagem da exceção.

## Alternatives

- **B. Bloqueio no `EngineHost`.** Menos mudança no motor, mas a regra fica no código
  imperativo, mais difícil de testar, e estatísticas e eventos ficam fora do fluxo.
- **C. Reaproveitar Pausar/Retomar.** Descartada: desbloquear desfaria a pausa manual do
  usuário.

## Consequences

- Nenhuma linha WARN de cursor esperada durante o bloqueio.
- Uma pausa temporizada pode expirar bloqueada, sem mover o mouse até desbloquear.
- Com a opção ligada só o sistema fica acordado; a tela pode apagar.
- O DevConsole não detecta bloqueio (não tem janela de mensagens).
- Arquivos de configurações antigos, sem o campo novo, assumem o padrão.
