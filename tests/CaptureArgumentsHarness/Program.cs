using System.Drawing;
using System.Globalization;
using PolarRecorder.Services;
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
var size = new Rectangle(-1920, -100, 1280, 720);
var window = CaptureArguments.Video(size, false, 123456789, "movie with spaces.mkv");
Check(!window.Contains("desktop") && !window.Contains("gdigrab") && !window.Contains("-i"), "Window mode must never capture the desktop");
var filter = window[Array.IndexOf(window, "-filter_complex") + 1];
Check(filter.Contains("hwnd=123456789:") && !filter.Contains("monitor_idx") && !filter.Contains("window_title"), "Must bind exact HWND, not title or monitor");
Check(filter.Contains("width=1280:height=720") && filter.Contains("resize_mode=scale_aspect"), "Stable canvas across resize and pause");
Check(filter.Contains("capture_cursor=0") && window.Contains("-r") && window.Contains("30") && filter.Contains("hwdownload"), "CPU encoding and cursor policy");
Check(window[^1] == "movie with spaces.mkv", "Output paths remain one argument");
var desktop = CaptureArguments.Video(size, true, null, "screen.mkv");
Check(desktop.Contains("desktop") && desktop.Contains("-1920") && !desktop.Contains("-filter_complex"), "Existing multi-monitor capture offsets preserved");
foreach (var h in new long[] {0, -1}) { try { CaptureArguments.WindowFilter(h,size,true); throw new Exception("Invalid HWND accepted"); } catch(ArgumentException) {} }
try { CaptureArguments.WindowFilter(1,new Rectangle(0,0,1279,719),true); throw new Exception("Odd canvas accepted"); } catch(ArgumentException) {}
Console.WriteLine("PASS: exact window binding, no desktop fallback, resize canvas, cursor, locale, negative monitor offsets, invalid targets");
