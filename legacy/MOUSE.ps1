# ============================================================
# Script: Manter PC Ativo (com deteccao de uso real)
# Descricao: Alterna entre dois modos:
#
#   - MODO PARADO: fica so observando. Verifica continuamente
#     se voce esta usando o mouse ou o teclado (deteccao nativa
#     do Windows, via GetLastInputInfo). Se ficar um tempo sem
#     nenhum input seu, passa para o Modo Movimento.
#
#   - MODO MOVIMENTO: move o mouse em um caminho curvo e com
#     velocidade variavel (simulando um movimento humano), em
#     intervalos aleatorios. Durante todo o movimento, continua
#     verificando se voce voltou a usar o mouse/teclado. Se
#     detectar uso real, aborta e volta pro Modo Parado.
#
# Como parar: pressione Ctrl + C nesta janela do PowerShell
# ============================================================

param(
    [int]$tempoInatividadeSegundos = 120,   # 2 minutos parado = considera que voce saiu
    [int]$intervaloChecagemSegundos = 1,    # de quanto em quanto tempo verifica o input
    [int]$intervaloMovMin = 25,             # intervalo minimo entre movimentos (modo movimento)
    [int]$intervaloMovMax = 90              # intervalo maximo entre movimentos (modo movimento)
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# ----------------------------------------------------------
# API do Windows para detectar o ultimo input real do usuario
# (mouse OU teclado), do jeito que o proprio Windows usa
# internamente para decidir quando bloquear a tela.
# ----------------------------------------------------------
$signature = @"
[DllImport("user32.dll")]
public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

[StructLayout(LayoutKind.Sequential)]
public struct LASTINPUTINFO {
    public uint cbSize;
    public uint dwTime;
}
"@
Add-Type -MemberDefinition $signature -Namespace Win32 -Name UserInput

# ----------------------------------------------------------
# API do Windows para impedir suspensao e desligamento de tela.
# Mover o cursor via Cursor.Position (usado no Modo Movimento)
# NAO reseta o contador de energia do Windows -- por isso em
# alguns notebooks a tela apaga e o PC suspende mesmo com o
# script "mexendo" o mouse. SetThreadExecutionState resolve isso
# na raiz, avisando o sistema para nao desligar tela/suspender
# enquanto este processo estiver rodando.
# ----------------------------------------------------------
$power = @"
[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
public static extern uint SetThreadExecutionState(uint esFlags);
"@
Add-Type -MemberDefinition $power -Namespace Win32 -Name PowerManagement

# Usa literais decimais com cast para [uint32]: no Windows PowerShell
# 5.1, o literal hexadecimal 0x80000000 e interpretado como Int32
# negativo e falha ao converter para UInt32 (overflow).
$ES_CONTINUOUS       = [uint32]2147483648  # 0x80000000
$ES_SYSTEM_REQUIRED  = [uint32]1           # 0x00000001
$ES_DISPLAY_REQUIRED = [uint32]2           # 0x00000002

function Set-ImpedirSuspensao {
    [Win32.PowerManagement]::SetThreadExecutionState($ES_CONTINUOUS -bor $ES_SYSTEM_REQUIRED -bor $ES_DISPLAY_REQUIRED) | Out-Null
}

function Set-PermitirSuspensao {
    [Win32.PowerManagement]::SetThreadExecutionState($ES_CONTINUOUS) | Out-Null
}

function Get-IdleSeconds {
    $lastInput = New-Object Win32.UserInput+LASTINPUTINFO
    $lastInput.cbSize = [System.Runtime.InteropServices.Marshal]::SizeOf($lastInput)
    [Win32.UserInput]::GetLastInputInfo([ref]$lastInput) | Out-Null
    $idleMs = [Environment]::TickCount - $lastInput.dwTime
    return [Math]::Round($idleMs / 1000, 1)
}

$tela = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

# ----------------------------------------------------------
# Executa um unico movimento natural (curva Bezier + velocidade
# variavel). Verifica input real a cada passo; se detectar que
# voce mexeu o mouse/teclado por conta propria, aborta na hora.
# Retorna $true se completou o movimento, $false se foi abortado.
# ----------------------------------------------------------
function Move-MouseNaturally {
    $origem = [System.Windows.Forms.Cursor]::Position

    # Guarda o idle no exato momento em que o movimento comeca.
    # [Cursor]::Position NAO conta como input real para o Windows
    # (so SendInput/eventos reais de mouse/teclado zeram o idle),
    # entao o idle do sistema continua subindo normalmente durante
    # todo o movimento sintetico do script. Para detectar um input
    # SEU no meio do caminho, verificamos se o idle caiu perto de
    # zero -- se caiu, so pode ter sido voce mexendo de verdade.
    $marcaTempo = Get-Date

    $deltaX = Get-Random -Minimum -120 -Maximum 120
    $deltaY = Get-Random -Minimum -120 -Maximum 120

    $destinoX = [Math]::Max(0, [Math]::Min($tela.Width - 1, $origem.X + $deltaX))
    $destinoY = [Math]::Max(0, [Math]::Min($tela.Height - 1, $origem.Y + $deltaY))

    $controleX = ($origem.X + $destinoX) / 2 + (Get-Random -Minimum -60 -Maximum 60)
    $controleY = ($origem.Y + $destinoY) / 2 + (Get-Random -Minimum -60 -Maximum 60)

    $passos = Get-Random -Minimum 20 -Maximum 40

    for ($i = 0; $i -le $passos; $i++) {
        # O idle so pode ter vindo de um input seu se ele estiver
        # MENOR do que o tempo que ja passou desde que este
        # movimento comecou (ou seja, foi resetado no meio do
        # caminho por voce, e nao pelo script).
        $segundosDesdeInicio = ((Get-Date) - $marcaTempo).TotalSeconds
        $idleAtual = Get-IdleSeconds
        if ($idleAtual -lt ($segundosDesdeInicio - 0.5)) {
            return $false
        }

        $t = $i / $passos

        $x = [Math]::Pow(1 - $t, 2) * $origem.X + 2 * (1 - $t) * $t * $controleX + [Math]::Pow($t, 2) * $destinoX
        $y = [Math]::Pow(1 - $t, 2) * $origem.Y + 2 * (1 - $t) * $t * $controleY + [Math]::Pow($t, 2) * $destinoY

        [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]$x, [int]$y)

        $velocidadeBase = 8
        $variacao = [Math]::Sin($t * [Math]::PI) * 6
        Start-Sleep -Milliseconds ($velocidadeBase + (Get-Random -Minimum 0 -Maximum 5) - $variacao)
    }

    Start-Sleep -Milliseconds (Get-Random -Minimum 100 -Maximum 500)
    return $true
}

# ----------------------------------------------------------
# MODO PARADO: so observa, ate detectar 2 minutos sem input seu
# Retorna quando estiver na hora de passar pro Modo Movimento
# ----------------------------------------------------------
function Start-ModoParado {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] | [PARADO]  | Aguardando $tempoInatividadeSegundos Segundos de Inatividade;" -ForegroundColor Cyan

    while ($true) {
        Set-ImpedirSuspensao
        $idle = Get-IdleSeconds
        if ($idle -ge $tempoInatividadeSegundos) {
            return
        }
        Start-Sleep -Seconds $intervaloChecagemSegundos
    }
}

