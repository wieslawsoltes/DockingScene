using DockingScene.Core;

namespace DockingScene.Layout;

/// <summary>Immutable, renderer-independent snapping plan for one window gesture.
/// Copies and validates neighbors once. Move/Resize queries allocate no objects.
/// Query raw pointer-derived bounds, not the preceding snapped result, to allow
/// immediate escape and reversal without accumulating snapping drift.</summary>
public sealed class WindowMagnetism
{
    public const int MaximumNeighbors = 10000;
    private readonly DockRect[] _neighbors;
    public double SnapDistance { get; }
    public double GapDistance { get; }
    public int NeighborCount => _neighbors.Length;

    public WindowMagnetism(IReadOnlyList<DockRect> neighbors, double snapDistance = 5, double gapDistance = 1)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        ValidateDistance(snapDistance); ValidateDistance(gapDistance);
        int count = neighbors.Count;
        if (count < 0 || count > MaximumNeighbors) throw new ArgumentOutOfRangeException(nameof(neighbors));
        _neighbors = new DockRect[count];
        for (int i = 0; i < count; i++) { var rect = neighbors[i]; ValidateRect(rect); _neighbors[i] = rect; }
        SnapDistance = snapDistance; GapDistance = gapDistance;
    }
    public static void ValidateDistance(double value)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Magnetism distances must be finite and nonnegative.");
    }
    private static void ValidateRect(DockRect rect)
    {
        if (!rect.IsValid || !double.IsFinite(rect.Right) || !double.IsFinite(rect.Bottom))
            throw new ArgumentException("Magnetism requires positive finite rectangles and edges.");
    }
    private static bool NearInterval(double start, double end, double otherStart, double otherEnd, double distance) =>
        start <= otherEnd && otherStart <= end ||
        start > otherEnd && start - otherEnd <= distance ||
        otherStart > end && otherStart - end <= distance;

    public DockRect Move(DockRect raw)
    {
        ValidateRect(raw);
        return SnapDistance == 0 ? raw : Query(raw, 0, 0, default, default);
    }
    /// <summary>Snap only the edited edges. Center leaves that axis unchanged,
    /// including when the untouched size lies outside the supplied constraints.</summary>
    public DockRect Resize(DockRect raw, DockSide horizontal, DockSide vertical, DockSize minimum, DockSize maximum)
    {
        ValidateRect(raw);
        if (horizontal is not (DockSide.Center or DockSide.Left or DockSide.Right) ||
            vertical is not (DockSide.Center or DockSide.Top or DockSide.Bottom)) throw new ArgumentException("Invalid resize edges.");
        if (!minimum.IsFinite || !maximum.IsFinite || minimum.Width < 0 || minimum.Height < 0 ||
            maximum.Width <= 0 || maximum.Height <= 0 || maximum.Width < minimum.Width || maximum.Height < minimum.Height)
            throw new ArgumentException("Invalid resize constraints.");
        return SnapDistance == 0 ? raw : Query(raw,
            horizontal == DockSide.Left ? -1 : horizontal == DockSide.Right ? 1 : 2,
            vertical == DockSide.Top ? -1 : vertical == DockSide.Bottom ? 1 : 2, minimum, maximum);
    }
    private DockRect Query(DockRect raw, int horizontal, int vertical, DockSize minimum, DockSize maximum)
    {
        var x = new Axis(raw.X, raw.Width, horizontal, SnapDistance, minimum.Width, maximum.Width);
        var y = new Axis(raw.Y, raw.Height, vertical, SnapDistance, minimum.Height, maximum.Height);
        double nearby = Math.Max(SnapDistance, GapDistance);
        foreach (var other in _neighbors)
        {
            if (NearInterval(raw.Y, raw.Bottom, other.Y, other.Bottom, nearby))
                x.Consider(other.X, other.Right, GapDistance);
            if (NearInterval(raw.X, raw.Right, other.X, other.Right, nearby))
                y.Consider(other.Y, other.Bottom, GapDistance);
        }
        return new(x.Origin, y.Origin, x.Extent, y.Extent);
    }
    private struct Axis(double start, double extent, int mode, double threshold, double minimum, double maximum)
    {
        private readonly double _start = start, _extent = extent, _end = start + extent;
        private readonly int _mode = mode;
        private readonly double _threshold = threshold, _minimum = minimum, _maximum = maximum;
        private bool _found;
        private double _best, _distance;
        public double Origin => _found && _mode is 0 or -1 ? _best : _start;
        public double Extent => !_found || _mode == 0 ? _extent : _mode == -1 ? _end - _best : _best - _start;
        public void Consider(double leading, double trailing, double gap)
        {
            switch (_mode)
            {
                case 0:
                    Candidate(leading); Candidate(trailing + gap);
                    Candidate(trailing - _extent); Candidate(leading - gap - _extent);
                    break;
                case -1: Candidate(leading); Candidate(trailing + gap); break;
                case 1: Candidate(trailing); Candidate(leading - gap); break;
            }
        }
        private void Candidate(double target)
        {
            if (!double.IsFinite(target)) return;
            double delta = Math.Abs(target - (_mode == 1 ? _end : _start));
            if (!double.IsFinite(delta) || delta > _threshold) return;
            if (_mode == 0)
            { if (!double.IsFinite(target + _extent)) return; }
            else
            {
                double size = _mode == -1 ? _end - target : target - _start;
                if (!double.IsFinite(size) || size <= 0 || size < _minimum || size > _maximum) return;
            }
            // Deterministic tie-breaking independent of neighbor enumeration.
            if (!_found || delta < _distance || delta == _distance && target < _best)
            { _found = true; _best = target; _distance = delta; }
        }
    }
}
