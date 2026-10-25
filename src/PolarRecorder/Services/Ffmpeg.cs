using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace PolarRecorder.Services;
public sealed class Ffmpeg : IDisposable
{
    private readonly Process process;
    private readonly StringBuilder tail = new();
    private readonly StreamWriter log;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public event Action? FirstFrame;
    public bool HasExited => process.HasExited;
    public Ffmpeg(string executable, string[] arguments, string logPath)
    {
        log = new StreamWriter(logPath, append: true) { AutoFlush = true };
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.ErrorDataReceived += (_, e) => { if (e.Data is null) return; lock (tail) { log.WriteLine(e.Data); tail.AppendLine(e.Data); if (tail.Length > 6000) tail.Remove(0, tail.Length - 6000); } };
        process.OutputDataReceived += (_, e) => {
            if (e.Data?.StartsWith("frame=") == true && int.TryParse(e.Data.AsSpan(6).Trim(), out int frame) && frame > 0 && !ready.Task.IsCompleted)
            { FirstFrame?.Invoke(); ready.TrySetResult(); }
        };
        try { process.Start(); process.BeginErrorReadLine(); process.BeginOutputReadLine(); }
        catch { process.Dispose(); log.Dispose(); throw; }
    }
    public async Task WaitForFramesAsync()
    {
        var exited = process.WaitForExitAsync();
        var completed = await Task.WhenAny(ready.Task, exited, Task.Delay(15000));
        if (completed == ready.Task) { await ready.Task; return; }
        throw new InvalidOperationException("화면 캡처를 시작하지 못했습니다. FFmpeg 로그를 확인하세요.\n" + ErrorText());
    }
    public async Task StopAsync()
    {
        if (!process.HasExited)
        {
            try { await process.StandardInput.WriteLineAsync("q"); await process.StandardInput.FlushAsync(); } catch (IOException) { }
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(); throw new IOException("FFmpeg 종료 시간이 초과되었습니다. 임시 파일은 보존됩니다."); }
        process.WaitForExit();
        if (process.ExitCode != 0) throw new IOException(ErrorText());
    }
    public async Task CompleteAsync()
    {
        await process.WaitForExitAsync(); process.WaitForExit();
        if (process.ExitCode != 0) throw new IOException(ErrorText());
    }
    private string ErrorText() { lock (tail) return tail.ToString(); }
    public void Dispose()
    {
        if (!process.HasExited) { process.Kill(true); process.WaitForExit(); }
        process.Dispose(); log.Dispose();
    }
    public static async Task RunAsync(string exe, string[] args, string log)
    { using var runner = new Ffmpeg(exe, args, log); await runner.CompleteAsync(); }
}
