using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
namespace PolarRecorder.Services;
public sealed record WindowTarget(long Handle, uint ProcessId, long ProcessStarted, string Title, string App)
{
    public string Label => $"{App} · {Title}";
}
public static class WindowTargets
{
    private delegate bool EnumProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    private static bool IsShellSurface(IntPtr window)
    {
        if (window == GetShellWindow() || window == GetDesktopWindow()) return true;
        var name = new StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        return WindowTargetPolicy.IsShellSurface(name.ToString());
    }
    public static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362);
    public static IReadOnlyList<WindowTarget> List()
    {
        var items = new List<WindowTarget>();
        EnumWindows((h, _) => {
            if (!IsWindowVisible(h) || IsIconic(h) || IsShellSurface(h)) return true;
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == Environment.ProcessId) return true;
            if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            var title = new StringBuilder(1024); GetWindowText(h, title, title.Capacity);
            if (title.Length == 0) return true;
            try {
                using var process = Process.GetProcessById((int)pid);
                var target = new WindowTarget(h.ToInt64(), pid, process.StartTime.ToUniversalTime().Ticks, title.ToString(), process.ProcessName);
                if (GetBounds(target).Width > 1) items.Add(target);
            } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return true;
        }, IntPtr.Zero);
        return items.OrderBy(x => x.App).ThenBy(x => x.Title).ToArray();
    }
    public static string? UnavailableReason(WindowTarget target)
    {
        var h = new IntPtr(target.Handle);
        if (!IsWindow(h)) return "선택한 창이 닫혔습니다. 창을 다시 선택하세요.";
        if (IsShellSurface(h)) return WindowTargetPolicy.ShellMessage;
        GetWindowThreadProcessId(h, out uint pid);
        if (pid != target.ProcessId) return "선택한 창이 변경되었습니다. 창을 다시 선택하세요.";
        try {
            using var process = Process.GetProcessById((int)pid);
            if (process.StartTime.ToUniversalTime().Ticks != target.ProcessStarted) return "선택한 앱이 다시 실행되었습니다. 창을 다시 선택하세요.";
        } catch { return "선택한 앱을 확인할 수 없습니다. 창을 다시 선택하세요."; }
        if (IsIconic(h)) return "선택한 창이 최소화되었습니다. 창을 복원한 뒤 계속 녹화하세요.";
        if (!IsWindowVisible(h)) return "선택한 창이 숨겨졌습니다. 창을 다시 표시하세요.";
        return null;
    }
    public static Rectangle GetBounds(WindowTarget target)
    {
        string? reason = UnavailableReason(target);
        if (reason != null) throw new InvalidOperationException(reason);
        if (!GetWindowRect(new IntPtr(target.Handle), out var r) || r.Right-r.Left < 2 || r.Bottom-r.Top < 2)
            throw new InvalidOperationException("창 크기를 확인할 수 없습니다.");
        return new Rectangle(0, 0, (r.Right-r.Left)/2*2, (r.Bottom-r.Top)/2*2);
    }
}
