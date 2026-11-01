; GapMouse per-user installer. Compile with ISCC and /D defines:
;   AppVersion, NumericVersion, Arch (x64|arm64), SourceDir, OutputDir
#ifndef AppVersion
  #error AppVersion must be defined (/DAppVersion=1.2.3)
#endif
#ifndef NumericVersion
  #error NumericVersion must be defined (/DNumericVersion=1.2.3)
#endif
#ifndef Arch
  #error Arch must be defined (/DArch=x64 or /DArch=arm64)
#endif
#ifndef SourceDir
  #error SourceDir must be defined (/DSourceDir=<publish dir>)
#endif
#ifndef OutputDir
  #error OutputDir must be defined (/DOutputDir=<output dir>)
#endif

[Setup]
AppId={{DBDB1941-7D22-4FE9-85C8-99227AAA11FE}
AppName=GapMouse
AppVersion={#AppVersion}
AppPublisher=Gustavo Auad Piccoli
AppPublisherURL=https://github.com/Gustavo-Piccoli/GapMouse
AppSupportURL=https://github.com/Gustavo-Piccoli/GapMouse
AppUpdatesURL=https://github.com/Gustavo-Piccoli/GapMouse
DefaultDirName={autopf}\GapMouse
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=GapMouse-Setup-{#AppVersion}-{#Arch}
SetupIconFile=..\src\GapMouse.App\Assets\Icons\gapmouse.ico
UninstallDisplayIcon={app}\GapMouse.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
AppMutex=GapMouse.SingleInstance
VersionInfoVersion={#NumericVersion}
#if Arch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#elif Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
  #error Arch must be x64 or arm64
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
english.StartWithWindows=Start GapMouse with Windows
brazilianportuguese.StartWithWindows=Iniciar o GapMouse com o Windows
english.AdditionalOptions=Additional options:
brazilianportuguese.AdditionalOptions=Opções adicionais:

[Tasks]
; CreateDesktopIcon and AdditionalIcons are built-in messages of both language files.
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: startup; Description: "{cm:StartWithWindows}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\GapMouse.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\GapMouse"; Filename: "{app}\GapMouse.exe"
Name: "{autodesktop}\GapMouse"; Filename: "{app}\GapMouse.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GapMouse"; ValueData: """{app}\GapMouse.exe"" --minimized"; Tasks: startup

[Run]
Filename: "{app}\GapMouse.exe"; Flags: nowait postinstall skipifsilent

[Code]
// The Run value is removed only when it still points at this installation,
// so a portable copy's (or another install's) startup entry is never touched.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Current, Expected: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GapMouse', Current) then
    begin
      Expected := '"' + ExpandConstant('{app}\GapMouse.exe') + '" --minimized';
      if CompareText(Current, Expected) = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GapMouse');
    end;
  end;
end;
