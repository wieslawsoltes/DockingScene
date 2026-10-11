using DockingScene.Core;

namespace DockingScene.Layout;

/// <summary>Single-entry cache for validated immutable layouts. Activation, titles and
/// other non-geometric metadata reuse the computed rectangles. Changed constraints,
/// structure, viewport or metrics always recompute. Retains only the latest snapshot.</summary>
public sealed class DockGeometryCache
{
    private readonly object _gate = new();
    private DockLayout? _source;
    private DockMetrics? _metrics;
    private DockRect _bounds;
    private DockGeometry? _geometry;
    private long _builds, _reuses;
    public long BuildCount { get { lock (_gate) return _builds; } }
    public long ReuseCount { get { lock (_gate) return _reuses; } }

    public DockGeometry Arrange(DockLayout layout, DockRect bounds, DockMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        metrics ??= new();
        lock (_gate)
        {
            if (_geometry is not null && _bounds == bounds && _metrics == metrics && GeometryEqual(_source!, layout))
            {
                _source = layout; _reuses++; return _geometry;
            }
            var geometry = DockLayoutEngine.Arrange(layout, bounds, metrics);
            _source = layout; _bounds = bounds; _metrics = metrics; _geometry = geometry; _builds++;
            return geometry;
        }
    }

    /// <summary>Release cached inputs and output without resetting lifetime counters.</summary>
    public void Clear() { lock (_gate) { _source = null; _geometry = null; _metrics = null; } }

    private static bool GeometryEqual(DockLayout a, DockLayout b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (!SameNode(a.Root, b.Root) || a.FloatingHosts.Length != b.FloatingHosts.Length) return false;
        for (int i = 0; i < a.FloatingHosts.Length; i++)
        {
            var x = a.FloatingHosts[i]; var y = b.FloatingHosts[i];
            if (x.Id != y.Id || x.Bounds != y.Bounds || x.IsMaximized != y.IsMaximized || !SameNode(x.Root, y.Root)) return false;
        }
        // Layout uses side presence, not auto-hide selection, title, group identity,
        // or popup size. Popup geometry is owned by the UI adapter, not this result.
        int Rails(DockLayout layout)
        {
            int mask = 0;
            foreach (var group in layout.AutoHideGroups) mask |= 1 << (int)group.Side;
            return mask;
        }
        if (Rails(a) != Rails(b)) return false;
        if (ReferenceEquals(a.Windows, b.Windows)) return true;
        if (a.Windows.Count != b.Windows.Count) return false;
        foreach (var (id, x) in a.Windows)
            if (!b.Windows.TryGetValue(id, out var y) || x.MinimumSize != y.MinimumSize || x.MaximumSize != y.MaximumSize || x.HasTitleBar != y.HasTitleBar) return false;
        return true;
    }

    private static bool SameNode(DockNode? a, DockNode? b, int depth = 0)
    {
        if (ReferenceEquals(a, b)) return true;
        if (depth > 64) throw new InvalidOperationException("Dock tree is too deep.");
        if (a is null || b is null || a.Id != b.Id) return false;
        return (a, b) switch
        {
            (DockGroup x, DockGroup y) => x.Kind == y.Kind && x.Windows.SequenceEqual(y.Windows),
            (DockWorkspace x, DockWorkspace y) => SameNode(x.Content, y.Content, depth + 1),
            (DockSplit x, DockSplit y) => x.Axis == y.Axis && x.Ratio == y.Ratio &&
                SameNode(x.First, y.First, depth + 1) && SameNode(x.Second, y.Second, depth + 1),
            _ => false
        };
    }
}
