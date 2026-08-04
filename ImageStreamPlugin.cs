using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.ImageStream;

/// <summary>
/// Entry point of the ImageStream plugin. Streams an FFmpeg-compatible source to the full device
/// display via the host's full-display renderer API (issue #124). Each use case is its own toggle
/// command whose parameters are set per button in the command editor — there is no plugin settings
/// page. The plugin owns FFmpeg decoding; the host owns scheduling, device transfer, and (while a
/// stream runs) the full input takeover.
/// </summary>
public sealed class ImageStreamPlugin : LoupixPlugin
{
    private ImageStreamController? _controller;
    private List<IPluginCommand> _commands = [];

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "imagestream",
        Name = "ImageStream",
        Version = new Version(1, 0, 0),
        SdkVersion = new Version(1, 18, 0),
        Author = "RadiatorTwo",
        Description = "Stream a video, screen, window or screen region to the full device display."
    };

    public override void Initialize(IPluginHost host)
    {
        _controller = new ImageStreamController(host);
        _commands =
        [
            new StreamSourceCommand(_controller),
            new StreamScreenCommand(_controller),
            new StreamWindowCommand(_controller),
            new StreamRegionCommand(_controller),
        ];
    }

    public override IEnumerable<IPluginCommand> GetCommands() => _commands;

    public override IReadOnlyList<CommandGroupDescriptor> GetCommandGroups() =>
    [
        new CommandGroupDescriptor
        {
            Group = "ImageStream",
            Description = "Stream live content to the display",
            Icon = "\U000F0567",
            Section = CommandGroupSection.Plugins
        }
    ];

    public override void Shutdown() => _controller?.Shutdown();
}
