using System.Globalization;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.ImageStream;

/// <summary>
/// Shared base for the ImageStream toggle commands. Each command resolves a <see cref="StreamSpec"/>
/// from its per-button parameters and toggles it on the shared <see cref="ImageStreamController"/>.
/// While a stream runs the device is taken over (buttons inert) and any press stops it, so from the
/// device a press only ever starts; the toggle-off happens through the host's takeover input handling.
/// </summary>
internal abstract class StreamCommandBase(ImageStreamController controller) : IPluginCommand
{
    private static readonly TimeSpan HintDuration = TimeSpan.FromSeconds(2);

    protected ImageStreamController Controller { get; } = controller;

    public abstract CommandDescriptor Descriptor { get; }

    public ButtonTargets SupportedTargets => ButtonTargets.All;

    public abstract Task Execute(CommandContext ctx);

    protected static string? Param(CommandContext ctx, int index) =>
        index < ctx.Parameters.Length ? ctx.Parameters[index] : null;

    protected static int ParseFps(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0
            ? Math.Clamp(v, 1, 60)
            : ImageStreamSettings.DefaultFps;

    protected static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    protected static void Warn(CommandContext ctx, string log, string overlay)
    {
        ctx.Host.Logger.Warn($"ImageStream: {log}");
        ctx.Host.OverlayTouchText(0, overlay, HintDuration);
    }
}

/// <summary>Streams a local video file or a network URL (rtsp/http/udp) to the full display.</summary>
internal sealed class StreamSourceCommand(ImageStreamController controller) : StreamCommandBase(controller)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "ImageStream.StreamSource",
        DisplayName = "ImageStream: Video / Stream",
        Group = "ImageStream",
        Icon = "\U000F0567",
        Description = "Toggle streaming a video file or network URL to the full display.",
        ParameterTemplate = "({FPS},{Source})",
        Parameters =
        [
            new CommandParameter("FPS", typeof(int)) { DefaultValue = ImageStreamSettings.DefaultFps.ToString(CultureInfo.InvariantCulture) },
            new CommandParameter("Source", typeof(string)) { DefaultValue = string.Empty }
        ]
    };

    public override Task Execute(CommandContext ctx)
    {
        try
        {
            int fps = ParseFps(Param(ctx, 0));
            string source = Param(ctx, 1) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                Warn(ctx, "no source configured for Video / Stream.", "No source set");
                return Task.CompletedTask;
            }

            Controller.Toggle(new StreamSpec(source.Trim(), string.Empty, fps, Realtime: true));
        }
        catch (Exception ex) { ctx.Host.Logger.Error("ImageStream.StreamSource failed", ex); }

        return Task.CompletedTask;
    }
}

/// <summary>Streams the entire desktop to the full display.</summary>
internal sealed class StreamScreenCommand(ImageStreamController controller) : StreamCommandBase(controller)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "ImageStream.StreamScreen",
        DisplayName = "ImageStream: Full Screen",
        Group = "ImageStream",
        Icon = "\U000F0379",
        Description = "Toggle streaming the whole desktop to the full display.",
        ParameterTemplate = "({FPS})",
        Parameters =
        [
            new CommandParameter("FPS", typeof(int)) { DefaultValue = ImageStreamSettings.DefaultFps.ToString(CultureInfo.InvariantCulture) }
        ]
    };

    public override Task Execute(CommandContext ctx)
    {
        try
        {
            int fps = ParseFps(Param(ctx, 0));
            Controller.Toggle(CaptureArgs.Screen(fps));
        }
        catch (Exception ex) { ctx.Host.Logger.Error("ImageStream.StreamScreen failed", ex); }

        return Task.CompletedTask;
    }
}

/// <summary>Streams a single window identified by its title (Windows only).</summary>
internal sealed class StreamWindowCommand(ImageStreamController controller) : StreamCommandBase(controller)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "ImageStream.StreamWindow",
        DisplayName = "ImageStream: Window",
        Group = "ImageStream",
        Icon = "\U000F040B",
        Description = "Toggle streaming a window (by title) to the full display. Windows only.",
        ParameterTemplate = "({FPS},{Title})",
        Parameters =
        [
            new CommandParameter("FPS", typeof(int)) { DefaultValue = ImageStreamSettings.DefaultFps.ToString(CultureInfo.InvariantCulture) },
            new CommandParameter("Title", typeof(string)) { DefaultValue = string.Empty }
        ]
    };

    public override Task Execute(CommandContext ctx)
    {
        try
        {
            if (!CaptureArgs.WindowSupported)
            {
                Warn(ctx, "window capture is only supported on Windows.", "Windows only");
                return Task.CompletedTask;
            }

            int fps = ParseFps(Param(ctx, 0));
            string title = Param(ctx, 1) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                Warn(ctx, "no window title configured.", "No window title");
                return Task.CompletedTask;
            }

            Controller.Toggle(CaptureArgs.Window(title.Trim(), fps));
        }
        catch (Exception ex) { ctx.Host.Logger.Error("ImageStream.StreamWindow failed", ex); }

        return Task.CompletedTask;
    }
}

/// <summary>Streams a rectangular region of the screen to the full display.</summary>
internal sealed class StreamRegionCommand(ImageStreamController controller) : StreamCommandBase(controller)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "ImageStream.StreamRegion",
        DisplayName = "ImageStream: Screen Region",
        Group = "ImageStream",
        Icon = "\U000F0A0C",
        Description = "Toggle streaming a rectangular screen region to the full display.",
        ParameterTemplate = "({X},{Y},{Width},{Height},{FPS})",
        Parameters =
        [
            new CommandParameter("X", typeof(int)) { DefaultValue = "0" },
            new CommandParameter("Y", typeof(int)) { DefaultValue = "0" },
            new CommandParameter("Width", typeof(int)) { DefaultValue = ImageStreamSettings.DefaultRegionWidth.ToString(CultureInfo.InvariantCulture) },
            new CommandParameter("Height", typeof(int)) { DefaultValue = ImageStreamSettings.DefaultRegionHeight.ToString(CultureInfo.InvariantCulture) },
            new CommandParameter("FPS", typeof(int)) { DefaultValue = ImageStreamSettings.DefaultFps.ToString(CultureInfo.InvariantCulture) }
        ]
    };

    public override Task Execute(CommandContext ctx)
    {
        try
        {
            // All five fields have non-empty defaults, so a full parameter list has five values.
            // A shorter list means the user cleared a field (the parser drops empty values), which
            // would misalign the positions — refuse rather than capture the wrong rectangle.
            if (ctx.Parameters.Length < 5)
            {
                Warn(ctx, "region parameters incomplete (do not clear the number fields).", "Bad region");
                return Task.CompletedTask;
            }

            int x = ParseInt(Param(ctx, 0), 0);
            int y = ParseInt(Param(ctx, 1), 0);
            int width = ParseInt(Param(ctx, 2), ImageStreamSettings.DefaultRegionWidth);
            int height = ParseInt(Param(ctx, 3), ImageStreamSettings.DefaultRegionHeight);
            int fps = ParseFps(Param(ctx, 4));

            if (width <= 0 || height <= 0)
            {
                Warn(ctx, "region width/height must be positive.", "Bad region size");
                return Task.CompletedTask;
            }

            Controller.Toggle(CaptureArgs.Region(x, y, width, height, fps));
        }
        catch (Exception ex) { ctx.Host.Logger.Error("ImageStream.StreamRegion failed", ex); }

        return Task.CompletedTask;
    }
}
