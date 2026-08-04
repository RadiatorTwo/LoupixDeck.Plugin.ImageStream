# LoupixDeck.Plugin.ImageStream

Full-display video streaming plugin for [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck),
built against [LoupixDeck.PluginSdk](https://github.com/RadiatorTwo/LoupixDeck.PluginSdk).

Streams an FFmpeg-compatible source — a video file, a network stream, the
desktop, a single window, or a screen region — to the **entire device display**
via the SDK's full-display renderer API (`IFullDisplayRenderer`, SDK 1.18+).

`ffmpeg` must be available on `PATH`. Decoding runs in the plugin; the host owns
scheduling and the device transfer.

## Commands

All four are toggles. While a stream runs the device is fully taken over: the
buttons have no normal function and **any press stops the stream**.

| Command | Parameters | Notes |
|---|---|---|
| `ImageStream.StreamSource` | `FPS`, `Source` | Video file path or network URL (rtsp/http/udp). |
| `ImageStream.StreamScreen` | `FPS` | The whole desktop. |
| `ImageStream.StreamWindow` | `FPS`, `Title` | A window by title. Windows only. |
| `ImageStream.StreamRegion` | `X`, `Y`, `Width`, `Height`, `FPS` | A rectangular screen region. Don't clear the number fields. |

`FPS` defaults to 30 and is clamped to 1–60; the host clamps further to its own
animation limit.

## Build & deploy

```bash
dotnet build LoupixDeck.Plugin.ImageStream.csproj -c Release
```

Copy the build output together with `plugin.json` into
`LoupixDeck/plugins/imagestream/`.
