using System.Diagnostics;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.ImageStream;

/// <summary>
/// Decodes an FFmpeg-compatible input into a stream of raw BGRA frames and keeps only the freshest
/// one available. The host's animation scheduler paces how often frames are pulled
/// (<see cref="TryCopyLatest"/>), so a single latest-frame slot is enough — when decoding outruns
/// presentation the newest frame simply overwrites the previous, which drops stale frames instead of
/// sliding into slow motion.
///
/// A background worker runs ffmpeg and, on end-of-stream or failure, reconnects after a short
/// backoff. That both recovers live streams (RTSP/HTTP) and loops local files. Mirrors the decode
/// half of the host's internal screensaver source, but owned by the plugin.
/// </summary>
internal sealed class FfmpegFrameSource : IDisposable
{
    private const int ReconnectDelayMs = 500;

    private readonly string _url;
    private readonly int _fps;
    private readonly int _width;
    private readonly int _height;
    private readonly int _frameBytes;
    private readonly string _inputOptions;
    private readonly bool _realtime;
    private readonly IPluginLogger? _logger;

    private readonly object _swapLock = new();
    private byte[] _latest;
    private byte[] _scratch;
    private long _seq;

    private CancellationTokenSource? _cts;
    private Task? _worker;
    private Process? _process;

    public FfmpegFrameSource(string url, int fps, int width, int height,
        string inputOptions, bool realtime, IPluginLogger? logger)
    {
        _url = url;
        _fps = fps;
        _width = width;
        _height = height;
        _frameBytes = width * height * 4;
        _inputOptions = inputOptions ?? string.Empty;
        _realtime = realtime;
        _logger = logger;
        _latest = new byte[_frameBytes];
        _scratch = new byte[_frameBytes];
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _worker = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>
    /// Copies the freshest decoded frame into <paramref name="dest"/> when a newer one exists than
    /// <paramref name="sinceSeq"/>. Returns false (leaving <paramref name="dest"/> untouched) when no
    /// frame has been decoded yet or none is newer, so the caller can skip a redundant device push.
    /// </summary>
    public bool TryCopyLatest(byte[] dest, long sinceSeq, out long seq)
    {
        lock (_swapLock)
        {
            seq = _seq;
            if (_seq == 0 || _seq == sinceSeq)
                return false;

            Buffer.BlockCopy(_latest, 0, dest, 0, _frameBytes);
            return true;
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await DecodeOnceAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"ImageStream: ffmpeg decode failed: {ex.Message}");
            }

            if (token.IsCancellationRequested)
                break;

            // Backoff before reconnecting (also the gap when looping a finished local file).
            try { await Task.Delay(ReconnectDelayMs, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DecodeOnceAsync(CancellationToken token)
    {
        // Argument layout: global opts, then INPUT opts (before -i), then OUTPUT opts. Mirrors the
        // host screensaver's battle-tested set:
        //  -analyzeduration 0 + small -probesize: start decoding immediately instead of analysing
        //    up to ~5 s first.
        //  -re (optional): pace ffmpeg's output to wall-clock so a local file plays at real speed
        //    (the consumer drops to the freshest frame rather than racing to the end). Off for live
        //    capture (screen/window/webcam), whose input is already realtime.
        //  {inputOptions}: user-supplied input flags before -i, e.g. "-f gdigrab -framerate 30" to
        //    capture the Windows desktop, or "-f x11grab -framerate 30 -video_size 1920x1080" on X11.
        //  scale=…:flags=fast_bilinear: the panel is tiny, so the cheapest scaler is plenty.
        // Do NOT add "-fflags nobuffer" — on some clips it makes ffmpeg pad the front with hundreds
        // of duplicated frames, so the stream looks frozen on launch.
        string reArg = _realtime ? "-re " : string.Empty;
        string inOpts = _inputOptions.Length == 0 ? string.Empty : _inputOptions.Trim() + " ";
        string args =
            "-hide_banner -loglevel error " +
            "-probesize 500000 -analyzeduration 0 " +
            reArg +
            inOpts +
            $"-i \"{_url}\" " +
            $"-an -f rawvideo -r {_fps} -pix_fmt bgra " +
            $"-vf scale={_width}:{_height}:flags=fast_bilinear -";

        Process process;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("ffmpeg failed to start (is it on PATH?).");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"ffmpeg start failed: {ex.Message}", ex);
        }

        _process = process;

        // Drain stderr continuously so ffmpeg never stalls on a full pipe.
        Task drainErr = Task.Run(async () =>
        {
            try { _ = await process.StandardError.ReadToEndAsync(token).ConfigureAwait(false); }
            catch { /* killed / cancelled */ }
        }, token);

        try
        {
            Stream stdout = process.StandardOutput.BaseStream;
            byte[] buffer = _scratch;

            while (!token.IsCancellationRequested)
            {
                // Read exactly one full BGRA frame.
                int read = 0;
                while (read < _frameBytes)
                {
                    int r = await stdout.ReadAsync(buffer.AsMemory(read, _frameBytes - read), token)
                        .ConfigureAwait(false);
                    if (r <= 0)
                        return; // EOF — the clip ended or ffmpeg exited; RunAsync reconnects.

                    read += r;
                }

                // Publish the completed frame by swapping it into the latest slot (no copy); the
                // consumer copies out under the same lock.
                lock (_swapLock)
                {
                    (_scratch, _latest) = (_latest, buffer);
                    _seq++;
                }
                buffer = _scratch;
            }
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch { /* already gone */ }
            try { await drainErr.ConfigureAwait(false); } catch { /* ignore */ }
            try { process.Dispose(); } catch { /* ignore */ }
            if (ReferenceEquals(_process, process))
                _process = null;
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* already disposed */ }
        try { if (_process is { HasExited: false }) _process.Kill(true); } catch { /* already gone */ }
        try { _worker?.Wait(1000); } catch { /* best effort */ }
        try { _cts?.Dispose(); } catch { /* ignore */ }
        _cts = null;
    }
}
