using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Rectangle = System.Drawing.Rectangle;
namespace PolarRecorder;
public sealed class RegionPicker : Window
{
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    private readonly Canvas canvas = new();
    private readonly System.Windows.Shapes.Rectangle selection = new() { Stroke = new SolidColorBrush(Color.FromRgb(171, 207, 237)), StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(55, 110, 180, 255)) };
    private NativePoint start;
    private bool dragging;
    public Rectangle SelectedRegion { get; private set; }
    // One overlay per selected monitor. Native cursor coordinates keep capture bounds in physical pixels.
    public RegionPicker(Rectangle monitor)
    {
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Topmost = true;
        AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
        Cursor = Cursors.Cross; ShowInTaskbar = false; Content = canvas;
        var hint = new TextBlock { Text = "녹화할 부분을 드래그하세요   ·   Esc 취소", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(34, 48, 59)), IsHitTestVisible = false };
        var hintBox = new Border { Child = hint, Background = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(18, 12, 18, 12), IsHitTestVisible = false };
        Canvas.SetLeft(hintBox, 24); Canvas.SetTop(hintBox, 24); canvas.Children.Add(selection); canvas.Children.Add(hintBox);
        SourceInitialized += (_, _) => SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), monitor.X, monitor.Y, monitor.Width, monitor.Height, 0x0040);
        Loaded += (_, _) => { SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), monitor.X, monitor.Y, monitor.Width, monitor.Height, 0x0040); Activate(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; } };
        MouseLeftButtonDown += (_, _) => { GetCursorPos(out start); dragging = true; CaptureMouse(); };
        MouseMove += (_, _) => {
            if (!dragging) return; GetCursorPos(out var end);
            Point a = PointFromScreen(new Point(start.X, start.Y)); Point b = PointFromScreen(new Point(end.X, end.Y));
            Canvas.SetLeft(selection, Math.Min(a.X, b.X)); Canvas.SetTop(selection, Math.Min(a.Y, b.Y));
            selection.Width = Math.Abs(a.X - b.X); selection.Height = Math.Abs(a.Y - b.Y);
            hint.Text = $"{Math.Abs(end.X - start.X) / 2 * 2} × {Math.Abs(end.Y - start.Y) / 2 * 2} px   ·   마우스를 놓으면 선택   ·   Esc 취소";
        };
        MouseLeftButtonUp += (_, _) => {
            if (!dragging) return; dragging = false; ReleaseMouseCapture(); GetCursorPos(out var end);
            int x = Math.Clamp(Math.Min(start.X, end.X), monitor.Left, monitor.Right - 2);
            int y = Math.Clamp(Math.Min(start.Y, end.Y), monitor.Top, monitor.Bottom - 2);
            int w = (Math.Min(Math.Max(start.X, end.X), monitor.Right) - x) / 2 * 2;
            int h = (Math.Min(Math.Max(start.Y, end.Y), monitor.Bottom) - y) / 2 * 2;
            if (w < 16 || h < 16) { hint.Text = "너무 작은 영역입니다. 16 × 16 px 이상으로 다시 드래그하세요.   ·   Esc 취소"; return; }
            SelectedRegion = new Rectangle(x, y, w, h); DialogResult = true;
        };
    }
}
