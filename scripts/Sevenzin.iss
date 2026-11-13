; Instalador do Sevenzin (app de USUÁRIO) via Inno Setup 6.
; Embrulha o portátil self-contained de dist\Sevenzin\Sevenzin.exe (gere-o antes com scripts\publish-portable.ps1).
; O app Admin NÃO entra aqui de propósito: é local-only (ver AGENTS.md), não se distribui a usuário final.
; Compilar: scripts\build-installer.ps1  (ou abrir este .iss no Inno Setup e Build). Saída: dist\installer\.

#define AppName "Sevenzin"
#define AppVersion "1.0.0"
#define AppExe "Sevenzin.exe"
#define AppPublisher "geandreac"

[Setup]
AppId={{8F3A6C21-5B4E-4D9A-9C2F-1E7A0B3D6F84}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppSupportURL=https://github.com/geandreac/sevenzinTwk
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Instala por usuário (sem UAC) por padrão; o usuário pode escolher "para todos" (admin) no diálogo.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist\installer
OutputBaseFilename=Sevenzin-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Sevenzin.Wpf\Assets\sevenzin.ico
UninstallDisplayIcon={app}\{#AppExe}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\Sevenzin\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
