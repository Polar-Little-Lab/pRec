using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace PolarRecorder.Services;
public sealed class Hotkeys : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    private readonly HwndSource source;
    private readonly List<int> registered = new();
    public event Action<int>? Pressed;
    public Hotkeys(Window window) { source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); source.AddHook(Hook); }
    public bool Register(int id, uint key)
    { bool success = RegisterHotKey(source.Handle, id, 0x4006, key); if (success) registered.Add(id); return success; }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    { if (msg == 0x0312) { handled = true; Pressed?.Invoke(w.ToInt32()); } return IntPtr.Zero; }
    public void Dispose() { foreach (int id in registered) UnregisterHotKey(source.Handle, id); source.RemoveHook(Hook); }
}
