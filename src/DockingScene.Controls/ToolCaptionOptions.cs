using DockingScene.Core;

namespace DockingScene.Controls;

/// <summary>Presentation settings for retained tool-container buttons. Hiding a
/// button does not change model capabilities or remove alternative commands.</summary>
public readonly record struct ToolCaptionOptions(bool HasCloseButton, bool HasOptionsButton, bool HasToggleAutoHideButton)
{
    public ToolCaptionOptions() : this(true, true, true) { }
    public static ToolCaptionOptions Default => new();
    public bool HasTitleBar { get; init; } = true;
    public ToolCaptionSettings Resolve(DockWindow window) => new ToolCaptionSettings(HasTitleBar,
        HasCloseButton, HasOptionsButton, HasToggleAutoHideButton).Resolve(window);
}
