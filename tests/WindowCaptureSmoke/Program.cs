using System.Diagnostics;
using PolarRecorder.Services;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var target = new Form { Text = "PolarRec capture fixture", BackColor = Color.Red, ClientSize = new Size(640, 360), StartPosition = FormStartPosition.Manual, Location = new Point(100,100) };
        target.Shown += async (_, _) => {
            try {
                await Task.Delay(500);
                using var cover = new Form { Text = "Unrelated blue window", BackColor = Color.Blue, ClientSize = new Size(640,360), StartPosition = FormStartPosition.Manual, Location = target.Location, TopMost = true };
                cover.Show(); await Task.Delay(500);
                var output = Path.GetFullPath("window-capture-smoke.png");
                var info = new ProcessStartInfo(Path.GetFullPath(args[0])) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                foreach (string a in new[] {"-hide_banner","-y","-filter_complex",CaptureArguments.WindowFilter(target.Handle.ToInt64(),new Rectangle(0,0,640,360),false),"-frames:v","1","-threads","1",output}) info.ArgumentList.Add(a);
                using var p = Process.Start(info)!;
                var logs = p.StandardError.ReadToEndAsync();
                try { await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25)); }
                catch { p.Kill(true); await p.WaitForExitAsync(); throw; }
                string error = await logs; File.WriteAllText("window-capture-smoke.log",error);
                if (p.ExitCode != 0) throw new Exception(error);
                using var image = new Bitmap(output);
                var pixel = image.GetPixel(image.Width/2,image.Height/2);
                if (pixel.R < 200 || pixel.B > 60 || pixel.G > 60) throw new Exception($"Expected red target behind blue occluder, got {pixel}");
                Console.WriteLine("PASS: HWND capture returned red target while unrelated blue window covered it.");
                Environment.ExitCode = 0;
            } catch(Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
            finally { target.Close(); }
        };
        Application.Run(target);
    }
}
