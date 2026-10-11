using System.Collections.Immutable;
using DockingScene.Core;

namespace DockingScene.Layout;

public sealed record DockGuide(string? TargetId, DockSide Side, DockRect Bounds, DockRect Preview, bool IsOuter);
public sealed record DockDragPreview(ImmutableArray<DockGuide> Guides, DockGuide? Target, DockRect FloatingBounds);

public static class DockGuides
{
    public static DockDragPreview HitTest(DockLayout layout, DockGeometry geometry, IReadOnlyCollection<string> draggedIds,
        double x, double y, DockRect floatingBounds, bool suppressDocking = false)
    {
        var guides = ImmutableArray.CreateBuilder<DockGuide>();
        if (suppressDocking || layout.IsLayoutLocked || draggedIds.Count == 0) return new([], null, floatingBounds);
        var ids = draggedIds.ToHashSet(StringComparer.Ordinal);
        if (ids.Any(id => !layout.Windows.TryGetValue(id, out var w) || !w.Capabilities.HasFlag(DockCapabilities.Dock))) return new([], null, floatingBounds);
        var groups = DockTree.Groups(layout).Where(g => !g.Windows.All(ids.Contains) && geometry.Nodes.ContainsKey(g.Id)).ToArray();
        // Floating groups have priority over groups underneath their host.
        var target = groups.Reverse().FirstOrDefault(g => geometry.Nodes[g.Id].Contains(x, y));
        DockNode? targetNode = target;
        if (targetNode is null)
            targetNode = DockTree.Descendants(layout.Root).OfType<DockWorkspace>().FirstOrDefault(w => geometry.Nodes[w.Id].Contains(x, y));
        bool documents = ids.Any(id => layout.Windows[id].Kind == WindowKind.Document);
        bool allowedTarget = targetNode is DockWorkspace || targetNode is DockGroup g && (g.Kind == GroupKind.Documents || !documents);
        if (targetNode is not null && allowedTarget)
        {
            var rect = geometry.Nodes[targetNode.Id];
            double cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
            foreach (var side in Enum.GetValues<DockSide>())
            {
                if (side == DockSide.Center && ids.Any(id => !layout.Windows[id].Capabilities.HasFlag(DockCapabilities.Attach))) continue;
                double dx = side == DockSide.Left ? -33 : side == DockSide.Right ? 33 : 0;
                double dy = side == DockSide.Top ? -33 : side == DockSide.Bottom ? 33 : 0;
                guides.Add(new(targetNode.Id, side, new(cx - 14 + dx, cy - 14 + dy, 28, 28), Preview(rect, side), false));
            }
        }
        if (!documents)
        {
            var r = geometry.WorkspaceBounds;
            guides.Add(new(null, DockSide.Left, new(r.X + 8, r.Y + r.Height / 2 - 14, 28, 28), Preview(r, DockSide.Left), true));
            guides.Add(new(null, DockSide.Right, new(r.Right - 36, r.Y + r.Height / 2 - 14, 28, 28), Preview(r, DockSide.Right), true));
            guides.Add(new(null, DockSide.Top, new(r.X + r.Width / 2 - 14, r.Y + 8, 28, 28), Preview(r, DockSide.Top), true));
            guides.Add(new(null, DockSide.Bottom, new(r.X + r.Width / 2 - 14, r.Bottom - 36, 28, 28), Preview(r, DockSide.Bottom), true));
        }
        var hit = guides.FirstOrDefault(g => g.Bounds.Contains(x, y));
        // Edge zones provide a large touch target, in addition to the explicit guides.
        if (hit is null && targetNode is not null && allowedTarget)
        {
            var r = geometry.Nodes[targetNode.Id];
            double edge = Math.Min(40, Math.Min(r.Width, r.Height) * .18);
            DockSide? side = x < r.X + edge ? DockSide.Left : x > r.Right - edge ? DockSide.Right : y < r.Y + edge ? DockSide.Top : y > r.Bottom - edge ? DockSide.Bottom : null;
            if (side is not null) hit = guides.FirstOrDefault(g => !g.IsOuter && g.Side == side);
        }
        return new(guides.ToImmutable(), hit, floatingBounds);
    }

    public static DockRect Preview(DockRect rect, DockSide side) => side switch
    {
        DockSide.Left => rect with { Width = rect.Width * .24 },
        DockSide.Right => new(rect.X + rect.Width * .76, rect.Y, rect.Width * .24, rect.Height),
        DockSide.Top => rect with { Height = rect.Height * .24 },
        DockSide.Bottom => new(rect.X, rect.Y + rect.Height * .76, rect.Width, rect.Height * .24),
        _ => rect
    };
}
