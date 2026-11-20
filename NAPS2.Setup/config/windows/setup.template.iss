; !defs

#include "..\config\windows\setup.languages.iss"

#define AppShortName             "NAPS2"
#define AppLongName              "NAPS2 - Not Another PDF Scanner"
#define AppCompany               "Nicy (unofficial NAPS2 build)"
#define AppCopyrightStartYear    "2009"
#define AppCopyrightEndYear      GetDateTimeString('yyyy','','')
#define AppCopyrightCompany      "NAPS2 Contributors"
#define ExeName                  "NAPS2.exe"

[Setup]
AppName={#AppLongName}
AppVersion={#AppVersion}
; Preserve AppName (and thus Inno Setup implicit AppId) so this replaces the official install.
; Only the display label identifies this unsigned community build.
AppVerName={#AppShortName} Nicy-0018 ({#AppVersionName})
AppPublisher={#AppCompany}
AppPublisherURL=https://github.com/Petr-Cherevin/naps2-nicy
AppSupportURL=https://github.com/Petr-Cherevin/naps2-nicy/issues
AppUpdatesURL=https://github.com/Petr-Cherevin/naps2-nicy

VersionInfoDescription={#AppShortName} installer
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppShortName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCompany={#AppCompany}
VersionInfoCopyright=(c) {#AppCopyrightStartYear}-{#AppCopyrightEndYear}

ShowLanguageDialog=yes
UsePreviousLanguage=no
LanguageDetectionMethod=uilanguage
WizardStyle=modern
; Require Windows 10 1607+
MinVersion=10.0.14393

DefaultDirName={commonpf}\{#AppShortName}
DefaultGroupName={#AppShortName}
LicenseFile=..\license.rtf

UninstallDisplayName={#AppShortName} Nicy-0018
UninstallDisplayIcon={app}\{#ExeName}

OutputDir=../publish/{#AppVersionName}
OutputBaseFilename=naps2-{#AppVersionName}-{#AppPlatform}
Compression=lzma2/ultra64
LZMAUseSeparateProcess=yes
SolidCompression=yes
; !arch

ChangesAssociations=yes

[Run]
Filename: "{app}\{#ExeName}"; Description: "{cm:LaunchProgram,{#AppShortName}}"; Flags: nowait postinstall

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]                              
; !files

; Delete files from old locations in case of upgrade
[InstallDelete]     
Type: files; Name: "{app}\*.exe"
Type: files; Name: "{app}\*.exe.config"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\lib"
; !clean32

[Icons]
Name: "{group}\NAPS2"; Filename: "{app}\{#ExeName}"
Name: "{commondesktop}\NAPS2"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\WIA_{{1c3a7177-f3a7-439e-be47-e304a185f932}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\WIA_{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "Action"; ValueData: "Scan with NAPS2"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\WIA_{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "CLSID"; ValueData: "WIACLSID"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\WIA_{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "DefaultIcon"; ValueData: "sti.dll,0"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\WIA_{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "InitCmdLine"; ValueData: "/WiaCmd;{app}\{#ExeName} /StiDevice:%1 /StiEvent:%2;"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\WIA_{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "Provider"; ValueData: "NAPS2"

Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\StillImage\Registered Applications"; Flags:uninsdeletevalue; ValueType: string; ValueName: "NAPS2"; ValueData: "{app}\{#ExeName}"

Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\StillImage\Events\STIProxyEvent\{{1c3a7177-f3a7-439e-be47-e304a185f932}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\StillImage\Events\STIProxyEvent\{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "Cmdline"; ValueData: "{app}\{#ExeName} /StiDevice:%1 /StiEvent:%2"
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\StillImage\Events\STIProxyEvent\{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "Desc"; ValueData: "Scan with NAPS2"
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\StillImage\Events\STIProxyEvent\{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#ExeName},0"
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\StillImage\Events\STIProxyEvent\{{1c3a7177-f3a7-439e-be47-e304a185f932}"; ValueType: string; ValueName: "Name"; ValueData: "NAPS2"

Root: HKCR; Subkey: ".pdf\OpenWithProgids"; ValueType: string; ValueName: "{#AppShortName}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".jpg\OpenWithProgids"; ValueType: string; ValueName: "{#AppShortName}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".jpeg\OpenWithProgids"; ValueType: string; ValueName: "{#AppShortName}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".png\OpenWithProgids"; ValueType: string; ValueName: "{#AppShortName}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".tiff\OpenWithProgids"; ValueType: string; ValueName: "{#AppShortName}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".tif\OpenWithProgids"; ValueType: string; ValueName: "{#AppShortName}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".bmp\OpenWithProgids"; ValueType: string; ValueName: "{#AppShortName}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: "{#AppShortName}"; ValueType: string; ValueName: ""; ValueData: "{#AppShortName}"; Flags: uninsdeletekey;
Root: HKCR; Subkey: "{#AppShortName}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#ExeName},0"
Root: HKCR; Subkey: "{#AppShortName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#ExeName}"" ""%1"""
