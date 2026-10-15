; Instalador do Sistema PDV (Inno Setup 6). Gerado pelo GitHub Actions: .github/workflows/instalador.yml
; Compilar localmente: publique em ..\publicacao e rode  ISCC.exe /DVersao=1.0.0 PDV.iss

#ifndef Versao
  #define Versao "0.0.0"
#endif

[Setup]
AppId={{6F1B2C4E-8A3D-4E5F-9B1A-2C7D8E9F0A11}
AppName=Sistema PDV
AppVersion={#Versao}
AppPublisher=Sistema PDV
DefaultDirName={sd}\PDV
DefaultGroupName=Sistema PDV
DisableProgramGroupPage=yes
OutputDir=..\saida
OutputBaseFilename=PDV-Instalador-{#Versao}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
UninstallDisplayName=Sistema PDV
WizardStyle=modern

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Dirs]
; O banco, os backups e a configuracao ficam na pasta do programa; o usuario do caixa precisa gravar nela.
Name: "{app}"; Permissions: users-modify
Name: "{app}\backups"; Flags: uninsneveruninstall

[Files]
Source: "..\publicacao\*"; DestDir: "{app}"; Excludes: "appsettings.json,appsettings.Development.json,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
; A configuracao existente nunca e sobrescrita em atualizacoes.
Source: "..\publicacao\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "configurar.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "iniciar-pdv.bat"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\PDV - Caixa"; Filename: "{app}\iniciar-pdv.bat"; WorkingDir: "{app}"; IconFilename: "{app}\ChurrascariaPDV.exe"
Name: "{group}\PDV - Caixa"; Filename: "{app}\iniciar-pdv.bat"; WorkingDir: "{app}"; IconFilename: "{app}\ChurrascariaPDV.exe"
Name: "{group}\Configurar PDV"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\configurar.ps1"""; WorkingDir: "{app}"
Name: "{group}\Pasta de backups"; Filename: "{app}\backups"
Name: "{group}\Desinstalar Sistema PDV"; Filename: "{uninstallexe}"

[Run]
; Libera a porta para os celulares dos garcons na rede local (somente entrada TCP 5000).
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Sistema PDV"""; Flags: runhidden; StatusMsg: "Configurando o firewall..."
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""Sistema PDV"" dir=in action=allow protocol=TCP localport=5000"; Flags: runhidden
; Primeira instalacao: pede tipo, nome, gerente e PIN pessoal. Atualizacoes preservam o banco.
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\configurar.ps1"""; WorkingDir: "{app}"; Description: "Configurar o estabelecimento agora"; Flags: postinstall waituntilterminated; Check: not Configurado

[UninstallRun]
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Sistema PDV"""; Flags: runhidden; RunOnceId: "RemoverFirewall"

[Messages]
FinishedLabel=O Sistema PDV foi instalado. O banco de dados e os backups ficam em [name] e nao sao apagados ao desinstalar.

[Code]
function Configurado: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\estabelecimento.db'))
    and not FileExists(ExpandConstant('{app}\estabelecimento.db.instalacao-pendente'));
end;
