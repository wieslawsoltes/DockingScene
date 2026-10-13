namespace DockingScene.Core;

public sealed partial record DockWindow
{
    /// <summary>Null inherits the host's options-button default. Presentation does not grant a docking capability.</summary>
    public bool? HasOptionsButton { get; init; }
    /// <summary>Optional per-window close-button presentation. Close capabilities and vetoes still apply.</summary>
    public bool? HasCloseButton { get; init; }
    /// <summary>Optional per-window pin-button presentation. Dock/auto-hide capabilities still apply.</summary>
    public bool? HasToggleAutoHideButton { get; init; }
}

/// <summary>Resolved tool-title presentation shared by model consumers and UI adapters.
/// Explicit instance values override host defaults; resolving never mutates metadata,
/// changes capabilities, or allocates. The parameterless constructor enables all four.</summary>
public readonly record struct ToolCaptionSettings(bool HasTitleBar, bool HasCloseButton,
    bool HasOptionsButton, bool HasToggleAutoHideButton)
{
    public ToolCaptionSettings() : this(true, true, true, true) { }
    public static ToolCaptionSettings Default => new();
    public ToolCaptionSettings Resolve(DockWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new(window.HasTitleBar ?? HasTitleBar, window.HasCloseButton ?? HasCloseButton,
            window.HasOptionsButton ?? HasOptionsButton, window.HasToggleAutoHideButton ?? HasToggleAutoHideButton);
    }
}
