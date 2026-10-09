namespace DockingScene.Core;

/// <summary>Checks untrusted layouts before they can become observable application state.</summary>
public static class DockLayoutValidator
{
    public static void Validate(DockLayout layout, int maximumWindows = 10000)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.SchemaVersion != 1) Fail("Unsupported layout schema.");
        if (layout.Windows.Count > maximumWindows || layout.Revision < 0) Fail("Layout limits exceeded.");
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        var locations = new HashSet<string>(StringComparer.Ordinal);
        void AddNode(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || !nodes.Add(id)) Fail("Duplicate or invalid node id.");
        }
        void AddWindow(string id, GroupKind? kind = null)
        {
            if (!layout.Windows.TryGetValue(id, out var window)) Fail("Unknown window reference: " + id);
            if (!locations.Add(id)) Fail("Window occurs in multiple locations: " + id);
            if (kind == GroupKind.Tools && window!.Kind != WindowKind.Tool) Fail("Documents cannot enter tool containers.");
        }
        void Visit(DockNode? node, int depth, bool floating)
        {
            if (node is null) return;
            if (depth > 64) Fail("Dock tree is too deep.");
            AddNode(node.Id);
            switch (node)
            {
                case DockGroup g:
                    if (g.Windows.IsDefaultOrEmpty || !Enum.IsDefined(g.Kind) || !Enum.IsDefined(g.TabPlacement)) Fail("Invalid group.");
                    foreach (var id in g.Windows) AddWindow(id, g.Kind);
                    if (g.SelectedId is null || !g.Windows.Contains(g.SelectedId)) Fail("Invalid selected window.");
                    if (g.Windows.Count(id => layout.Windows[id].DisplayMode == TabDisplayMode.Preview) > 1) Fail("A group can contain only one preview tab.");
                    break;
                case DockSplit s:
                    if (!Enum.IsDefined(s.Axis) || !double.IsFinite(s.Ratio) || s.Ratio <= 0 || s.Ratio >= 1 || s.First is null || s.Second is null) Fail("Invalid split.");
                    Visit(s.First, depth + 1, floating); Visit(s.Second, depth + 1, floating);
                    break;
                case DockWorkspace w:
                    if (floating || DockTree.Descendants(w.Content).Any(n => n is DockWorkspace)) Fail("Nested layout workspaces are not allowed; nest DockSite controls instead.");
                    Visit(w.Content, depth + 1, false);
                    break;
                default: Fail("Unknown node type."); break;
            }
        }
        foreach (var (id, window) in layout.Windows)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || window.Id != id || window.Title is null || window.Title.Length > 65536) Fail("Invalid window metadata.");
            if (!Enum.IsDefined(window.Kind) || !Enum.IsDefined(window.DisplayMode) || !Enum.IsDefined(window.MdiState)) Fail("Invalid window enum.");
            if ((window.Capabilities & ~DockCapabilities.All) != 0) Fail("Unknown capability.");
            if (!window.MinimumSize.IsFinite || !window.MaximumSize.IsFinite || !window.DesiredSize.IsFinite ||
                window.MinimumSize.Width < 0 || window.MinimumSize.Height < 0 || window.MaximumSize.Width < window.MinimumSize.Width || window.MaximumSize.Height < window.MinimumSize.Height ||
                window.DesiredSize.Width < 0 || window.DesiredSize.Height < 0) Fail("Invalid size constraints.");
            if (!window.FloatingBounds.IsValid || !window.MdiBounds.IsValid || !double.IsFinite(window.AutoHideSize) || window.AutoHideSize < 40) Fail("Invalid window geometry.");
            if (window.Kind == WindowKind.Tool && window.DisplayMode == TabDisplayMode.Preview) Fail("Preview mode is for documents.");
        }
        if (DockTree.Descendants(layout.Root).Count(n => n is DockWorkspace) > 1) Fail("Multiple workspaces.");
        Visit(layout.Root, 0, false);
        foreach (var host in layout.FloatingHosts)
        {
            AddNode(host.Id);
            if (!host.Bounds.IsValid || host.Root is null) Fail("Invalid floating host.");
            Visit(host.Root, 0, true);
        }
        foreach (var group in layout.AutoHideGroups)
        {
            AddNode(group.Id);
            if (group.Side == DockSide.Center || !Enum.IsDefined(group.Side) || group.Windows.IsDefaultOrEmpty) Fail("Invalid auto-hide group.");
            foreach (var id in group.Windows) AddWindow(id, GroupKind.Tools);
            if (group.SelectedId is null || !group.Windows.Contains(group.SelectedId)) Fail("Invalid auto-hide selection.");
        }
        foreach (var id in layout.HiddenWindows) AddWindow(id);
        if (locations.Count != layout.Windows.Count) Fail("Every registered window must have exactly one location.");
        if (layout.ActiveWindowId is not null && (!locations.Contains(layout.ActiveWindowId) || layout.HiddenWindows.Contains(layout.ActiveWindowId))) Fail("Active window is not open.");
        if (layout.ActivationHistory.Distinct(StringComparer.Ordinal).Count() != layout.ActivationHistory.Length ||
            layout.ActivationHistory.Any(id => !locations.Contains(id) || layout.HiddenWindows.Contains(id))) Fail("Invalid activation history.");
        if (!Enum.IsDefined(layout.MdiMode)) Fail("Unknown MDI mode.");
    }
    private static void Fail(string message) => throw new InvalidOperationException(message);
}
