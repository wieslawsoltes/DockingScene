using System.Collections.Immutable;

namespace DockingScene.Core;

/// <summary>
/// Serializes edits into validated immutable snapshots. No failed or cancelled command leaks a partial layout.
/// UI adapters marshal Changed notifications onto their dispatcher; this assembly has no UI dependency.
/// </summary>
public sealed class DockManager
{
    private readonly object _gate = new();
    private readonly List<DockLayout> _undo = [];
    private readonly List<DockLayout> _redo = [];
    private DockLayout _layout;
    private bool _changing;
    public DockPolicy Policy { get; }
    public DockLayout Layout { get { lock (_gate) return _layout; } }
    public bool CanUndo { get { lock (_gate) return _undo.Count > 0; } }
    public bool CanRedo { get { lock (_gate) return _redo.Count > 0; } }
    public event EventHandler<DockChangingEventArgs>? Changing;
    public event EventHandler<DockChangedEventArgs>? Changed;

    public DockManager(DockLayout? layout = null, DockPolicy? policy = null)
    {
        Policy = policy ?? new();
        if (Policy.HistoryLimit < 0 || Policy.MaximumWindows < 1) throw new ArgumentOutOfRangeException(nameof(policy));
        _layout = layout ?? new();
        DockLayoutValidator.Validate(_layout, Policy.MaximumWindows);
    }

    public bool Allows(string id, DockCapabilities capability) => Layout.Windows.TryGetValue(id, out var w) &&
        (w.Capabilities & Policy.AllowedCapabilities & capability) == capability;

    private void Require(DockLayout layout, IEnumerable<string> ids, DockCapabilities capability, bool structural = true)
    {
        if (structural && layout.IsLayoutLocked) throw new InvalidOperationException("The layout is locked.");
        foreach (var id in ids)
            if (!layout.Windows.TryGetValue(id, out var window) || (window.Capabilities & Policy.AllowedCapabilities & capability) != capability)
                throw new InvalidOperationException("Window does not allow " + capability + ": " + id);
    }

