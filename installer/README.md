# Windows installer

Build the app first using scripts/build.ps1, or use the provided app-polarrec directory.
NSIS 3.09 or later can compile installer/PolarRecorder.nsi.
The .nsi currently consumes app-polarrec/ (not dist/); copy a newly built dist/PolarRec tree there before packaging.
The installer is per-user, offline and includes the engine and runtime.
It registers an uninstaller, a Start menu entry and an optional desktop shortcut.
It does not delete recording files or preference files during uninstall.
No Authenticode certificate is available; the installer is unsigned.

uninstall-files.nsh enumerates only packaged files. Refresh that list when app contents change.
Windows installation, shortcut creation and uninstall need native acceptance testing.
