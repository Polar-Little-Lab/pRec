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
