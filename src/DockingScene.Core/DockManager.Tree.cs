using System.Collections.Immutable;

namespace DockingScene.Core;

public sealed partial class DockManager
{
    private static DockWindow Get(DockLayout layout, string id) => layout.Windows.TryGetValue(id, out var window) ? window : throw new InvalidOperationException("Unknown window: " + id);
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
            layout = DockTree.Map(layout, node => node.Id == same.Id ? same with { Windows = SortTabs(layout, order), SelectedId = ids[0] } : node);
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
            target = DockTree.Descendants(layout.Root).OfType<DockGroup>().FirstOrDefault(group => group.Kind == GroupKind.Tools);
            if (target is null && layout.Root is not null) side = DockSide.Left;
        }
        if (target is DockWorkspace workspaceTarget)
        {
            if (kind == GroupKind.Tools) { target = workspaceTarget; if (side == DockSide.Center) side = DockSide.Left; }
            else if (workspaceTarget.Content is not null) target = DockTree.Descendants(workspaceTarget.Content).OfType<DockGroup>().FirstOrDefault() ?? workspaceTarget.Content;
        }
        if (target is DockGroup group && side == DockSide.Center)
        {
            if (group.Kind == GroupKind.Tools && kind == GroupKind.Documents) throw new InvalidOperationException("Cannot attach a document to tools.");
            var merged = group.Windows.InsertRange(index < 0 ? group.Windows.Length : Math.Clamp(index, 0, group.Windows.Length), ids);
            layout = PromoteExtraPreviews(layout, merged);
            var replacement = group with { Windows = SortTabs(layout, merged), SelectedId = ids[0] };
            layout = DockTree.Map(layout, node => node.Id == group.Id ? replacement : node);
        }
        else
        {
            layout = PromoteExtraPreviews(layout, ids);
            DockNode added = new DockGroup(DockTree.NewId("group"), kind, SortTabs(layout, ids), ids[0]) { TabPlacement = kind == GroupKind.Tools ? TabPlacement.Bottom : TabPlacement.Top };
            if (target is DockWorkspace { Content: null } empty && kind == GroupKind.Documents)
                layout = DockTree.Map(layout, node => node.Id == empty.Id ? empty with { Content = added } : node);
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
                    layout = DockTree.Map(layout, node => node.Id == replaceId ? split : node);
                }
            }
        }
        return DockTree.Activate(layout, ids[0]);
    }
}
