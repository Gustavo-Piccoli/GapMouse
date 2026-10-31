@echo off

REM CAMINHO DO SCRIPT .ps1 ============================================================
set "PS1_PATH=%~dp0MOUSE.ps1"


REM PARAMETROS DO SCRIPT============================================================

set "TEMPO_INATIVIDADE=120"
set "INTERVALO_CHECAGEM=1"
set "INTERVALO_MOV_MIN=10"
set "INTERVALO_MOV_MAX=25"

if not exist "%PS1_PATH%" (
    echo ERRO: nao encontrei o arquivo:
    echo   %PS1_PATH%
    echo.
    echo Edite a linha "set PS1_PATH=..." no topo deste .bat
    echo e aponte para o local correto do manter-pc-ativo.ps1
    echo.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1_PATH%" ^
  -tempoInatividadeSegundos %TEMPO_INATIVIDADE% ^
  -intervaloChecagemSegundos %INTERVALO_CHECAGEM% ^
  -intervaloMovMin %INTERVALO_MOV_MIN% ^
  -intervaloMovMax %INTERVALO_MOV_MAX%

pause
