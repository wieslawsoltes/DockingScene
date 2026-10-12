using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace DockingScene.Core;

public enum WindowKind { Document, Tool }
public enum GroupKind { Documents, Tools }
public enum DockSide { Center, Left, Top, Right, Bottom }
public enum SplitAxis { Horizontal, Vertical }
public enum WindowPlacement { Hidden, Docked, Document, AutoHidden, Floating }
public enum TabDisplayMode { Normal, Pinned, Preview }
public enum MdiMode { Tabbed, Standard }
public enum MdiWindowState { Normal, Minimized, Maximized }
public enum TabPlacement { Top, Bottom, Left, Right }

[Flags]
public enum DockCapabilities
{
    None = 0, Close = 1, Drag = 2, Float = 4, AutoHide = 8,
    Dock = 16, Attach = 32, EnterWorkspace = 64, Resize = 128,
    All = Close | Drag | Float | AutoHide | Dock | Attach | EnterWorkspace | Resize
}

public readonly record struct DockSize(double Width, double Height)
{
    public static DockSize Minimum => new(80, 60);
    public static DockSize Maximum => new(100000, 100000);
    public bool IsFinite => double.IsFinite(Width) && double.IsFinite(Height);
}

public readonly record struct DockRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) &&
        double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
    public bool Contains(double x, double y) => x >= X && y >= Y && x < Right && y < Bottom;
    public DockRect Deflate(double n) => new(X + n, Y + n, Math.Max(0, Width - 2 * n), Math.Max(0, Height - 2 * n));
}

public sealed record DockBookmark(string? GroupId = null, DockSide Side = DockSide.Left, int Index = -1);

/// <summary>Serializable window metadata. Application content is deliberately not stored here.</summary>
public sealed partial record DockWindow(string Id, string Title, WindowKind Kind)
{
    public string? ContentKey { get; init; }
    public string Description { get; init; } = "";
    public DockCapabilities Capabilities { get; init; } = DockCapabilities.All;
    public TabDisplayMode DisplayMode { get; init; }
    public bool IsModified { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsFlashing { get; init; }
    public bool HasTitleBar { get; init; } = true;
    public string? TabColor { get; init; }
    public DockSize MinimumSize { get; init; } = DockSize.Minimum;
    public DockSize MaximumSize { get; init; } = DockSize.Maximum;
    public DockSize DesiredSize { get; init; } = new(280, 220);
    public DockRect FloatingBounds { get; init; } = new(100, 100, 540, 360);
    public DockRect MdiBounds { get; init; } = new(20, 20, 480, 300);
    public MdiWindowState MdiState { get; init; }
    public double AutoHideSize { get; init; } = 280;
    public DockBookmark Bookmark { get; init; } = new();
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DockGroup), "group")]
[JsonDerivedType(typeof(DockSplit), "split")]
[JsonDerivedType(typeof(DockWorkspace), "workspace")]
public abstract record DockNode(string Id);
public sealed record DockGroup(string Id, GroupKind Kind, ImmutableArray<string> Windows, string? SelectedId = null) : DockNode(Id)
{
    public TabPlacement TabPlacement { get; init; } = TabPlacement.Top;
}
public sealed record DockSplit(string Id, SplitAxis Axis, double Ratio, DockNode First, DockNode Second) : DockNode(Id);
public sealed record DockWorkspace(string Id, DockNode? Content = null) : DockNode(Id);
public sealed record AutoHideGroup(string Id, DockSide Side, ImmutableArray<string> Windows, string? SelectedId = null);
public sealed record FloatingHost(string Id, DockNode Root, DockRect Bounds)
{
    public bool IsMaximized { get; init; }
    public bool IsHosted { get; init; } = true;
}

public sealed record DockLayout
{
    public int SchemaVersion { get; init; } = 1;
    public long Revision { get; init; }
    public DockNode? Root { get; init; } = new DockWorkspace("workspace");
    public ImmutableDictionary<string, DockWindow> Windows { get; init; } = ImmutableDictionary<string, DockWindow>.Empty;
    public ImmutableArray<AutoHideGroup> AutoHideGroups { get; init; } = [];
    public ImmutableArray<FloatingHost> FloatingHosts { get; init; } = [];
    public ImmutableArray<string> HiddenWindows { get; init; } = [];
    public string? ActiveWindowId { get; init; }
    public ImmutableArray<string> ActivationHistory { get; init; } = [];
    public MdiMode MdiMode { get; init; }
    public bool IsLayoutLocked { get; init; }
}

public sealed record WindowLocation(WindowPlacement Placement, string? GroupId, string? HostId, DockSide Side, int Index);
public sealed record DockPolicy
{
    public DockCapabilities AllowedCapabilities { get; init; } = DockCapabilities.All;
    public int HistoryLimit { get; init; } = 100;
    public int MaximumWindows { get; init; } = 10000;
    public bool AutoHideEntireGroup { get; init; } = true;
    public bool AllowMixedDocumentGroups { get; init; } = true;
}
public readonly record struct DockResult(bool Success, long Revision, string? Error = null)
{
    public void ThrowIfFailed() { if (!Success) throw new InvalidOperationException(Error); }
}

public sealed class DockChangingEventArgs(string command, DockLayout before, DockLayout after, ImmutableArray<string> closing) : EventArgs
{
    public string Command { get; } = command;
    public DockLayout Before { get; } = before;
    public DockLayout After { get; } = after;
    public ImmutableArray<string> ClosingWindowIds { get; } = closing;
    public bool Cancel { get; set; }
    public string? Reason { get; set; }
}
public sealed class DockChangedEventArgs(string command, DockLayout before, DockLayout after) : EventArgs
{
    public string Command { get; } = command;
    public DockLayout Before { get; } = before;
    public DockLayout After { get; } = after;
}
