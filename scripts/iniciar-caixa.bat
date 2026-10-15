@echo off
REM Inicia o servidor do PDV e abre a tela do caixa em tela cheia (modo quiosque).
REM Coloque este arquivo na mesma pasta do ChurrascariaPDV.exe publicado.
cd /d "%~dp0"
start "Churrascaria PDV - Servidor" /min ChurrascariaPDV.exe
timeout /t 4 /nobreak >nul
start "" msedge --kiosk http://localhost:5000/caixa --edge-kiosk-type=fullscreen
