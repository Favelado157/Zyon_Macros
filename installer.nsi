Unicode true
!ifndef APP_VERSION
!define APP_VERSION "2.0.1"
!endif
!ifndef PUBLISH_DIR
!define PUBLISH_DIR "publish"
!endif
!ifndef OUT_FILE
!define OUT_FILE "ZyonMacros-Setup-x64.exe"
!endif
!define APP "ZyonMacros"
!define INSTALL_ID "GhosttzzKBot"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${INSTALL_ID}"
Name "${APP} ${APP_VERSION}"
OutFile "${OUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\${INSTALL_ID}"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "FileDescription" "Instalador do ${APP} para Windows"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "ZyonMacros"
!include "MUI2.nsh"
!include "x64.nsh"
!define MUI_ICON "assets\app.ico"
!define MUI_UNICON "assets\app.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\GhosttzzKBot.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir o ZyonMacros"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Este instalador requer Windows de 64 bits."
    Abort
  ${EndIf}
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\${INSTALL_ID}"
  again:
  FindWindow $0 "" "ZyonMacros"
  StrCmp $0 0 0 running
  FindWindow $0 "" "GhosttzzKBot"
  StrCmp $0 0 ready
  running:
  MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Feche o ZyonMacros para continuar a instalação." IDRETRY again
  Abort
  ready:
FunctionEnd

Section "ZyonMacros"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${APP}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "${APP}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\GhosttzzKBot.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  Delete "$SMPROGRAMS\GhosttzzKBot.lnk"
  Delete "$DESKTOP\GhosttzzKBot.lnk"
  CreateShortCut "$SMPROGRAMS\ZyonMacros.lnk" "$INSTDIR\GhosttzzKBot.exe" "" "$INSTDIR\assets\app.ico"
  CreateShortCut "$DESKTOP\ZyonMacros.lnk" "$INSTDIR\GhosttzzKBot.exe" "" "$INSTDIR\assets\app.ico"
SectionEnd

Function un.onInit
  StrCpy $INSTDIR "$LOCALAPPDATA\Programs\${INSTALL_ID}"
  FindWindow $0 "" "ZyonMacros"
  StrCmp $0 0 0 running
  FindWindow $0 "" "GhosttzzKBot"
  StrCmp $0 0 ready
  running:
  MessageBox MB_ICONSTOP "Feche o ZyonMacros antes de desinstalar."
  Abort
  ready:
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  Delete "$SMPROGRAMS\ZyonMacros.lnk"
  Delete "$DESKTOP\ZyonMacros.lnk"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  Delete "$INSTDIR\*"
  RMDir /r "$INSTDIR\assets"
  RMDir "$INSTDIR"
SectionEnd
