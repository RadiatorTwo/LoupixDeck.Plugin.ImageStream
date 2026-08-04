using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.ImageStream;

/// <summary>
/// Owns the single full-display streaming session shared by all ImageStream commands. Toggling a
/// source starts it (taking the display over); toggling the same source again stops it; toggling a
/// different source switches. On the device a running stream is a full takeover (any press stops it
/// via the host), so a device press only ever starts — the switch/stop branches serve programmatic
/// or chained invocation.
/// </summary>
internal sealed class ImageStreamController(IPluginHost host)
{
    private static readonly TimeSpan HintDuration = TimeSpan.FromSeconds(2);

    private readonly IPluginHost _host = host;
    private readonly ImageStreamRenderer _renderer = new(host);
    private readonly object _gate = new();

    private IFullDisplayRenderSession? _session;
    private StreamSpec? _activeSpec;

    public void Toggle(StreamSpec spec)
    {
        lock (_gate)
        {
            if (_session is { IsActive: true })
            {
                bool sameSource = _activeSpec == spec;
                _session.Release();
                _session = null;
                _activeSpec = null;

                if (sameSource)
                    return; // toggled the active source off

                // Different source → fall through and switch to it.
            }

            StartLocked(spec);
        }
    }

    private void StartLocked(StreamSpec spec)
    {
        if (!FfmpegLocator.IsAvailable())
        {
            _host.Logger.Warn("ImageStream: ffmpeg not found on PATH.");
            _host.OverlayTouchText(0, "ffmpeg not found", HintDuration);
            return;
        }

        _renderer.Configure(spec);

        IFullDisplayRenderSession? session = _host.RequestFullDisplayRenderer(_renderer);
        if (session == null)
        {
            _host.Logger.Warn("ImageStream: the display is already in use (exclusive mode or another stream).");
            _host.OverlayTouchText(0, "Display busy", HintDuration);
            return;
        }

        _session = session;
        _activeSpec = spec;
    }

    public void Shutdown()
    {
        lock (_gate)
        {
            _session?.Release();
            _session = null;
            _activeSpec = null;
        }
    }
}
