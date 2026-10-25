Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
Name "PolarRec"
OutFile "../../PolarRec-0.4.9-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\PolarRec"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
SetDatablockOptimize on
BrandingText "PolarRec 0.4.9"
VIProductVersion "0.4.9.0"
VIAddVersionKey /LANG=1042 "ProductName" "PolarRec"
VIAddVersionKey /LANG=1042 "FileDescription" "PolarRec 설치 프로그램"
VIAddVersionKey /LANG=1042 "FileVersion" "0.4.9"
VIAddVersionKey /LANG=1042 "LegalCopyright" "PolarRec contributors"
!define MUI_ICON "../src/PolarRecorder/Assets/PolarRecorder.ico"
!define MUI_UNICON "../src/PolarRecorder/Assets/PolarRecorder.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "PolarRec 설치"
!define MUI_WELCOMEPAGE_TEXT "화면과 소리를 간편하게 녹화하세요.$\r$\n$\r$\n녹화에 필요한 구성 요소가 모두 포함되어 있습니다.$\r$\n별도 다운로드나 설정은 필요하지 않습니다.$\r$\n$\r$\n실행 중인 PolarRec가 있다면 먼저 종료해 주세요."
!define MUI_FINISHPAGE_RUN "$INSTDIR\PolarRec.exe"
!define MUI_FINISHPAGE_RUN_TEXT "PolarRec 실행"
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
Section "PolarRec (필수)" SEC_APP
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
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\PolarRec"
  CreateShortcut "$SMPROGRAMS\PolarRec\PolarRec.lnk" "$INSTDIR\PolarRec.exe" "" "$INSTDIR\PolarRec.exe" 0
  CreateShortcut "$SMPROGRAMS\PolarRec\제거.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "DisplayName" "PolarRec"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "DisplayVersion" "0.4.9"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "DisplayIcon" "$INSTDIR\PolarRec.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder" "NoRepair" 1
SectionEnd
Section "바탕화면 바로가기" SEC_DESKTOP
  CreateShortcut "$DESKTOP\PolarRec.lnk" "$INSTDIR\PolarRec.exe" "" "$INSTDIR\PolarRec.exe" 0
SectionEnd
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_APP} "앱과 녹화 구성 요소를 현재 사용자 계정에 설치합니다."
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_DESKTOP} "바탕화면에 앱 실행 아이콘을 만듭니다."
!insertmacro MUI_FUNCTION_DESCRIPTION_END
Section "Uninstall"
  SetShellVarContext current
  !include "uninstall-files.nsh"
  Delete "$DESKTOP\PolarRec.lnk"
  Delete "$SMPROGRAMS\PolarRec\PolarRec.lnk"
  Delete "$SMPROGRAMS\PolarRec\제거.lnk"
  RMDir "$SMPROGRAMS\PolarRec"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PolarRecorder"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
