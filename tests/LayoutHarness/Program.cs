using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PolarRecorder;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0])) throw new Exception("Pass the bundled FFmpeg path");
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "tools"));
        File.Copy(args[0], Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"), true);
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        var root = (Grid)window.Content;
        root.Background = window.Background;
        T Find<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        var pc = Find<CheckBox>("SystemAudio");
        var full = Find<RadioButton>("FullScreen");
        var region = Find<RadioButton>("RegionMode");
        var windowMode = Find<RadioButton>("WindowMode");
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        pc.IsChecked = true; windowMode.IsChecked = true;
        Check(pc.IsChecked == false, "Window entry defaults PC sound off");
        full.IsChecked = true; Check(pc.IsChecked == true, "Full screen restores PC sound");
        pc.IsChecked = false; windowMode.IsChecked = true; region.IsChecked = true;
        Check(pc.IsChecked == false, "Explicit desktop off is retained across window mode");
        pc.IsChecked = true; windowMode.IsChecked = true; pc.IsChecked = true;
        full.IsChecked = true; windowMode.IsChecked = true;
        Check(pc.IsChecked == false, "Window re-entry defaults off even after a window override");
        string preferences = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PolarRec", "settings.json"));
        using (var json = System.Text.Json.JsonDocument.Parse(preferences))
            Check(json.RootElement.GetProperty("DesktopPc").GetBoolean(), "Window settings must not overwrite desktop preference");
        Console.WriteLine("PASS: mode-specific PC sound, manual-off preference, window re-entry and persistence");
        // Exercise the actual WPF view without screen capture or a graphics capture device.
        Find<RadioButton>("WindowMode").IsChecked = true;
        Find<Border>("NoticePanel").Visibility = Visibility.Collapsed;
        Find<Border>("SetupBanner").Visibility = Visibility.Collapsed;
        Find<TextBlock>("FolderLabel").Text = @"C:\Users\Polar\Videos\PolarRec";
        Find<ComboBox>("WindowList").ItemsSource = new[] { new PolarRecorder.Services.WindowTarget(0, 0, 0, "예시 브라우저 창", "chrome") };
        Find<ComboBox>("WindowList").SelectedIndex = 0;
        Find<TextBlock>("CaptureSummary").Text = "예시 창 · 1920 × 1080";
        Directory.CreateDirectory("layout-checks");
        foreach (var size in new[] { new Size(984, 771), new Size(864, 691) }) {
            root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
            // Shared-size rows settle through another WPF layout pass.
            root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
            var cards = Find<Grid>("SettingsPanel");
            for (int i = 0; i < 2; i++) {
                var l = (Border)cards.Children[i]; var r = (Border)cards.Children[i+2];
                if (l.ActualHeight <= 0 || r.ActualHeight <= 0) throw new Exception("Cards must be laid out");
                double lb = l.TranslatePoint(new Point(0, l.ActualHeight), root).Y;
                double rb = r.TranslatePoint(new Point(0, r.ActualHeight), root).Y;
                if (Math.Abs(lb-rb) > 1) throw new Exception($"Card row {i} bottoms differ: {lb} / {rb}");
            }
            var scroll = Find<ScrollViewer>("RecordPage");
            double beforeToast = scroll.ViewportHeight;
            Find<Border>("NoticePanel").Visibility = Visibility.Visible;
            root.UpdateLayout();
            Check(Math.Abs(scroll.ViewportHeight - beforeToast) < 1, "Toast must not shrink the viewport");
            Check(Grid.GetRow(Find<Border>("NoticePanel")) == 1, "Toast appears at top of content");
            Find<Border>("NoticePanel").Visibility = Visibility.Collapsed;
            root.UpdateLayout();
            foreach (string name in new[] { "SettingsPanel", "FullScreen", "WindowMode", "WindowPicker", "RegionLabel", "SystemAudio", "MicAudio", "CursorOption", "PreviewPlaceholder" }) {
                var element = Find<FrameworkElement>(name);
                Console.WriteLine($"METRIC {name}: actual={element.ActualWidth}x{element.ActualHeight} desired={element.DesiredSize}");
            }
            for (int i = 0; i < 4; i++) Console.WriteLine($"CARD {i}: {((FrameworkElement)cards.Children[i]).ActualHeight}");
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create($"layout-checks/window-{size.Width}.png"); encoder.Save(file);
            if (size.Width == 984 && scroll.ScrollableHeight > 1)
                throw new Exception($"Default size clips controls: overflow {scroll.ScrollableHeight}, viewport {scroll.ViewportHeight}");
            Console.WriteLine($"PASS: WPF layout {size.Width}x{size.Height}, card bottoms aligned, scroll overflow {scroll.ScrollableHeight}");
        }
        app.Shutdown();
    }
}
