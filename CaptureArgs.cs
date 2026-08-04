using System.Globalization;

namespace LoupixDeck.Plugin.ImageStream;

/// <summary>
/// Builds the OS-specific FFmpeg input options and input value for screen/window/region capture.
/// Windows uses <c>gdigrab</c>, Linux uses <c>x11grab</c>. All values are plain (no commas), so they
/// survive the host's command-parameter parser.
/// </summary>
internal static class CaptureArgs
{
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Full desktop.</summary>
    public static StreamSpec Screen(int fps) => OperatingSystem.IsWindows()
        ? new StreamSpec("desktop", $"-f gdigrab -framerate {I(fps)}", fps, Realtime: false)
        : new StreamSpec(":0.0", $"-f x11grab -framerate {I(fps)}", fps, Realtime: false);

    /// <summary>Whether window-by-title capture is available on this OS (Windows/gdigrab only).</summary>
    public static bool WindowSupported => OperatingSystem.IsWindows();

    /// <summary>A window identified by its exact title (Windows only; guard with
    /// <see cref="WindowSupported"/>).</summary>
    public static StreamSpec Window(string title, int fps) =>
        new($"title={title}", $"-f gdigrab -framerate {I(fps)}", fps, Realtime: false);

    /// <summary>A rectangular screen region.</summary>
    public static StreamSpec Region(int x, int y, int width, int height, int fps) => OperatingSystem.IsWindows()
        ? new StreamSpec("desktop",
            $"-f gdigrab -framerate {I(fps)} -offset_x {I(x)} -offset_y {I(y)} -video_size {I(width)}x{I(height)}",
            fps, Realtime: false)
        : new StreamSpec($":0.0+{I(x)},{I(y)}",
            $"-f x11grab -framerate {I(fps)} -video_size {I(width)}x{I(height)}",
            fps, Realtime: false);
}
