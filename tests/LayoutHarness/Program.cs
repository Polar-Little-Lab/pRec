using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PolarRecorder;
internal static class Program
{
    [STAThread] static void Main()
    {
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        var root = (FrameworkElement)window.Content;
        T Find<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        // Exercise the actual WPF view without screen capture or a graphics capture device.
        Find<RadioButton>("WindowMode").IsChecked = true;
        Find<Border>("NoticePanel").Visibility = Visibility.Collapsed;
        Find<Border>("SetupBanner").Visibility = Visibility.Collapsed;
        Find<TextBlock>("FolderLabel").Text = @"C:\Users\Polar\Videos\PolarRec";
        Find<ComboBox>("WindowList").ItemsSource = new[] { "chrome · 녹화할 브라우저 창" };
        Find<ComboBox>("WindowList").SelectedIndex = 0;
        Directory.CreateDirectory("layout-checks");
        foreach (var size in new[] { new Size(984, 771), new Size(864, 691) }) {
            root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
            // Shared-size rows settle through another WPF layout pass.
            root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
            var left = Find<Grid>("SettingsPanel");
            var outer = (Grid)left.Parent;
            var right = (Grid)outer.Children[1];
            for (int i = 0; i < 2; i++) {
                var l = (Border)left.Children[i]; var r = (Border)right.Children[i];
                double lb = l.TranslatePoint(new Point(0, l.ActualHeight), root).Y;
                double rb = r.TranslatePoint(new Point(0, r.ActualHeight), root).Y;
                if (Math.Abs(lb-rb) > 1) throw new Exception($"Card row {i} bottoms differ: {lb} / {rb}");
            }
            var scroll = Find<ScrollViewer>("RecordPage");
            if (size.Width == 984 && scroll.ScrollableHeight > 1)
                throw new Exception($"Default size clips controls: overflow {scroll.ScrollableHeight}, viewport {scroll.ViewportHeight}");
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create($"layout-checks/window-{size.Width}.png"); encoder.Save(file);
            Console.WriteLine($"PASS: WPF layout {size.Width}x{size.Height}, card bottoms aligned, scroll overflow {scroll.ScrollableHeight}");
        }
        app.Shutdown();
    }
}
