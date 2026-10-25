using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
namespace PolarRecorder.Services;
// Insert silence against a monotonic clock: WASAPI loopback emits no packets during silence.
public sealed class AudioTrack
{
    private readonly WasapiCapture capture;
    private readonly WaveFileWriter writer;
    private readonly Stopwatch clock;
    private readonly object gate = new();
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly byte[] silence = new byte[65536];
    private bool finished;
    private bool started;
    private Exception? failure;
    public AudioTrack(bool loopback, string path, Stopwatch clock)
    {
        this.clock = clock;
        capture = loopback ? new WasapiLoopbackCapture() : new WasapiCapture();
        try { writer = new WaveFileWriter(path, capture.WaveFormat); }
        catch { capture.Dispose(); throw; }
        capture.DataAvailable += (_, e) => {
            lock (gate)
            {
                if (finished || failure != null) return;
                try {
                    long target = AlignedBytes(clock.Elapsed.TotalSeconds) - e.BytesRecorded;
                    // Allow packet jitter, but fill real idle periods (especially loopback silence).
                    if (target - writer.Length > writer.WaveFormat.AverageBytesPerSecond / 20) PadTo(target);
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                } catch (Exception ex) { failure = ex; }
            }
        };
        capture.RecordingStopped += (_, e) => { if (e.Exception != null) failure = e.Exception; stopped.TrySetResult(); };
    }
    public bool HasFailed => failure != null;
    public void Start() { capture.StartRecording(); started = true; }
    private long AlignedBytes(double seconds)
    { var format = writer.WaveFormat; return (long)(seconds * format.SampleRate) * format.BlockAlign; }
    private void PadTo(long target)
    {
        int align = writer.WaveFormat.BlockAlign;
        while (writer.Length < target) { int n = (int)Math.Min(silence.Length / align * align, target - writer.Length); writer.Write(silence, 0, n); }
    }
    public async Task StopAsync(double seconds)
    {
        try {
            if (started) { capture.StopRecording(); await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        }
        finally {
            lock (gate) { finished = true; try { if (failure == null) PadTo(AlignedBytes(seconds)); } finally { writer.Dispose(); } }
            capture.Dispose();
        }
        if (failure != null) throw new InvalidOperationException("오디오 녹음에 실패했습니다.", failure);
    }
}
