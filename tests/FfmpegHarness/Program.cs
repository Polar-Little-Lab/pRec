using PolarRecorder.Services;
string exe = args.Length > 0 ? args[0] : "ffmpeg";
string dir = Path.Combine(Path.GetTempPath(), "Polar test 한글 " + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try {
    string target = Path.Combine(dir, "capture.mkv");
    using (var recorder = new Ffmpeg(exe, new[] { "-hide_banner", "-y", "-progress", "pipe:1", "-stats_period", "0.1", "-re", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30", "-an", "-c:v", "libx264", "-preset", "veryfast", target }, Path.Combine(dir, "capture.log"))) {
        await recorder.WaitForFramesAsync();
        await Task.Delay(500);
        await recorder.StopAsync();
    }
    if (!File.Exists(target) || new FileInfo(target).Length < 1024) throw new Exception("Capture output missing");
    await Ffmpeg.RunAsync(exe, new[] { "-v", "error", "-i", target, "-f", "null", "-" }, Path.Combine(dir, "decode.log"));
    Console.WriteLine("PASS: progress readiness, q/flush/exit, Unicode+space path, complete decode");
    bool failed = false;
    try { await Ffmpeg.RunAsync(exe, new[] { "-i", Path.Combine(dir, "missing.mp4"), "-f", "null", "-" }, Path.Combine(dir, "error.log")); }
    catch (IOException ex) { failed = ex.Message.Contains("missing.mp4"); }
    if (!failed) throw new Exception("Nonzero exit not propagated");
    Console.WriteLine("PASS: FFmpeg failure and diagnostic propagation");
    failed = false;
    using (var invalid = new Ffmpeg(exe, new[] { "-this-is-invalid" }, Path.Combine(dir, "startup.log"))) {
        try { await invalid.WaitForFramesAsync(); } catch (InvalidOperationException) { failed = true; }
    }
    if (!failed) throw new Exception("Startup failure not detected");
    Console.WriteLine("PASS: startup exit detection and cleanup");
} finally { Directory.Delete(dir, true); }
