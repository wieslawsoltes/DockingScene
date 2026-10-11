using System.Collections.Immutable;

namespace DockingScene.Core;

/// <summary>Pure tree operations shared by commands, renderers and serializers.</summary>
public static class DockTree
{
    public static IEnumerable<DockNode> Descendants(DockNode? root)
    {
        if (root is null) yield break;
        var stack = new Stack<DockNode>();
        stack.Push(root);
        while (stack.TryPop(out var node))
        {
            yield return node;
            if (node is DockSplit split) { stack.Push(split.Second); stack.Push(split.First); }
            else if (node is DockWorkspace { Content: { } content }) stack.Push(content);
        }
    }

    public static IEnumerable<DockNode> Nodes(DockLayout layout) => DockLayoutIndex.For(layout).Nodes;
    public static IEnumerable<DockGroup> Groups(DockLayout layout) => DockLayoutIndex.For(layout).Groups;
    public static IEnumerable<string> WindowIds(DockNode? node) => Descendants(node).OfType<DockGroup>().SelectMany(g => g.Windows);
    public static DockNode? Find(DockLayout layout, string id) => DockLayoutIndex.For(layout).Find(id);
    public static DockGroup? GroupOf(DockLayout layout, string id) => DockLayoutIndex.For(layout).GroupOf(id);
    public static string NewId(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");
    public static WindowLocation Locate(DockLayout layout, string id) => DockLayoutIndex.For(layout).Locate(id);

    public static DockNode? Map(DockNode? node, Func<DockNode, DockNode?> transform)
    {
        if (node is null) return null;
        DockNode? mapped = node;
        if (node is DockSplit split)
        {
            var first = Map(split.First, transform);
            var second = Map(split.Second, transform);
            mapped = first is null ? second : second is null ? first : ReferenceEquals(first, split.First) && ReferenceEquals(second, split.Second) ? split : split with { First = first, Second = second };
        }
        else if (node is DockWorkspace workspace)
        {
            var content = Map(workspace.Content, transform);
            mapped = ReferenceEquals(content, workspace.Content) ? workspace : workspace with { Content = content };
        }
        return mapped is null ? null : transform(mapped);
    }

    public static DockLayout Map(DockLayout layout, Func<DockNode, DockNode?> transform)
    {
        var hosts = ImmutableArray.CreateBuilder<FloatingHost>();
        bool hostsChanged = false;
        foreach (var host in layout.FloatingHosts)
        {
            var root = Map(host.Root, transform);
            hostsChanged |= !ReferenceEquals(root, host.Root);
            if (root is not null) hosts.Add(ReferenceEquals(root, host.Root) ? host : host with { Root = root });
        }
        var primary = Map(layout.Root, transform);
        if (!hostsChanged && ReferenceEquals(primary, layout.Root)) return layout;
        return layout with { Root = primary, FloatingHosts = hostsChanged ? hosts.ToImmutable() : layout.FloatingHosts };
    }

    public static DockLayout RemoveReferences(DockLayout layout, IReadOnlySet<string> ids, bool remember = true)
    {
        if (ids.Count == 0) return layout;
        if (remember)
        {
            var windows = layout.Windows;
            var index = DockLayoutIndex.For(layout);
            foreach (var id in ids)
            {
                var location = index.Locate(id);
                if (location.Placement is WindowPlacement.Docked or WindowPlacement.Document)
                    windows = windows.SetItem(id, windows[id] with { Bookmark = new(location.GroupId, DockSide.Left, location.Index) });
            }
            layout = layout with { Windows = windows };
        }
        layout = Map(layout, node =>
        {
            if (node is not DockGroup group || !group.Windows.Any(ids.Contains)) return node;
            var remaining = group.Windows.Where(id => !ids.Contains(id)).ToImmutableArray();
            return remaining.IsEmpty ? null : group with
            {
                Windows = remaining,
                SelectedId = group.SelectedId is not null && remaining.Contains(group.SelectedId) ? group.SelectedId : remaining[0]
            };
        });
        return layout with
        {
            AutoHideGroups = layout.AutoHideGroups.Select(g => g with
            {
                Windows = g.Windows.Where(id => !ids.Contains(id)).ToImmutableArray(),
                SelectedId = g.SelectedId is not null && !ids.Contains(g.SelectedId) ? g.SelectedId : g.Windows.FirstOrDefault(id => !ids.Contains(id))
            }).Where(g => !g.Windows.IsEmpty).ToImmutableArray(),
            HiddenWindows = layout.HiddenWindows.Where(id => !ids.Contains(id)).ToImmutableArray()
        };
    }

    public static DockLayout RepairActivation(DockLayout layout)
    {
        var visible = Groups(layout).SelectMany(g => g.Windows).Concat(layout.AutoHideGroups.SelectMany(g => g.Windows)).ToHashSet(StringComparer.Ordinal);
        var history = layout.ActivationHistory.Where(visible.Contains).Distinct(StringComparer.Ordinal).ToImmutableArray();
        var active = layout.ActiveWindowId is not null && visible.Contains(layout.ActiveWindowId) ? layout.ActiveWindowId : history.FirstOrDefault() ?? visible.Order(StringComparer.Ordinal).FirstOrDefault();
        if (active is not null && (history.IsEmpty || history[0] != active)) history = history.Remove(active).Insert(0, active);
        var result = active == layout.ActiveWindowId && history.SequenceEqual(layout.ActivationHistory) ? layout :
            layout with { ActiveWindowId = active, ActivationHistory = history };
        return active is null ? result : Activate(result, active);
    }

    public static DockLayout Activate(DockLayout layout, string id)
    {
        var index = DockLayoutIndex.For(layout);
        if (!layout.Windows.ContainsKey(id) || index.Locate(id).Placement == WindowPlacement.Hidden)
            throw new InvalidOperationException("Only an open window can be activated.");
        var group = index.GroupOf(id);
        if (group is not null && group.SelectedId != id)
            layout = Map(layout, node => ReferenceEquals(node, group) ? group with { SelectedId = id } : node);
        var auto = layout.AutoHideGroups;
        for (int i = 0; i < auto.Length; i++)
            if (auto[i].Windows.Contains(id) && auto[i].SelectedId != id)
            { auto = auto.SetItem(i, auto[i] with { SelectedId = id }); break; }
        var history = layout.ActivationHistory;
        if (history.IsEmpty || history[0] != id) history = history.Remove(id).Insert(0, id);
        if (layout.ActiveWindowId == id && history == layout.ActivationHistory && auto == layout.AutoHideGroups) return layout;
        return layout with { ActiveWindowId = id, ActivationHistory = history, AutoHideGroups = auto };
    }
}