    private DockResult Apply(string command, Func<DockLayout, DockLayout> change, long? expectedRevision = null,
        ImmutableArray<string> closing = default, bool recordHistory = true)
    {
        DockChangedEventArgs? notification = null;
        DockResult result;
        lock (_gate)
        {
            if (_changing) return new(false, _layout.Revision, "Reentrant changes during Changing are not allowed.");
            if (expectedRevision is { } revision && revision != _layout.Revision) return new(false, _layout.Revision, "Stale layout revision.");
            var before = _layout;
            _changing = true;
            try
            {
                var after = DockTree.RepairActivation(change(before)) with { Revision = checked(before.Revision + 1) };
                DockLayoutValidator.Validate(after, Policy.MaximumWindows);
                var changing = new DockChangingEventArgs(command, before, after, closing.IsDefault ? [] : closing);
                Changing?.Invoke(this, changing);
                if (changing.Cancel) return new(false, before.Revision, changing.Reason ?? "Change cancelled.");
                if (recordHistory && Policy.HistoryLimit > 0)
                {
                    _undo.Add(before);
                    if (_undo.Count > Policy.HistoryLimit) _undo.RemoveAt(0);
                    _redo.Clear();
                }
                _layout = after;
                notification = new(command, before, after);
                result = new(true, after.Revision);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or OverflowException)
            { result = new(false, before.Revision, e.Message); }
            finally { _changing = false; }
        }
        if (notification is not null) Changed?.Invoke(this, notification);
        return result;
    }

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
        var w = Get(layout, id);
        if (!layout.HiddenWindows.Contains(id)) return DockTree.Activate(layout, id);
        var bookmark = w.Bookmark;
        string? target = bookmark.GroupId is not null && DockTree.Find(layout, bookmark.GroupId) is DockGroup ? bookmark.GroupId : null;
        return Place(layout, [id], target, target is null && w.Kind == WindowKind.Tool ? bookmark.Side : DockSide.Center,
            w.Kind == WindowKind.Document ? GroupKind.Documents : GroupKind.Tools, bookmark.Index);
    });

    public DockResult Activate(string id) => Apply("Activate", l => DockTree.Activate(l, id), recordHistory: false);

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
        ids = SortTabs(layout, ids);
        return DockTree.Map(layout, n => n.Id == group.Id ? group with { Windows = ids } : n);
    });

    public DockResult UpdateWindow(string id, Func<DockWindow, DockWindow> update) => Apply("UpdateWindow", layout =>
    {
        var before = Get(layout, id);
        var after = update(before);
        if (after.Id != id || after.Kind != before.Kind) throw new InvalidOperationException("Window identity and kind cannot change.");
        if (after.IsModified && after.DisplayMode == TabDisplayMode.Preview) after = after with { DisplayMode = TabDisplayMode.Normal };
        layout = layout with { Windows = layout.Windows.SetItem(id, after) };
        var group = DockTree.GroupOf(layout, id);
        if (group is not null)
        {
            if (after.DisplayMode == TabDisplayMode.Preview)
            {
                foreach (var other in group.Windows.Where(key => key != id && layout.Windows[key].DisplayMode == TabDisplayMode.Preview))
                    layout = layout with { Windows = layout.Windows.SetItem(other, layout.Windows[other] with { DisplayMode = TabDisplayMode.Normal }) };
            }
            var sorted = SortTabs(layout, group.Windows);
            layout = DockTree.Map(layout, n => n.Id == group.Id ? group with { Windows = sorted } : n);
        }
        return layout;
    });

    public DockResult ResizeSplit(string id, double ratio, long? expectedRevision = null) => Apply("ResizeSplit", layout =>
    {
        if (layout.IsLayoutLocked || !double.IsFinite(ratio)) throw new InvalidOperationException("Cannot resize split.");
        var split = DockTree.Find(layout, id) as DockSplit ?? throw new InvalidOperationException("Unknown splitter.");
        Require(layout, DockTree.WindowIds(split), DockCapabilities.Resize);
        return DockTree.Map(layout, n => n.Id == id ? split with { Ratio = Math.Clamp(ratio, 0.01, 0.99) } : n);
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
        return layout with { FloatingHosts = layout.FloatingHosts.SetItem(index, host with { IsMaximized = maximized }) };
    });

    public DockResult SetMdiMode(MdiMode mode) => Apply("SetMdiMode", l => l with { MdiMode = mode });
    public DockResult SetMdiBounds(string id, DockRect bounds) => UpdateWindow(id, w => w with { MdiBounds = bounds, MdiState = MdiWindowState.Normal });
    public DockResult SetMdiState(string id, MdiWindowState state) => UpdateWindow(id, w => w with { MdiState = state });
    public DockResult SetTabPlacement(string groupId, TabPlacement placement) => Apply("SetTabPlacement", layout =>
    {
        var group = DockTree.Find(layout, groupId) as DockGroup ?? throw new InvalidOperationException("Unknown group.");
        return DockTree.Map(layout, n => n.Id == groupId ? group with { TabPlacement = placement } : n);
    });
    public DockResult SetLayoutLocked(bool locked) => Apply("SetLayoutLocked", l => l with { IsLayoutLocked = locked });
    public DockResult Restore(DockLayout layout, long? expectedRevision = null) => Apply("Restore", _ => layout, expectedRevision);

    public DockResult Undo() => NavigateHistory(_undo, _redo, "Undo");
    public DockResult Redo() => NavigateHistory(_redo, _undo, "Redo");
    private DockResult NavigateHistory(List<DockLayout> source, List<DockLayout> destination, string command)
    {
        // The outer lock prevents another writer from changing the journal between preparation and commit.
        lock (_gate)
        {
            if (_changing || source.Count == 0) return new(false, _layout.Revision, "No history available.");
            var before = _layout;
            var target = source[^1];
            var result = Apply(command, _ => target, recordHistory: false);
            if (result.Success) { source.RemoveAt(source.Count - 1); destination.Add(before); }
            return result;
        }
    }

    private static DockWindow Get(DockLayout layout, string id) => layout.Windows.TryGetValue(id, out var w) ? w : throw new InvalidOperationException("Unknown window: " + id);
    private static int IndexOfHost(DockLayout layout, string id)
    {
        for (int i = 0; i < layout.FloatingHosts.Length; i++) if (layout.FloatingHosts[i].Id == id) return i;
        throw new InvalidOperationException("Unknown floating host.");
    }
    private static DockNode? ResolveTarget(DockLayout layout, string? id) => id is null ? null : DockTree.Find(layout, id) ?? DockTree.GroupOf(layout, id) ?? throw new InvalidOperationException("Unknown docking target: " + id);
    private static ImmutableArray<string> SortTabs(DockLayout layout, IEnumerable<string> ids) => ids.OrderBy(id => layout.Windows[id].DisplayMode == TabDisplayMode.Pinned ? 0 : layout.Windows[id].DisplayMode == TabDisplayMode.Preview ? 2 : 1).ToImmutableArray();
    private static DockLayout PromoteExtraPreviews(DockLayout layout, IEnumerable<string> ids)
    {
        bool seen = false;
        foreach (var id in ids)
            if (layout.Windows[id].DisplayMode == TabDisplayMode.Preview)
            {
                if (seen) layout = layout with { Windows = layout.Windows.SetItem(id, layout.Windows[id] with { DisplayMode = TabDisplayMode.Normal }) };
                seen = true;
            }
        return layout;
    }

    private static DockLayout Place(DockLayout layout, ImmutableArray<string> ids, string? targetId, DockSide side, GroupKind kind, int index = -1)
    {
        if (!Enum.IsDefined(side)) throw new ArgumentException("Unknown dock side.");
        foreach (var id in ids) Get(layout, id);
        if (kind == GroupKind.Tools && ids.Any(id => layout.Windows[id].Kind == WindowKind.Document)) throw new InvalidOperationException("Documents cannot dock into tool containers.");
        var target = ResolveTarget(layout, targetId);
        if (target is DockGroup same && ids.All(same.Windows.Contains) && side == DockSide.Center)
        {
            var order = same.Windows.Where(id => !ids.Contains(id)).ToImmutableArray();
            order = order.InsertRange(index < 0 ? order.Length : Math.Clamp(index, 0, order.Length), ids);
            layout = DockTree.Map(layout, n => n.Id == same.Id ? same with { Windows = SortTabs(layout, order), SelectedId = ids[0] } : n);
            return DockTree.Activate(layout, ids[0]);
        }
        if (target is not null && DockTree.WindowIds(target).Any() && DockTree.WindowIds(target).All(ids.Contains))
            throw new InvalidOperationException("A container cannot dock relative to itself.");
        layout = DockTree.RemoveReferences(layout, ids.ToHashSet(StringComparer.Ordinal));
        if (target is not null) target = DockTree.Find(layout, target.Id) ?? throw new InvalidOperationException("Dock target was removed.");
        if (target is null && kind == GroupKind.Documents)
        {
            var workspace = DockTree.Descendants(layout.Root).OfType<DockWorkspace>().FirstOrDefault();
            if (workspace is not null)
                target = side == DockSide.Center ? DockTree.Descendants(workspace.Content).OfType<DockGroup>().FirstOrDefault() ?? (DockNode)workspace : workspace.Content ?? workspace;
            else if (layout.Root is not null) throw new InvalidOperationException("This site has no document workspace.");
        }
        if (target is null && kind == GroupKind.Tools && side == DockSide.Center)
        {
            target = DockTree.Descendants(layout.Root).OfType<DockGroup>().FirstOrDefault(g => g.Kind == GroupKind.Tools);
            if (target is null && layout.Root is not null) side = DockSide.Left;
        }
        if (target is DockWorkspace ws)
        {
            if (kind == GroupKind.Tools) { target = ws; if (side == DockSide.Center) side = DockSide.Left; }
            else if (ws.Content is not null) target = DockTree.Descendants(ws.Content).OfType<DockGroup>().FirstOrDefault() ?? ws.Content;
        }
        if (target is DockGroup group && side == DockSide.Center)
        {
            if (group.Kind == GroupKind.Tools && kind == GroupKind.Documents) throw new InvalidOperationException("Cannot attach a document to tools.");
            var merged = group.Windows.InsertRange(index < 0 ? group.Windows.Length : Math.Clamp(index, 0, group.Windows.Length), ids);
            layout = PromoteExtraPreviews(layout, merged);
            var replacement = group with { Windows = SortTabs(layout, merged), SelectedId = ids[0] };
            layout = DockTree.Map(layout, n => n.Id == group.Id ? replacement : n);
        }
        else
        {
            layout = PromoteExtraPreviews(layout, ids);
            DockNode added = new DockGroup(DockTree.NewId("group"), kind, SortTabs(layout, ids), ids[0]) { TabPlacement = kind == GroupKind.Tools ? TabPlacement.Bottom : TabPlacement.Top };
            if (target is DockWorkspace { Content: null } empty && kind == GroupKind.Documents)
                layout = DockTree.Map(layout, n => n.Id == empty.Id ? empty with { Content = added } : n);
            else
            {
                target ??= layout.Root;
                if (target is null) layout = layout with { Root = kind == GroupKind.Documents ? new DockWorkspace("workspace", added) : added };
                else
                {
                    if (side == DockSide.Center) side = DockSide.Right;
                    bool before = side is DockSide.Left or DockSide.Top;
                    var split = new DockSplit(DockTree.NewId("split"), side is DockSide.Left or DockSide.Right ? SplitAxis.Horizontal : SplitAxis.Vertical,
                        before ? .24 : .76, before ? added : target, before ? target : added);
                    string replaceId = target.Id;
                    layout = DockTree.Map(layout, n => n.Id == replaceId ? split : n);
                }
            }
        }
        return DockTree.Activate(layout, ids[0]);
    }
}
