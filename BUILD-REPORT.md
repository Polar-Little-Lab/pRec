# Polar Recorder 0.3 verification · 2026-10-25

- Windows x64 self-contained Release publish: SUCCESS; no compiler warnings/errors.
- FFmpeg executable is now physically bundled at app/tools/ffmpeg.exe.
- Gyan download archive SHA-256 matched its published checksum:
  60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba
- Archive identifies version 9.0.2-essentials_build-www.gyan.dev, GPL v3.
- Original LICENSE and README (build configuration and source reference) preserved.
- Build configuration lists gdigrab, libx264 and AAC support.
- Application only resolves the bundled path; prior user paths and PATH are ignored.
- Removed engine file picker. Missing files produce re-extraction guidance.
- Old JSON settings migrate by ignoring the obsolete Ffmpeg field.
- Developer build script bundles the engine; source-only builds fetch a hash-pinned archive and fail on mismatch.

Windows GUI and bundled Windows FFmpeg cannot execute on this Linux host.
Real capture, sound synchronization and compatibility with the newly bundled engine remain Windows acceptance tests. This is not a claim of Windows end-to-end verification.
Earlier media pipeline tests and UI build evidence are in docs/BUILD-REPORT-0.1.md and docs/BUILD-REPORT-0.2.md.

## 0.3.1 icon and alignment update

Windows x64 Release publish succeeded without compiler warnings/errors. Verified PE resource types RT_ICON and RT_GROUP_ICON, ICO sizes 16/20/24/32/40/48/64/128/256, and XAML parsing. Preview header uses a two-column Grid, shared vertical center, and 14 DIP bottom gap. Removed global TextBlock foreground setter that overrode primary button text. Bundled engine retained unchanged. Actual updated WPF rendering/taskbar behavior still needs Windows confirmation.

## 0.3.2 — selected B-focus icon

B-focus PNG/SVG/ICO assets are byte-identical to the approved candidate. Eight PE icon images verified by hash. Release build and installer compilation passed. Installer payload integrity and all 482 file hashes passed. Windows icon rendering still needs native confirmation.

## 0.3.3 PolarRec · 폴라렉
앱 제목, 실행 파일, 설치 프로그램, 바로가기 이름 통일. B 아이콘 유지. 이전 설정과 설치 경로 호환 처리. win-x64 게시 성공. Windows 실행 검증은 미수행.
