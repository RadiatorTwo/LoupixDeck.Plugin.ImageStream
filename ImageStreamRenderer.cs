using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.ImageStream;

/// <summary>
/// Full-display renderer (issue #124) that feeds FFmpeg-decoded BGRA frames to the host. The plugin
/// owns decoding via <see cref="FfmpegFrameSource"/>; the host scheduler pulls the freshest frame each
/// tick through <see cref="RenderFrame"/>. <see cref="Configure"/> supplies the source spec before the
/// controller requests the takeover, so <see cref="OnStart"/> can build the decoder from it.
/// </summary>
internal sealed class ImageStreamRenderer(IPluginHost host) : IFullDisplayRenderer
{
    private readonly IPluginHost _host = host;

    private StreamSpec? _spec;
    private FfmpegFrameSource? _source;
    private long _lastSeq = -1;
    private int _fps = ImageStreamSettings.DefaultFps;

    /// <summary>Sets the source used by the next <see cref="OnStart"/>.</summary>
    public void Configure(StreamSpec spec) => _spec = spec;

    public int TargetFps => _fps;

    // The scheduler polls this: once decoding is torn down (OnStop), stop being ticked.
    public bool IsActive => _source != null;

    public void OnStart(FullDisplaySurface surface)
    {
        StreamSpec? spec = _spec;
        if (spec == null)
            return;

        _fps = Math.Clamp(spec.Fps <= 0 ? ImageStreamSettings.DefaultFps : spec.Fps, 1, 60);
        _lastSeq = -1;
        _source = new FfmpegFrameSource(
            spec.Url, _fps, surface.Width, surface.Height, spec.InputOptions, spec.Realtime, _host.Logger);
        _source.Start();
    }

    public void OnStop()
    {
        _source?.Dispose();
        _source = null;
    }

    public FullDisplayFrameResult RenderFrame(byte[] buffer, in FullDisplayFrameContext frame)
    {
        FfmpegFrameSource? source = _source;
        if (source == null)
            return FullDisplayFrameResult.Skip();

        if (source.TryCopyLatest(buffer, _lastSeq, out long seq))
        {
            _lastSeq = seq;
            return FullDisplayFrameResult.Frame(seq);
        }

        return FullDisplayFrameResult.Skip();
    }
}
