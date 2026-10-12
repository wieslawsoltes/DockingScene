using System.Text.Json.Serialization;

namespace DockingScene.Core;

public sealed partial record DockWindow
{
    /// <summary>Allow the MDI caption/primary UI to minimize this window.
    /// Direct manager state changes still use capabilities, not this UI policy.</summary>
    public bool CanStandardMdiMinimize { get; init; } = true;
    /// <summary>Allow the MDI maximize button and title double-click.
    /// Restore remains available when a window was maximized programmatically.</summary>
    public bool CanStandardMdiMaximize { get; init; } = true;

    /// <summary>Constructor defaults preserve optional-field semantics in .NET 10
    /// source-generated JSON. The existing three-argument constructor and
    /// three-value deconstruction remain unchanged for application consumers.</summary>
    [JsonConstructor]
    public DockWindow(string id, string title, WindowKind kind,
        bool canStandardMdiMinimize = true, bool canStandardMdiMaximize = true)
        : this(id, title, kind)
    {
        CanStandardMdiMinimize = canStandardMdiMinimize;
        CanStandardMdiMaximize = canStandardMdiMaximize;
    }
}
