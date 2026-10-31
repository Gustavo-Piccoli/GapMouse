# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Visão geral

Este repositório está se tornando o GapMouse, um app .NET 10 para Windows que mantém o PC ativo. Design: `docs/superpowers/specs/2026-10-31-gapmouse-design.md`. Plano 1: `docs/superpowers/plans/2026-10-31-gapmouse-plan-1-foundation-engine.md`.

Projetos atuais: `src/GapMouse.Domain`, `src/GapMouse.Application`, `src/GapMouse.Infrastructure` e `tests/*`. Comandos: `dotnet build` e `dotnet test` (o runner de testes é Microsoft.Testing.Platform, habilitado em `global.json`).

Os scripts originais ficam em `legacy/` (`legacy/MOUSE.ps1`, `legacy/MOUSE.bat`). São um utilitário Windows de dois arquivos que mantém o PC ativo (evita bloqueio de tela/suspensão) movendo o mouse quando o usuário está ocioso. Os comentários e mensagens desses scripts são em português (sem acentos nos arquivos).

- `legacy/MOUSE.ps1` — toda a lógica (Windows PowerShell 5.1, usa `System.Windows.Forms` e P/Invoke via `Add-Type`).
- `legacy/MOUSE.bat` — launcher: define os parâmetros e chama `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`. Usa caminho relativo (`%~dp0MOUSE.ps1`).

## Executar (scripts legados)

```
legacy\MOUSE.bat
# ou diretamente:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File legacy/MOUSE.ps1 -tempoInatividadeSegundos 120 -intervaloChecagemSegundos 1 -intervaloMovMin 10 -intervaloMovMax 25
```

Parar com Ctrl+C (o bloco `finally` restaura o controle de energia e espera um `Read-Host`).

Os valores padrão dos parâmetros no `.ps1` (25/90 s) diferem dos definidos no `.bat` (10/25 s); o `.bat` prevalece.

## Arquitetura (`legacy/MOUSE.ps1`)

Máquina de estados em loop infinito alternando entre dois modos:

- **Modo Parado** (`Start-ModoParado`): só observa. Retorna quando `Get-IdleSeconds` (via `GetLastInputInfo`) ≥ `tempoInatividadeSegundos`.
- **Modo Movimento** (`Start-ModoMovimento`): chama `Move-MouseNaturally` (curva Bézier quadrática, 20–40 passos, velocidade variável) e espera um intervalo aleatório entre `intervaloMovMin`/`Max`, checando input a cada `intervaloChecagemSegundos`. Volta ao Modo Parado se detectar uso real.

Pontos não óbvios:

- Mover o cursor com `Cursor.Position` **não** zera o idle do Windows (só input real/`SendInput` zera). O script depende disso: o idle continua subindo durante o movimento sintético, então uso real é detectado quando o idle cai abaixo do tempo decorrido desde o início do movimento (`$idleAtual -lt $segundosDesdeInicio - 0.5`) ou `< 1` s durante a espera.
- Como o movimento sintético não evita suspensão, `SetThreadExecutionState` (`Set-ImpedirSuspensao`, chamado repetidamente nos loops) impede suspensão e desligamento de tela; `Set-PermitirSuspensao` no `finally` desfaz isso.
- Constantes `ES_*` são literais decimais com cast `[uint32]` porque em PS 5.1 `0x80000000` vira Int32 negativo e estoura na conversão — não trocar por hex.
- O movimento se limita aos limites de `PrimaryScreen` (apenas monitor principal).