# ----------------------------------------------------------
# MODO MOVIMENTO: fica movendo o mouse periodicamente, checando
# a cada movimento e a cada espera se voce voltou a usar o PC
# ----------------------------------------------------------
function Start-ModoMovimento {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] | [MOVENDO] | Movendo o Mouse a cada $intervaloMovMin - $intervaloMovMax Segundos;" -ForegroundColor Green

    while ($true) {
        Set-ImpedirSuspensao
        $completou = Move-MouseNaturally
        if (-not $completou) {
            return  # detectou uso real durante o movimento -> volta pro modo parado
        }

        # Espera um intervalo aleatorio, checando input a cada segundo
        $espera = Get-Random -Minimum $intervaloMovMin -Maximum $intervaloMovMax
        for ($s = 0; $s -lt $espera; $s++) {
            Set-ImpedirSuspensao
            if ((Get-IdleSeconds) -lt 1) {
                return  # detectou uso real durante a espera -> volta pro modo parado
            }
            Start-Sleep -Seconds $intervaloChecagemSegundos
        }
    }
}

# ----------------------------------------------------------
# Loop principal: alterna entre os dois modos
# ----------------------------------------------------------
Write-Host "[$(Get-Date -Format 'HH:mm:ss')] | [INICIO]  | Script Iniciado (Pressione Ctrl + C para Parar);" -ForegroundColor Yellow

try {
    Set-ImpedirSuspensao
    while ($true) {
        Start-ModoParado
        Start-ModoMovimento
    }
}
finally {
    # Ao parar o script (Ctrl+C ou fechamento), devolve ao Windows
    # o controle normal de energia (volta a poder suspender/desligar
    # a tela quando ficar ocioso, como de costume).
    Set-PermitirSuspensao
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] |  [FIM]    | Script Interrompido;" -ForegroundColor Red
    Read-Host ""
}
