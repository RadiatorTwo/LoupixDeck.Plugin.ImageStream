using System.Diagnostics;

namespace LoupixDeck.Plugin.ImageStream;

/// <summary>
/// Detects whether an <c>ffmpeg</c> executable is available on PATH. The host has its own internal
/// detector, but that is not exposed to plugins, so ImageStream probes independently. The invocation
/// is identical on Windows and Linux, so no platform split is needed here. The result is cached for
/// the process lifetime.
/// </summary>
internal static class FfmpegLocator
{
    private static readonly object Gate = new();
    private static bool? _available;

    public static bool IsAvailable()
    {
        lock (Gate)
        {
            _available ??= Probe();
            return _available.Value;
        }
    }

    private static bool Probe()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
                return false;

            if (!process.WaitForExit(3000))
            {
                try { process.Kill(true); } catch { /* ignore */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            // ffmpeg not on PATH, or not executable.
            return false;
        }
    }
}
