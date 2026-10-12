using DockingScene.Core;

namespace DockingScene.Layout;

/// <summary>Single-entry minimized-caption cache. Copies IDs only on a miss; callers
/// may reuse and edit their list between calls, but must not edit it during a call.</summary>
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
            if (_layout is not null && area == _area && size == _size && gap == _gap && SameIds(ids))
            { _reuses++; return _layout; }
            var copy = ids.ToArray();
            var layout = MinimizedWindowLayout.Arrange(copy, area, size, gap);
            _ids = copy; _area = area; _size = size; _gap = gap; _layout = layout; _builds++;
            return layout;
        }
    }
    public void Clear() { lock (_gate) { _ids = []; _layout = null; } }
    private bool SameIds(IReadOnlyList<string> ids)
    {
        if (ids.Count != _ids.Length) return false;
        for (int i = 0; i < ids.Count; i++) if (!StringComparer.Ordinal.Equals(ids[i], _ids[i])) return false;
        return true;
    }
}
