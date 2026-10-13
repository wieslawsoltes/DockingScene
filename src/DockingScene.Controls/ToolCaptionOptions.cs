namespace DockingScene.Controls;

/// <summary>Presentation settings for retained tool-container buttons. Hiding a
/// button does not change model capabilities or remove alternative commands.</summary>
public readonly record struct ToolCaptionOptions(bool HasCloseButton, bool HasOptionsButton, bool HasToggleAutoHideButton)
{
    public ToolCaptionOptions() : this(true, true, true) { }
    public static ToolCaptionOptions Default => new();
}
