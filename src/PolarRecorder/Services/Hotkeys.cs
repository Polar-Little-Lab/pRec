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
    private readonly Dictionary<(uint Mod, uint Key), int> registered = new();
    private readonly Dictionary<int, int> actions = new();
    private int nextId = 10;
    public event Action<int>? Pressed;
    public Hotkeys(Window window) { source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); source.AddHook(Hook); }
    // Register new combinations before releasing old ones: conflicts leave the current bindings intact.
    public bool Configure(uint startMod, uint startKey, uint pauseMod, uint pauseKey)
    {
        var desired = new[] { (startMod, startKey), (pauseMod, pauseKey) };
        if (desired[0] == desired[1]) return false;
        var added = new List<(uint Mod, uint Key)>();
        foreach (var chord in desired) {
            if (registered.ContainsKey(chord)) continue;
            int id = nextId++;
            if (!RegisterHotKey(source.Handle, id, chord.Item1 | 0x4000, chord.Item2)) {
                foreach (var item in added) { UnregisterHotKey(source.Handle, registered[item]); registered.Remove(item); }
                return false;
            }
            registered[chord] = id; added.Add(chord);
        }
        foreach (var chord in new List<(uint Mod, uint Key)>(registered.Keys))
            if (chord != desired[0] && chord != desired[1]) { UnregisterHotKey(source.Handle, registered[chord]); registered.Remove(chord); }
        actions.Clear(); actions[registered[desired[0]]] = 1; actions[registered[desired[1]]] = 2;
        return true;
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    { if (msg == 0x0312 && actions.TryGetValue(w.ToInt32(), out int action)) { handled = true; Pressed?.Invoke(action); } return IntPtr.Zero; }
    public void Dispose() { foreach (int id in registered.Values) UnregisterHotKey(source.Handle, id); registered.Clear(); actions.Clear(); source.RemoveHook(Hook); }
}
