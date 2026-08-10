; Instalador de "Gestor de Contrasenas" (Inno Setup)
; Instalacion POR USUARIO (sin permisos de administrador / sin UAC).

#define MyAppName "Bai-Pass"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Javosornio"
#define MyAppExeName "Bai-Pass.exe"

[Setup]
; GUID unico de esta aplicacion (no cambiar entre versiones).
AppId={{A7F3C2E1-5B9D-4E6A-8C1F-2D3E4F5A6B7C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Instala sin admin, en la carpeta del usuario -> no aparece el aviso UAC.
PrivilegesRequired=lowest
OutputDir=C:\Develop\Passwords\dist
OutputBaseFilename=BaiPass-Setup
SetupIconFile=C:\Develop\Passwords\Assets\bai-pass.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "C:\Develop\Passwords\bin\Release\net10.0\win-x64\publish\Bai-Pass.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
