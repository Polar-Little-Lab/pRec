# Build and verification report

Date: 2026-10-23

## Environment
- Connected execution host: Linux Ubuntu 24.04 x64. No Windows desktop/remote execution session was available.
- .NET SDK was absent initially. Microsoft SDK download and NuGet endpoints were reachable.
- Installed .NET SDK 8.0.426 / runtime 8.0.32 in a temporary directory.
- Archive ownership restoration failed on the host, but extracted SDK files were usable. Subsequent install probe confirmed SDK presence.
- The host intermittently rejected .NET CLI process telemetry inspection; disabling first-run/telemetry and retrying allowed builds.

## Final build
- `dotnet publish src/PolarRecorder/PolarRecorder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o app`
- SUCCESS. Final compilation produced no warnings or errors.
- `app/PolarRecorder.exe`: PE32+ GUI executable, x86-64 Windows.
- WFAC010 is explicitly suppressed because this is a WPF entry point with DPI set in the application manifest. Windows Forms is used only for monitor enumeration.
- Dependencies: .NET 8 Windows Desktop runtime, NAudio 2.2.1.
- FFmpeg Windows executable is NOT bundled. Select a supported `ffmpeg.exe` in the app.

## Executed automated checks
1. Real C# FFmpeg wrapper harness (the same Ffmpeg.cs compiled into a net8 console harness): PASS.
   - first-frame progress readiness;
   - graceful q shutdown, stderr/stdout draining, output decoding;
   - Unicode and spaces in file paths;
   - nonzero exit diagnostics;
   - startup failure detection and disposal.
2. Real FFmpeg media integration (Python + Linux FFmpeg): PASS, all three cases.
   - no audio; one audio source; two audio sources (including silence).
   - three H.264 segments mixed/muxed then concatenated to MP4.
   - durations: 3.600313 s / 3.621313 s / 3.621313 s.
   - stream counts, video codec, duration range, full decoding checked.
3. All XAML, project and manifest files parsed as XML; Windows-targeted WPF markup compiled by MSBuild.

## NOT validated
- Launching the Windows executable, actual UI layout/focus/keyboard behavior.
- gdigrab desktop capture on Windows 10/11.
- WASAPI loopback/microphone device operation, disconnects, and silence insertion on real hardware.
- Actual A/V synchronization, long-session drift, capture latency and CPU load.
- Mixed-DPI / multiple-monitor coordinates, RDP/VDI, HDR or protected content.
- Disk-full recovery, 60-minute automatic pause, actual device removal.

This is a compiled MVP for Windows acceptance testing, not a claim of completed Windows end-to-end validation.

## Reproduce tests
```powershell
dotnet run --project tests/FfmpegHarness -- 'C:\Tools\ffmpeg\bin\ffmpeg.exe'
# With ffmpeg and ffprobe in PATH:
python tests/media_smoke.py
```
