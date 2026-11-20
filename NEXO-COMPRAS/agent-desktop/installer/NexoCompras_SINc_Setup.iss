; Script de Instalação Profissional Inno Setup 6
; Nexo Compras — SINc (Sincronizador Local de ERP)
; Compatível com Windows 10/11 e Windows Server (x64)

#define MyAppName "Nexo Compras SINc"
#define MyAppVersion "2.4.0"
#define MyAppPublisher "Nexo Compras Tecnologia Ltda."
#define MyAppURL "https://nexocompras.com.br"
#define MyAppExeName "SINc.exe"
#define MyAppId "{{8B2372A5-88A0-44BC-8F35-1F034B769D9E}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Nexo Compras\SINc
DefaultGroupName=Nexo Compras
AllowNoIcons=yes
OutputDir=C:\Users\sergi\OneDrive\Desktop\NEXO_COMPRAS_TESTE\INSTALADOR
OutputBaseFilename=NexoCompras_SINc_Setup
SetupIconFile=staging\app.ico
UninstallDisplayIcon={app}\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
PrivilegesRequired=admin
DisableWelcomePage=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Iniciar o SINc automaticamente com o Windows (Recomendado)"; GroupDescription: "Inicialização em Segundo Plano:"; Flags: unchecked

[Files]
; Executável Principal Autocontido (Single-File .NET 9 x64)
Source: "staging\SINc.exe"; DestDir: "{app}"; Flags: ignoreversion

; Ícone oficial do aplicativo
Source: "staging\app.ico"; DestDir: "{app}"; Flags: ignoreversion

; Configuração padrão (preserva arquivo se já existir em atualizações)
Source: "staging\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

; Dependências nativas e drivers Firebird/Npgsql
Source: "staging\Dependencias\*"; DestDir: "{app}\Dependencias"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Menu Iniciar
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\Configurar SINc"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--config"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; Atalho na Área de Trabalho (opcional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

; Inicialização automática com o Windows
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\*.tmp"

[Code]
// Proteção de Dados do Cliente na Desinstalação
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
  ConfigFile: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{app}\data');
    ConfigFile := ExpandConstant('{app}\appsettings.json');
    
    if DirExists(DataDir) or FileExists(ConfigFile) then
    begin
      if MsgBox('Deseja remover também os dados de ativação, filas de contingência offline e configurações locais do banco ERP?' + #13#10 + #13#10 + 
                '(Se você pretende atualizar ou reinstalar o SINc futuramente, selecione "NÃO" para manter suas credenciais salvas)', 
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        if DirExists(DataDir) then
          DelTree(DataDir, True, True, True);
        if FileExists(ConfigFile) then
          DeleteFile(ConfigFile);
      end;
    end;
  end;
end;

