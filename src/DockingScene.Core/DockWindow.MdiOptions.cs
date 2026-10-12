namespace DockingScene.Core;

public sealed partial record DockWindow
{
    /// <summary>Allow the MDI caption/primary UI to minimize this window.
    /// Direct manager state changes still use capabilities, not this UI policy.</summary>
    public bool CanStandardMdiMinimize { get; init; } = true;
    /// <summary>Allow the MDI maximize button and title double-click.
    /// Restore remains available when a window was maximized programmatically.</summary>
    public bool CanStandardMdiMaximize { get; init; } = true;
}
