using System.Diagnostics.CodeAnalysis;

namespace DockingScene.Core;

/// <summary>Bounded validation of application-created and deserialized layouts.</summary>
public static class DockLayoutValidator
{
    public static void Validate(DockLayout layout, int maximumWindows = 10000, bool validateActivation = true)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (maximumWindows < 1) throw new ArgumentOutOfRangeException(nameof(maximumWindows));
        if (layout.SchemaVersion != 1) Fail("Unsupported layout schema.");
        if (layout.Windows is null || layout.Windows.Count > maximumWindows || layout.Revision < 0) Fail("Layout limits exceeded.");
        if (layout.FloatingHosts.IsDefault || layout.AutoHideGroups.IsDefault || layout.HiddenWindows.IsDefault || layout.ActivationHistory.IsDefault)
            Fail("Layout collections must be initialized.");
        if (!Enum.IsDefined(layout.MdiMode)) Fail("Unknown MDI mode.");
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        var locations = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<(DockNode Node, int Depth, bool Floating, bool InWorkspace)>();
        int workspaceCount = 0;
        void AddNode(string? id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || !nodes.Add(id)) Fail("Duplicate or invalid node id.");
            if (nodes.Count > (long)maximumWindows * 4 + 32) Fail("Layout node limit exceeded.");
        }
        void AddWindow(string? id, GroupKind? kind = null)
        {
            if (id is null) Fail("Null window reference.");
            if (!layout.Windows.TryGetValue(id, out var window)) Fail("Unknown window reference: " + id);
            if (!locations.Add(id)) Fail("Window occurs in multiple locations: " + id);
            if (kind == GroupKind.Tools && window.Kind != WindowKind.Tool) Fail("Documents cannot enter tool containers.");
        }
        foreach (var (id, window) in layout.Windows)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || window is null || window.Id != id || window.Title is null || window.Title.Length > 65536)
                Fail("Invalid window metadata.");
            if (window.Description is null || window.Bookmark is null || !Enum.IsDefined(window.Bookmark.Side) || window.Bookmark.Index < -1)
                Fail("Invalid window description or bookmark.");
            if (!Enum.IsDefined(window.Kind) || !Enum.IsDefined(window.DisplayMode) || !Enum.IsDefined(window.MdiState)) Fail("Invalid window enum.");
            if ((window.Capabilities & ~DockCapabilities.All) != 0) Fail("Unknown capability.");
            if (!window.MinimumSize.IsFinite || !window.MaximumSize.IsFinite || !window.DesiredSize.IsFinite ||
                window.MinimumSize.Width < 0 || window.MinimumSize.Height < 0 || window.MaximumSize.Width < window.MinimumSize.Width || window.MaximumSize.Height < window.MinimumSize.Height ||
                window.DesiredSize.Width < 0 || window.DesiredSize.Height < 0) Fail("Invalid size constraints.");
            if (!ValidBounds(window.FloatingBounds) || !ValidBounds(window.MdiBounds) || !double.IsFinite(window.AutoHideSize) || window.AutoHideSize < 40) Fail("Invalid window geometry.");
            if (window.TabColor is { } color && (color.Length is not (7 or 9) || color[0] != '#' || color.Skip(1).Any(c => !Uri.IsHexDigit(c))))
                Fail("Tab colors must use #RRGGBB or #AARRGGBB notation.");
            if (window.Kind == WindowKind.Tool && window.DisplayMode == TabDisplayMode.Preview) Fail("Preview mode is for documents.");
        }
        if (layout.Root is { } root) stack.Push((root, 0, false, false));
        foreach (var host in layout.FloatingHosts)
        {
            if (host is null || host.Root is null || !ValidBounds(host.Bounds)) Fail("Invalid floating host.");
            AddNode(host.Id); stack.Push((host.Root, 0, true, false));
        }
        while (stack.TryPop(out var entry))
        {
            if (entry.Depth > 64) Fail("Dock tree is too deep.");
            AddNode(entry.Node.Id);
            switch (entry.Node)
            {
                case DockGroup group:
                    if (group.Windows.IsDefaultOrEmpty || !Enum.IsDefined(group.Kind) || !Enum.IsDefined(group.TabPlacement)) Fail("Invalid group.");
                    foreach (var id in group.Windows) AddWindow(id, group.Kind);
                    if (group.SelectedId is null || !group.Windows.Contains(group.SelectedId)) Fail("Invalid selected window.");
                    if (group.Windows.Count(id => layout.Windows[id].DisplayMode == TabDisplayMode.Preview) > 1) Fail("A group can contain only one preview tab.");
                    break;
                case DockSplit split:
                    if (!Enum.IsDefined(split.Axis) || !double.IsFinite(split.Ratio) || split.Ratio <= 0 || split.Ratio >= 1 || split.First is null || split.Second is null)
                        Fail("Invalid split.");
                    stack.Push((split.Second, entry.Depth + 1, entry.Floating, entry.InWorkspace));
                    stack.Push((split.First, entry.Depth + 1, entry.Floating, entry.InWorkspace));
                    break;
                case DockWorkspace workspace:
                    if (entry.Floating || entry.InWorkspace || ++workspaceCount > 1) Fail("Only one non-nested primary workspace is allowed; nest DockSite controls instead.");
                    if (workspace.Content is { } content) stack.Push((content, entry.Depth + 1, false, true));
                    break;
                default: Fail("Unknown node type."); break;
            }
        }
        foreach (var group in layout.AutoHideGroups)
        {
            if (group is null) Fail("Invalid auto-hide group.");
            AddNode(group.Id);
            if (group.Side == DockSide.Center || !Enum.IsDefined(group.Side) || group.Windows.IsDefaultOrEmpty) Fail("Invalid auto-hide group.");
            foreach (var id in group.Windows) AddWindow(id, GroupKind.Tools);
            if (group.SelectedId is null || !group.Windows.Contains(group.SelectedId)) Fail("Invalid auto-hide selection.");
        }
        foreach (var id in layout.HiddenWindows) AddWindow(id);
        if (locations.Count != layout.Windows.Count) Fail("Every registered window must have exactly one location.");
        var hidden = layout.HiddenWindows.ToHashSet(StringComparer.Ordinal);
        if (validateActivation)
        {
            if (layout.ActiveWindowId is not null && (!locations.Contains(layout.ActiveWindowId) || hidden.Contains(layout.ActiveWindowId))) Fail("Active window is not open.");
            if (layout.ActivationHistory.Distinct(StringComparer.Ordinal).Count() != layout.ActivationHistory.Length ||
                layout.ActivationHistory.Any(id => id is null || !locations.Contains(id) || hidden.Contains(id))) Fail("Invalid activation history.");
        }
    }
    private static bool ValidBounds(DockRect bounds) => bounds.IsValid && double.IsFinite(bounds.Right) && double.IsFinite(bounds.Bottom);
    [DoesNotReturn]
    private static void Fail(string message) => throw new InvalidOperationException(message);
}
