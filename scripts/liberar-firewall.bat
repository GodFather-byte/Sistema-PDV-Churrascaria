@echo off
REM Execute como administrador uma única vez para os celulares acessarem o PDV pela rede Wi-Fi.
netsh advfirewall firewall add rule name="Churrascaria PDV" dir=in action=allow protocol=TCP localport=5000
pause
