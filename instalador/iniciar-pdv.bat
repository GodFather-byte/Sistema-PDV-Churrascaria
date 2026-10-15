@echo off
REM Inicia o servidor do PDV e abre o caixa em tela cheia. Configura o estabelecimento se ainda nao foi feito.
cd /d "%~dp0"
if not exist "estabelecimento.db" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0configurar.ps1"
    if not exist "estabelecimento.db" exit /b 1
)
if exist "estabelecimento.db.instalacao-pendente" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0configurar.ps1"
)
tasklist /fi "imagename eq ChurrascariaPDV.exe" | find /i "ChurrascariaPDV.exe" >nul
if errorlevel 1 (
    start "PDV - Servidor (nao feche esta janela)" /min ChurrascariaPDV.exe
    timeout /t 5 /nobreak >nul
)
start "" msedge --kiosk http://localhost:5000/caixa --edge-kiosk-type=fullscreen
