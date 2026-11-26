; Instalador do SevenzinTwks para os CLIENTES: só o otimizador (User). O app Admin é exclusivo do
; administrador (Geandre), fica só local e NUNCA entra neste instalador.
; Embrulha o portátil self-contained dist\Sevenzin\Sevenzin.exe (gere com scripts\publish-portable.ps1).
; Compilar: scripts\build-installer.ps1  (ou abrir este .iss no Inno Setup e Build). Saída: dist\installer\.

#define AppName "SevenzinTwks"
#define AppVersion "2.2.0"
#define AppExe "Sevenzin.exe"
#define AppPublisher "geandreac"

[Setup]
AppId={{8F3A6C21-5B4E-4D9A-9C2F-1E7A0B3D6F84}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (C) 2026 {#AppPublisher}
AppSupportURL=https://github.com/geandreac/sevenzinTwk
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; O SevenzinTwks opera sempre como Administrador para aplicar e reverter tweaks HKLM/GPU/Sistema sem fechar o app.
PrivilegesRequired=admin
OutputDir=..\dist\installer
OutputBaseFilename=SevenzinTwks-{#AppVersion}-setup
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
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: shellexec nowait postinstall skipifsilent
