using System.Collections.Immutable;

namespace DockingScene.Core;

public sealed partial class DockManager
{
    public DockResult Register(DockWindow window, bool open = true, string? targetId = null, DockSide side = DockSide.Center) => Apply("Register", layout =>
    {
        ArgumentNullException.ThrowIfNull(window);
        if (layout.Windows.ContainsKey(window.Id)) throw new InvalidOperationException("Duplicate window id: " + window.Id);
        if (layout.Windows.Count >= Policy.MaximumWindows) throw new InvalidOperationException("Window limit reached.");
        layout = layout with { Windows = layout.Windows.Add(window.Id, window), HiddenWindows = layout.HiddenWindows.Add(window.Id) };
        return open ? Place(layout, [window.Id], targetId, side, window.Kind == WindowKind.Document ? GroupKind.Documents : GroupKind.Tools) : layout;
    });
    public DockResult Open(string id) => Apply("Open", layout =>
    {
        var window = Get(layout, id);
        if (!layout.HiddenWindows.Contains(id)) return DockTree.Activate(layout, id);
        var bookmark = window.Bookmark;
        string? target = bookmark.GroupId is not null && DockTree.Find(layout, bookmark.GroupId) is DockGroup ? bookmark.GroupId : null;
        return Place(layout, [id], target, target is null && window.Kind == WindowKind.Tool ? bookmark.Side : DockSide.Center,
            window.Kind == WindowKind.Document ? GroupKind.Documents : GroupKind.Tools, bookmark.Index);
    });
    public DockResult Activate(string id) => Apply("Activate", layout => DockTree.Activate(layout, id), recordHistory: false);
    public DockResult Close(string id) => CloseMany([id]);
    public DockResult CloseMany(IEnumerable<string> windowIds)
    {
        var ids = windowIds.Distinct(StringComparer.Ordinal).ToImmutableArray();
        return Apply("Close", layout =>
        {
            Require(layout, ids, DockCapabilities.Close, false);
            var result = DockTree.RemoveReferences(layout, ids.ToHashSet(StringComparer.Ordinal));
            return result with { HiddenWindows = result.HiddenWindows.AddRange(ids) };
        }, closing: ids);
    }
    public DockResult Unregister(string id) => Apply("Unregister", layout =>
    {
        Require(layout, [id], DockCapabilities.Close, false);
        var result = DockTree.RemoveReferences(layout, new HashSet<string>([id], StringComparer.Ordinal));
        return result with { Windows = result.Windows.Remove(id) };
    }, closing: [id]);
    public DockResult Dock(string id, string? targetId = null, DockSide side = DockSide.Center, int index = -1, long? expectedRevision = null) =>
        DockMany([id], targetId, side, index, expectedRevision);
    public DockResult DockMany(IEnumerable<string> windowIds, string? targetId, DockSide side, int index = -1, long? expectedRevision = null)
    {
        var ids = windowIds.Distinct(StringComparer.Ordinal).ToImmutableArray();
        return Apply("Dock", layout =>
        {
            if (ids.IsEmpty) throw new InvalidOperationException("No windows to dock.");
            Require(layout, ids, DockCapabilities.Dock);
            var target = ResolveTarget(layout, targetId);
            var kind = target is DockGroup group ? group.Kind : ids.Any(id => Get(layout, id).Kind == WindowKind.Document) ? GroupKind.Documents : GroupKind.Tools;
            if (side == DockSide.Center && target is DockGroup) Require(layout, ids, DockCapabilities.Attach);
            if (kind == GroupKind.Documents && ids.Any(id => Get(layout, id).Kind == WindowKind.Tool))
            {
                if (!Policy.AllowMixedDocumentGroups) throw new InvalidOperationException("Mixed document groups are disabled.");
                Require(layout, ids.Where(id => Get(layout, id).Kind == WindowKind.Tool), DockCapabilities.EnterWorkspace);
            }
            return Place(layout, ids, targetId, side, kind, index);
        }, expectedRevision);
    }
    public DockResult MoveToWorkspace(string id, DockSide side = DockSide.Center) => Apply("MoveToWorkspace", layout =>
    {
        Require(layout, [id], DockCapabilities.EnterWorkspace | DockCapabilities.Dock);
        var workspace = DockTree.Descendants(layout.Root).OfType<DockWorkspace>().FirstOrDefault();
        if (workspace is null) throw new InvalidOperationException("This dock site has no workspace.");
        var target = DockTree.Descendants(workspace.Content).OfType<DockGroup>().FirstOrDefault()?.Id ?? workspace.Id;
        return Place(layout, [id], target, side, GroupKind.Documents);
    });
    public DockResult Float(string id, DockRect? bounds = null) => FloatMany([id], bounds);
    public DockResult FloatMany(IEnumerable<string> windowIds, DockRect? bounds = null)
    {
        var ids = windowIds.Distinct(StringComparer.Ordinal).ToImmutableArray();
        return Apply("Float", layout =>
        {
            if (ids.IsEmpty) throw new InvalidOperationException("No windows to float.");
            Require(layout, ids, DockCapabilities.Float);
            var first = Get(layout, ids[0]);
            var kind = ids.Any(id => Get(layout, id).Kind == WindowKind.Document) || DockTree.GroupOf(layout, ids[0])?.Kind == GroupKind.Documents ? GroupKind.Documents : GroupKind.Tools;
            if (kind == GroupKind.Documents && ids.Any(id => Get(layout, id).Kind == WindowKind.Tool))
            {
                if (!Policy.AllowMixedDocumentGroups) throw new InvalidOperationException("Mixed document groups are disabled.");
                Require(layout, ids.Where(id => Get(layout, id).Kind == WindowKind.Tool), DockCapabilities.EnterWorkspace);
            }
            var rect = bounds ?? first.FloatingBounds;
            if (!rect.IsValid) throw new ArgumentException("Invalid floating bounds.");
            layout = DockTree.RemoveReferences(layout, ids.ToHashSet(StringComparer.Ordinal));
            layout = PromoteExtraPreviews(layout, ids);
            var group = new DockGroup(DockTree.NewId("group"), kind, SortTabs(layout, ids), ids[0]);
            var host = new FloatingHost(DockTree.NewId("float"), group, rect);
            var windows = layout.Windows;
            foreach (var id in ids) windows = windows.SetItem(id, windows[id] with { FloatingBounds = rect });
            return DockTree.Activate(layout with { Windows = windows, FloatingHosts = layout.FloatingHosts.Add(host) }, ids[0]);
        });
    }
    public DockResult AutoHide(string id, DockSide side, bool? entireGroup = null) => Apply("AutoHide", layout =>
    {
        if (side == DockSide.Center || !Enum.IsDefined(side)) throw new ArgumentException("An auto-hide edge is required.");
        var source = DockTree.GroupOf(layout, id);
        var ids = (entireGroup ?? Policy.AutoHideEntireGroup) && source is not null ? source.Windows : ImmutableArray.Create(id);
        Require(layout, ids, DockCapabilities.AutoHide);
        if (ids.Any(key => Get(layout, key).Kind != WindowKind.Tool)) throw new InvalidOperationException("Only tool windows can auto-hide.");
        layout = DockTree.RemoveReferences(layout, ids.ToHashSet(StringComparer.Ordinal));
        return layout with { AutoHideGroups = layout.AutoHideGroups.Add(new(DockTree.NewId("auto"), side, ids, id)) };
    });
    public DockResult PinAutoHide(string id) => Apply("PinAutoHide", layout =>
    {
        var group = layout.AutoHideGroups.FirstOrDefault(g => g.Windows.Contains(id)) ?? throw new InvalidOperationException("Window is not auto-hidden.");
        Require(layout, group.Windows, DockCapabilities.Dock);
        var bookmark = Get(layout, id).Bookmark;
        var target = bookmark.GroupId is not null && DockTree.Find(layout, bookmark.GroupId) is DockGroup ? bookmark.GroupId : null;
        return Place(layout, group.Windows, target, target is null ? group.Side : DockSide.Center, GroupKind.Tools);
    });
    public DockResult Reorder(string id, int index) => Apply("Reorder", layout =>
    {
        Require(layout, [id], DockCapabilities.Drag);
        var group = DockTree.GroupOf(layout, id) ?? throw new InvalidOperationException("No tab group.");
        var ids = group.Windows.Remove(id);
        ids = ids.Insert(Math.Clamp(index, 0, ids.Length), id);
        return DockTree.Map(layout, node => node.Id == group.Id ? group with { Windows = SortTabs(layout, ids) } : node);
    });
    public DockResult UpdateWindow(string id, Func<DockWindow, DockWindow> update) => Apply("UpdateWindow", layout =>
    {
        var before = Get(layout, id);
        var after = update(before);
        if (after is null || after.Id != id || after.Kind != before.Kind) throw new InvalidOperationException("Window identity and kind cannot change.");
        if (after.IsModified && after.DisplayMode == TabDisplayMode.Preview) after = after with { DisplayMode = TabDisplayMode.Normal };
        layout = layout with { Windows = layout.Windows.SetItem(id, after) };
        var group = DockTree.GroupOf(layout, id);
        if (group is not null)
        {
            if (after.DisplayMode == TabDisplayMode.Preview)
                foreach (var other in group.Windows.Where(key => key != id && layout.Windows[key].DisplayMode == TabDisplayMode.Preview))
                    layout = layout with { Windows = layout.Windows.SetItem(other, layout.Windows[other] with { DisplayMode = TabDisplayMode.Normal }) };
            var sorted = SortTabs(layout, group.Windows);
            layout = DockTree.Map(layout, node => node.Id == group.Id ? group with { Windows = sorted } : node);
        }
        return layout;
    });
    public DockResult ResizeSplit(string id, double ratio, long? expectedRevision = null) => Apply("ResizeSplit", layout =>
    {
        if (layout.IsLayoutLocked || !double.IsFinite(ratio)) throw new InvalidOperationException("Cannot resize split.");
        var split = DockTree.Find(layout, id) as DockSplit ?? throw new InvalidOperationException("Unknown splitter.");
        Require(layout, DockTree.WindowIds(split), DockCapabilities.Resize);
        return DockTree.Map(layout, node => node.Id == id ? split with { Ratio = Math.Clamp(ratio, 0.01, 0.99) } : node);
    }, expectedRevision);
    public DockResult SetFloatingBounds(string hostId, DockRect bounds) => Apply("SetFloatingBounds", layout =>
    {
        if (!bounds.IsValid) throw new ArgumentException("Invalid bounds.");
        int index = IndexOfHost(layout, hostId);
        var host = layout.FloatingHosts[index];
        Require(layout, DockTree.WindowIds(host.Root), DockCapabilities.Resize);
        var windows = layout.Windows;
        foreach (var id in DockTree.WindowIds(host.Root)) windows = windows.SetItem(id, windows[id] with { FloatingBounds = bounds });
        return layout with { Windows = windows, FloatingHosts = layout.FloatingHosts.SetItem(index, host with { Bounds = bounds }) };
    });
    public DockResult SetFloatingMaximized(string hostId, bool maximized) => Apply("MaximizeFloating", layout =>
    {
        int index = IndexOfHost(layout, hostId);
        var host = layout.FloatingHosts[index];
        Require(layout, DockTree.WindowIds(host.Root), DockCapabilities.Resize);
        return layout with { FloatingHosts = layout.FloatingHosts.SetItem(index, host with { IsMaximized = maximized }) };
    });
    public DockResult SetMdiMode(MdiMode mode) => Apply("SetMdiMode", layout => layout with { MdiMode = mode });
    public DockResult SetMdiBounds(string id, DockRect bounds) => Apply("SetMdiBounds", layout =>
    {
        Require(layout, [id], DockCapabilities.Resize);
        var window = Get(layout, id);
        return layout with { Windows = layout.Windows.SetItem(id, window with { MdiBounds = bounds, MdiState = MdiWindowState.Normal }) };
    });
    public DockResult SetMdiState(string id, MdiWindowState state) => Apply("SetMdiState", layout =>
    {
        Require(layout, [id], DockCapabilities.Resize);
        var window = Get(layout, id);
        return layout with { Windows = layout.Windows.SetItem(id, window with { MdiState = state }) };
    });
    public DockResult SetAutoHideSize(string id, double size) => Apply("SetAutoHideSize", layout =>
    {
        Require(layout, [id], DockCapabilities.Resize);
        var window = Get(layout, id);
        return layout with { Windows = layout.Windows.SetItem(id, window with { AutoHideSize = size }) };
    });
    public DockResult SetTabPlacement(string groupId, TabPlacement placement) => Apply("SetTabPlacement", layout =>
    {
        if (layout.IsLayoutLocked) throw new InvalidOperationException("The layout is locked.");
        var group = DockTree.Find(layout, groupId) as DockGroup ?? throw new InvalidOperationException("Unknown group.");
        return DockTree.Map(layout, node => node.Id == groupId ? group with { TabPlacement = placement } : node);
    });
}
