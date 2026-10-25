Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
Name "pRec"
OutFile "../../pRec-0.4.10-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\pRec"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
SetDatablockOptimize on
BrandingText "pRec 0.4.10"
VIProductVersion "0.4.10.0"
VIAddVersionKey /LANG=1042 "ProductName" "pRec"
VIAddVersionKey /LANG=1042 "FileDescription" "pRec 설치 프로그램"
VIAddVersionKey /LANG=1042 "FileVersion" "0.4.10"
VIAddVersionKey /LANG=1042 "LegalCopyright" "pRec contributors"
!define MUI_ICON "../src/PolarRecorder/Assets/PolarRecorder.ico"
!define MUI_UNICON "../src/PolarRecorder/Assets/PolarRecorder.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "pRec 설치"
!define MUI_WELCOMEPAGE_TEXT "화면과 소리를 간편하게 녹화하세요.$\r$\n$\r$\n녹화에 필요한 구성 요소가 모두 포함되어 있습니다.$\r$\n별도 다운로드나 설정은 필요하지 않습니다.$\r$\n$\r$\n실행 중인 pRec 또는 PolarRec이 있다면 먼저 종료해 주세요."
!define MUI_FINISHPAGE_RUN "$INSTDIR\pRec.exe"
!define MUI_FINISHPAGE_RUN_TEXT "pRec 실행"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Korean"
Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "이 앱은 Windows 10·11 64비트용입니다."
    Abort
  ${EndIf}
  SetShellVarContext current
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "InstallLocation"
  ${If} $0 != ""
    StrCpy $INSTDIR $0
  ${EndIf}
FunctionEnd
Section "pRec (필수)" SEC_APP
  SectionIn RO
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File /r "..\app-polarrec\*"
  Delete "$INSTDIR\PolarRecorder.exe"
  Delete "$INSTDIR\PolarRecorder.dll"
  Delete "$INSTDIR\PolarRecorder.deps.json"
  Delete "$INSTDIR\PolarRecorder.runtimeconfig.json"
  Delete "$INSTDIR\PolarRecorder.pdb"
  Delete "$DESKTOP\Polar Recorder.lnk"
  Delete "$SMPROGRAMS\Polar Recorder\Polar Recorder.lnk"
  Delete "$SMPROGRAMS\Polar Recorder\제거.lnk"
  RMDir "$SMPROGRAMS\Polar Recorder"
  Delete "$INSTDIR\PolarRec.exe"
  Delete "$INSTDIR\PolarRec.dll"
  Delete "$INSTDIR\PolarRec.deps.json"
  Delete "$INSTDIR\PolarRec.runtimeconfig.json"
  Delete "$INSTDIR\PolarRec.pdb"
  Delete "$DESKTOP\PolarRec.lnk"
  Delete "$SMPROGRAMS\PolarRec\PolarRec.lnk"
  Delete "$SMPROGRAMS\PolarRec\제거.lnk"
  RMDir "$SMPROGRAMS\PolarRec"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\pRec"
  CreateShortcut "$SMPROGRAMS\pRec\pRec.lnk" "$INSTDIR\pRec.exe" "" "$INSTDIR\pRec.exe" 0
  CreateShortcut "$SMPROGRAMS\pRec\제거.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "DisplayName" "pRec"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "DisplayVersion" "0.4.10"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "DisplayIcon" "$INSTDIR\pRec.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "NoRepair" 1
SectionEnd
Section "바탕화면 바로가기" SEC_DESKTOP
  CreateShortcut "$DESKTOP\pRec.lnk" "$INSTDIR\pRec.exe" "" "$INSTDIR\pRec.exe" 0
SectionEnd
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_APP} "앱과 녹화 구성 요소를 현재 사용자 계정에 설치합니다."
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_DESKTOP} "바탕화면에 앱 실행 아이콘을 만듭니다."
!insertmacro MUI_FUNCTION_DESCRIPTION_END
Section "Uninstall"
  SetShellVarContext current
  !include "uninstall-files.nsh"
  Delete "$DESKTOP\pRec.lnk"
  Delete "$SMPROGRAMS\pRec\pRec.lnk"
  Delete "$SMPROGRAMS\pRec\제거.lnk"
  RMDir "$SMPROGRAMS\pRec"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
