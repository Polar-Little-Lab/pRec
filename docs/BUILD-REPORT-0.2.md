# Polar Recorder 0.2 · Frost redesign verification

Date: 2026-10-25

## Changed
- Shared WPF resource dictionary: Frost color, typography, buttons, toggles, panels, focus rings.
- Recording / recordings / preferences pages; permanent bottom recording controls.
- Clear preparation and failure states; one-time engine setup hint, PATH discovery.
- Cancelable countdown; saved-video opening; recent MP4 listing with keyboard support.
- On-demand screen preview, in-memory only; native aspect ratio preserved; GDI handles disposed.
- Region picker instruction panel, physical-pixel size, too-small-selection feedback.
- Preference changes persist independently of starting a recording.
- Version 0.2.0. Capture/encoding service implementations are unchanged.

## Build performed
Linux host; .NET SDK 8.0.426. Windows x64 self-contained Release publish succeeded.
Final compilation produced no warnings or errors.
Command:
`dotnet publish src/PolarRecorder/PolarRecorder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o app`

`app/PolarRecorder.exe` is a Windows GUI PE32+ x64 executable. The .NET desktop runtime is bundled; FFmpeg is not.
WFAC010 remains intentionally suppressed: this is a WPF entry point using manifest DPI configuration, with WinForms only for screen enumeration.

## Checks performed this revision
- C# and WPF XAML compilation, including resource dictionaries and event bindings.
- XML parsing of project, manifest, XAML files.
- Static consistency review of page names, event handlers, state transitions and disabled controls.
- Text/action color contrast checked against their backgrounds.
- Rendered design preview inspected. This is a design illustration, not a WPF runtime screenshot.
- ZIP integrity and Windows executable presence verified.

FFmpeg integration/process-wrapper tests passed in version 0.1 (see docs/BUILD-REPORT-0.1.md).
They were not rerun in this UI revision because those service implementations were not changed.

## Remaining Windows acceptance checks
- Actual WPF rendering, control sizes at minimum window width and 125/150/200% DPI.
- Keyboard focus and screen-reader names in the custom controls.
- Preview capture, region overlay positioning, multiple monitors, negative coordinates.
- Countdown cancellation and minimize/restore on a real desktop.
- Real desktop/WASAPI capture, A/V synchronization, disconnects and long-session behavior.

The Linux host cannot execute Windows WPF. No claim of Windows end-to-end validation is made.
