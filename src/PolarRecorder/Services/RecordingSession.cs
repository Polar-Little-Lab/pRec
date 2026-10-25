using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
namespace PolarRecorder.Services;
public sealed record RecordingOptions(string Ffmpeg, string Folder, Rectangle Region, bool PcAudio, bool Microphone, bool Cursor, WindowTarget? Window = null);
public sealed class RecordingSession
{
    private readonly RecordingOptions options;
    private readonly List<string> segments = new();
    private readonly List<AudioTrack> audio = new();
    private readonly List<string> audioFiles = new();
    private readonly Stopwatch clock = new();
    private Ffmpeg? video;
    private string rawVideo = "";
    private double totalSeconds;
    private int nextCaptureId;
    private bool preserveWork;
    public string WorkDirectory { get; }
    public TimeSpan Duration => TimeSpan.FromSeconds(totalSeconds + clock.Elapsed.TotalSeconds);
    public bool IsCapturing => video != null;
    public bool CaptureFailed => video?.HasExited == true || audio.Any(track => track.HasFailed);
    public string? TargetUnavailable => options.Window == null ? null : WindowTargets.UnavailableReason(options.Window);
    public double SegmentSeconds => clock.Elapsed.TotalSeconds;
    public RecordingSession(RecordingOptions options)
    {
        this.options = options;
        WorkDirectory = Path.Combine(options.Folder, ".polar-work", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkDirectory);
        File.WriteAllText(Path.Combine(WorkDirectory, "session.json"), JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true }));
    }
    public async Task ResumeAsync()
    {
        if (video != null) throw new InvalidOperationException("이미 녹화 중입니다.");
        if (TargetUnavailable is string reason) throw new InvalidOperationException(reason);
        var region = options.Region;
        string prefix = Path.Combine(WorkDirectory, $"{nextCaptureId++:D4}");
        rawVideo = prefix + "-video.mkv";
        audio.Clear(); audioFiles.Clear(); clock.Reset();
        try {
            if (options.PcAudio) AddAudio(true, prefix + "-pc.wav");
            if (options.Microphone) AddAudio(false, prefix + "-mic.wav");
            // Launch clock and audio together. gdigrab's first-frame startup latency must be validated on hardware.
            clock.Start();
            foreach (var track in audio) track.Start();
            video = new Ffmpeg(options.Ffmpeg, CaptureArguments.Video(region, options.Cursor, options.Window?.Handle, rawVideo), Path.Combine(WorkDirectory, "ffmpeg.log"));
            try { await video.WaitForFramesAsync(); }
            catch (Exception ex) when (options.Window != null) {
                throw new InvalidOperationException("선택한 창의 캡처를 시작하지 못했습니다. 창이 열려 있고 최소화되지 않았는지 확인하세요. Windows 창 캡처·D3D11 그래픽 장치를 사용할 수 없거나 보호된 콘텐츠일 수 있습니다. 다른 화면은 녹화하지 않습니다.\n" + ex.Message, ex);
            }
        } catch {
            preserveWork = true;
            clock.Stop();
            video?.Dispose(); video = null;
            foreach (var track in audio) { try { await track.StopAsync(clock.Elapsed.TotalSeconds); } catch { } }
            audio.Clear(); throw;
        }
    }
    private void AddAudio(bool pc, string path) { audio.Add(new AudioTrack(pc, path, clock)); audioFiles.Add(path); }
    public async Task PauseAsync()
    {
        if (video == null) return;
        clock.Stop(); double seconds = clock.Elapsed.TotalSeconds;
        Exception? error = null;
        var stopping = audio.Select(track => track.StopAsync(seconds)).Append(video.StopAsync()).ToArray();
        try { await Task.WhenAll(stopping); } catch (Exception ex) { error = ex; }
        finally { audio.Clear(); video.Dispose(); video = null; totalSeconds += seconds; clock.Reset(); }
        if (error != null) { preserveWork = true; throw new IOException("녹화 구간을 마무리하지 못했습니다. 원본은 임시 폴더에 보존됩니다.", error); }
        string segment = Path.Combine(WorkDirectory, $"segment-{segments.Count:D4}.mp4");
        var args = new List<string> { "-hide_banner", "-y", "-i", rawVideo };
        foreach (string path in audioFiles) args.AddRange(new[] { "-i", path });
        args.AddRange(new[] { "-map", "0:v:0" });
        if (options.Window != null) {
            // WGC sends frames when content changes. Preserve still time at the end of a segment.
            string duration = Math.Max(0.04, seconds).ToString("0.######", CultureInfo.InvariantCulture);
            args.AddRange(new[] { "-vf", $"tpad=stop_mode=clone:stop_duration={duration},trim=duration={duration},setpts=PTS-STARTPTS,fps=30", "-c:v", "libx264", "-preset", "veryfast", "-crf", "22", "-pix_fmt", "yuv420p" });
        } else args.AddRange(new[] { "-c:v", "copy" });
        if (audioFiles.Count > 0) {
            string inputs = string.Concat(Enumerable.Range(1, audioFiles.Count).Select(i => $"[{i}:a]"));
            string filter = audioFiles.Count == 1 ? "[1:a]apad[a]" : inputs + "amix=inputs=2:duration=longest:normalize=1,apad[a]";
            args.AddRange(new[] { "-filter_complex", filter, "-map", "[a]", "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2", "-shortest" });
        }
        args.AddRange(new[] { "-movflags", "+faststart", segment });
        try { await Ffmpeg.RunAsync(options.Ffmpeg, args.ToArray(), Path.Combine(WorkDirectory, "ffmpeg.log")); }
        catch { preserveWork = true; throw; }
        segments.Add(segment);
        File.WriteAllLines(Path.Combine(WorkDirectory, "concat.txt"), segments.Select(s => "file '" + Path.GetFileName(s) + "'"));
        // Delete raw capture only after a playable segment has been created successfully.
        File.Delete(rawVideo); foreach (string path in audioFiles) File.Delete(path);
    }
    public async Task<string> SaveAsync()
    {
        await PauseAsync();
        if (segments.Count == 0) throw new InvalidOperationException("저장할 녹화 구간이 없습니다.");
        string output = Path.Combine(options.Folder, $"Polar_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.mp4");
        string partial = Path.Combine(WorkDirectory, "final.mp4");
        await Ffmpeg.RunAsync(options.Ffmpeg, new[] { "-hide_banner", "-y", "-f", "concat", "-safe", "1", "-i", Path.Combine(WorkDirectory, "concat.txt"), "-c", "copy", "-movflags", "+faststart", partial }, Path.Combine(WorkDirectory, "ffmpeg.log"));
        File.Move(partial, output, overwrite: false);
        try { if (!preserveWork) Directory.Delete(WorkDirectory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return output;
    }
}
