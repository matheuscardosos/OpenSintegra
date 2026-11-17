; Inno Setup script for OpenSintegra Contabil (Windows x64)
; Pass /DAppVersion=X.Y.Z /DPublishAppDir=... /DPublishCliDir=... /DOutputDir=...

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishAppDir
  #define PublishAppDir "..\..\publish"
#endif
#ifndef PublishCliDir
  #define PublishCliDir "..\..\publish-cli"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\publish-installer"
#endif

[Setup]
AppId={{B4E3E9D2-7E5C-4B21-9A14-8C3A7F2D190E}
AppName=OpenSintegra Contabil
AppVersion={#AppVersion}
AppVerName=OpenSintegra Contabil {#AppVersion}
AppPublisher=OpenSintegra Contributors
AppPublisherURL=https://github.com/opensintegra/open-sintegra
AppSupportURL=https://github.com/opensintegra/open-sintegra/issues
AppUpdatesURL=https://github.com/opensintegra/open-sintegra/releases
DefaultDirName={autopf}\OpenSintegra Contabil
DefaultGroupName=OpenSintegra Contabil
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
SetupIconFile=..\..\assets\brand\app.ico
UninstallDisplayIcon={app}\OpenSintegra.exe
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=OpenSintegra-Setup-{#AppVersion}-win-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ChangesEnvironment=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "contextmenu"; Description: "Adicionar 'Abrir com OpenSintegra Contabil' ao botao direito em arquivos .txt e .zip"; GroupDescription: "Integracao com o Windows:"
Name: "addtopath"; Description: "Adicionar utilitario de linha de comando (opensintegra-cli) ao PATH do usuario"; GroupDescription: "Ferramentas de Terminal:"; Flags: unchecked

[Files]
Source: "{#PublishAppDir}\OpenSintegra.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishCliDir}\opensintegra-cli.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\OpenSintegra Contabil"; Filename: "{app}\OpenSintegra.exe"
Name: "{group}\Desinstalar OpenSintegra Contabil"; Filename: "{uninstallexe}"
Name: "{autodesktop}\OpenSintegra Contabil"; Filename: "{app}\OpenSintegra.exe"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.txt\shell\OpenSintegra"; ValueType: string; ValueName: ""; ValueData: "Abrir com OpenSintegra Contabil"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.txt\shell\OpenSintegra"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\OpenSintegra.exe"""; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.txt\shell\OpenSintegra\command"; ValueType: string; ValueName: ""; ValueData: """{app}\OpenSintegra.exe"" ""%1"""; Tasks: contextmenu

Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsAddPath('{app}'); Tasks: addtopath

[Run]
Filename: "{app}\OpenSintegra.exe"; Description: "{cm:LaunchProgram,OpenSintegra Contabil}"; Flags: nowait postinstall skipifsilent

[Code]
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Uppercase(Param) + ';', ';' + Uppercase(OrigPath) + ';') = 0;
end;
