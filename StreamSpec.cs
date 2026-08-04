namespace LoupixDeck.Plugin.ImageStream;

/// <summary>
/// A fully-resolved streaming source: everything <see cref="FfmpegFrameSource"/> needs. Built by a
/// command from its parameters. Value equality (record) lets the controller tell "same source pressed
/// again" (toggle off) from "a different source" (switch).
/// </summary>
/// <param name="Url">The ffmpeg input value that goes into <c>-i "&lt;Url&gt;"</c> (file path, URL,
/// <c>desktop</c>, <c>title=…</c>, …).</param>
/// <param name="InputOptions">Extra ffmpeg input flags placed before <c>-i</c> (e.g. the capture
/// format). Empty for plain files/streams.</param>
/// <param name="Fps">Target frame rate (1–60).</param>
/// <param name="Realtime">Whether to add <c>-re</c> (on for files/loops, off for live capture).</param>
internal sealed record StreamSpec(string Url, string InputOptions, int Fps, bool Realtime);
