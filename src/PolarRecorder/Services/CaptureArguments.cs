using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
namespace PolarRecorder.Services;
public static class CaptureArguments
{
    // Native handle only: no title matching, monitor fallback, or shell interpolation.
    public static string WindowFilter(long handle, Rectangle canvas, bool cursor)
    {
        if (handle <= 0 || canvas.Width < 2 || canvas.Height < 2 || canvas.Width % 2 != 0 || canvas.Height % 2 != 0)
            throw new ArgumentException("잘못된 창 또는 녹화 크기입니다.");
        return FormattableString.Invariant($"gfxcapture=hwnd={handle}:capture_cursor={(cursor ? 1 : 0)}:capture_border=1:display_border=1:max_framerate=30:width={canvas.Width}:height={canvas.Height}:resize_mode=scale_aspect:output_fmt=bgra,hwdownload,format=bgra,format=yuv420p");
    }
    public static string[] Video(Rectangle region, bool cursor, long? window, string output)
    {
        var args = new List<string> { "-hide_banner", "-y", "-progress", "pipe:1", "-stats_period", "0.1" };
        if (window.HasValue) args.AddRange(new[] { "-filter_complex", WindowFilter(window.Value, region, cursor) });
        else args.AddRange(new[] { "-f", "gdigrab", "-framerate", "30", "-draw_mouse", cursor ? "1" : "0",
            "-offset_x", region.X.ToString(CultureInfo.InvariantCulture), "-offset_y", region.Y.ToString(CultureInfo.InvariantCulture),
            "-video_size", $"{region.Width}x{region.Height}", "-i", "desktop" });
        args.AddRange(new[] { "-an", "-c:v", "libx264", "-preset", "veryfast", "-tune", "zerolatency", "-crf", "22", "-pix_fmt", "yuv420p", "-r", "30", output });
        return args.ToArray();
    }
}
