using DockingScene.Core;

namespace DockingScene.Layout;

/// <summary>Single-entry cache for minimized caption placement. Activation and
/// metadata changes reuse the plan if ordered minimized ids and geometry match.
/// Callers must not mutate their id list during Arrange. No manager is retained.</summary>
public sealed class MinimizedWindowLayoutCache
{
    private readonly object _gate = new();
    private string[] _ids = [];
    private DockRect _area;
    private DockSize _size;
    private double _gap;
    private MinimizedWindowLayout? _layout;
    private long _builds, _reuses;
    public long BuildCount { get { lock (_gate) return _builds; } }
    public long ReuseCount { get { lock (_gate) return _reuses; } }
    public MinimizedWindowLayout Arrange(IReadOnlyList<string> ids, DockRect area, DockSize? captionSize = null, double gap = 2)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var size = captionSize ?? new DockSize(168, 26);
        lock (_gate)
        {
            bool same = _layout is not null && _area == area && _size == size && _gap == gap && _ids.Length == ids.Count;
            for (int i = 0; same && i < _ids.Length; i++) same = string.Equals(_ids[i], ids[i], StringComparison.Ordinal);
            if (same) { _reuses++; return _layout!; }
            var snapshot = ids.ToArray();
            var layout = MinimizedWindowLayout.Arrange(snapshot, area, size, gap);
            _ids = snapshot; _area = area; _size = size; _gap = gap; _layout = layout; _builds++;
            return layout;
        }
    }
    public void Clear() { lock (_gate) { _ids = []; _layout = null; } }
}
