namespace LoupixDeck.Plugin.ImageStream;

/// <summary>Shared defaults for the ImageStream commands.</summary>
internal static class ImageStreamSettings
{
    /// <summary>Fallback frame rate when a command's FPS parameter is missing or invalid.</summary>
    public const int DefaultFps = 30;

    /// <summary>Default capture region width (used as the Screen Region command's default).</summary>
    public const int DefaultRegionWidth = 1280;

    /// <summary>Default capture region height.</summary>
    public const int DefaultRegionHeight = 720;
}
