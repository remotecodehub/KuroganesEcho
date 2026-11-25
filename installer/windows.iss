#ifndef SourceDir
  #error SourceDir must be defined
#endif

#ifndef OutputDir
  #error OutputDir must be defined
#endif

[Setup]
AppId={{30F52D18-8822-4A15-9B9E-8C12F3A83EE1}
AppName=Kuroganes Echo
AppVersion=1.0.0
DefaultDirName={localappdata}\Programs\KuroganesEcho
DefaultGroupName=Kuroganes Echo
OutputDir={#OutputDir}
OutputBaseFilename=KuroganesEcho-Setup-win-x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Kuroganes Echo"; Filename: "{app}\KuroganesEcho.exe"
Name: "{autodesktop}\Kuroganes Echo"; Filename: "{app}\KuroganesEcho.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\KuroganesEcho.exe"; Description: "Launch Kuroganes Echo"; Flags: postinstall nowait skipifsilent
