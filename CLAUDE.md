# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Visão geral

Utilitário Windows de dois arquivos que mantém o PC ativo (evita bloqueio de tela/suspensão) movendo o mouse quando o usuário está ocioso. Não há build, lint, testes nem gerenciador de pacotes. Os comentários e mensagens do código são em português (sem acentos nos arquivos).

- `MOUSE.ps1` — toda a lógica (Windows PowerShell 5.1, usa `System.Windows.Forms` e P/Invoke via `Add-Type`).
- `MOUSE.bat` — launcher: define os parâmetros e chama `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`.

## Executar

```
MOUSE.bat
# ou diretamente:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File MOUSE.ps1 -tempoInatividadeSegundos 120 -intervaloChecagemSegundos 1 -intervaloMovMin 10 -intervaloMovMax 25
```

Parar com Ctrl+C (o bloco `finally` restaura o controle de energia e espera um `Read-Host`).

**Atenção:** `MOUSE.bat` tem `PS1_PATH` fixo em `C:\Users\Gustavo\Desktop\Programas\Mouse\MOUSE.ps1`, não neste diretório do repositório. Alterações feitas aqui só valem ao rodar o `.bat` se esse caminho for ajustado (ou se a cópia for sincronizada). Os valores padrão dos parâmetros no `.ps1` (25/90 s) diferem dos definidos no `.bat` (10/25 s); o `.bat` prevalece.

## Arquitetura (`MOUSE.ps1`)

Máquina de estados em loop infinito alternando entre dois modos:

- **Modo Parado** (`Start-ModoParado`): só observa. Retorna quando `Get-IdleSeconds` (via `GetLastInputInfo`) ≥ `tempoInatividadeSegundos`.
- **Modo Movimento** (`Start-ModoMovimento`): chama `Move-MouseNaturally` (curva Bézier quadrática, 20–40 passos, velocidade variável) e espera um intervalo aleatório entre `intervaloMovMin`/`Max`, checando input a cada `intervaloChecagemSegundos`. Volta ao Modo Parado se detectar uso real.

Pontos não óbvios:

- Mover o cursor com `Cursor.Position` **não** zera o idle do Windows (só input real/`SendInput` zera). O script depende disso: o idle continua subindo durante o movimento sintético, então uso real é detectado quando o idle cai abaixo do tempo decorrido desde o início do movimento (`$idleAtual -lt $segundosDesdeInicio - 0.5`) ou `< 1` s durante a espera.
- Como o movimento sintético não evita suspensão, `SetThreadExecutionState` (`Set-ImpedirSuspensao`, chamado repetidamente nos loops) impede suspensão e desligamento de tela; `Set-PermitirSuspensao` no `finally` desfaz isso.
- Constantes `ES_*` são literais decimais com cast `[uint32]` porque em PS 5.1 `0x80000000` vira Int32 negativo e estoura na conversão — não trocar por hex.
- O movimento se limita aos limites de `PrimaryScreen` (apenas monitor principal).
