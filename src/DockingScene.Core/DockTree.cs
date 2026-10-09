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

    public static IEnumerable<DockNode> Nodes(DockLayout layout) =>
        Descendants(layout.Root).Concat(layout.FloatingHosts.SelectMany(x => Descendants(x.Root)));
    public static IEnumerable<DockGroup> Groups(DockLayout layout) => Nodes(layout).OfType<DockGroup>();
    public static IEnumerable<string> WindowIds(DockNode? node) => Descendants(node).OfType<DockGroup>().SelectMany(g => g.Windows);
    public static DockNode? Find(DockLayout layout, string id) => Nodes(layout).FirstOrDefault(n => n.Id == id);
    public static DockGroup? GroupOf(DockLayout layout, string id) => Groups(layout).FirstOrDefault(g => g.Windows.Contains(id));
    public static string NewId(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");

    public static WindowLocation Locate(DockLayout layout, string id)
    {
        foreach (var group in Descendants(layout.Root).OfType<DockGroup>())
        {
            int index = group.Windows.IndexOf(id);
            if (index >= 0) return new(group.Kind == GroupKind.Documents ? WindowPlacement.Document : WindowPlacement.Docked, group.Id, null, DockSide.Center, index);
        }
        foreach (var host in layout.FloatingHosts)
            foreach (var group in Descendants(host.Root).OfType<DockGroup>())
            {
                int index = group.Windows.IndexOf(id);
                if (index >= 0) return new(WindowPlacement.Floating, group.Id, host.Id, DockSide.Center, index);
            }
        foreach (var group in layout.AutoHideGroups)
        {
            int index = group.Windows.IndexOf(id);
            if (index >= 0) return new(WindowPlacement.AutoHidden, group.Id, null, group.Side, index);
        }
        return new(WindowPlacement.Hidden, null, null, DockSide.Center, -1);
    }

    public static DockNode? Map(DockNode? node, Func<DockNode, DockNode?> transform)
    {
        if (node is null) return null;
        DockNode? mapped = node;
        if (node is DockSplit split)
        {
            var first = Map(split.First, transform);
            var second = Map(split.Second, transform);
            mapped = first is null ? second : second is null ? first : split with { First = first, Second = second };
        }
        else if (node is DockWorkspace workspace)
            mapped = workspace with { Content = Map(workspace.Content, transform) };
        return mapped is null ? null : transform(mapped);
    }

    public static DockLayout Map(DockLayout layout, Func<DockNode, DockNode?> transform)
    {
        var hosts = ImmutableArray.CreateBuilder<FloatingHost>();
        foreach (var host in layout.FloatingHosts)
        {
            var root = Map(host.Root, transform);
            if (root is not null) hosts.Add(host with { Root = root });
        }
        return layout with { Root = Map(layout.Root, transform), FloatingHosts = hosts.ToImmutable() };
    }

    public static DockLayout RemoveReferences(DockLayout layout, IReadOnlySet<string> ids, bool remember = true)
    {
        if (remember)
        {
            var windows = layout.Windows;
            foreach (var id in ids)
            {
                var location = Locate(layout, id);
                if (location.Placement is WindowPlacement.Docked or WindowPlacement.Document)
                    windows = windows.SetItem(id, windows[id] with { Bookmark = new(location.GroupId, DockSide.Left, location.Index) });
            }
            layout = layout with { Windows = windows };
        }
        layout = Map(layout, node =>
        {
            if (node is not DockGroup group) return node;
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
        if (active is not null) history = history.Remove(active).Insert(0, active);
        return layout with { ActiveWindowId = active, ActivationHistory = history };
    }

    public static DockLayout Activate(DockLayout layout, string id)
    {
        if (!layout.Windows.ContainsKey(id) || Locate(layout, id).Placement == WindowPlacement.Hidden)
            throw new InvalidOperationException("Only an open window can be activated.");
        layout = Map(layout, node => node is DockGroup g && g.Windows.Contains(id) ? g with { SelectedId = id } : node);
        return layout with
        {
            ActiveWindowId = id,
            ActivationHistory = layout.ActivationHistory.Remove(id).Insert(0, id),
            AutoHideGroups = layout.AutoHideGroups.Select(g => g.Windows.Contains(id) ? g with { SelectedId = id } : g).ToImmutableArray()
        };
    }
}
