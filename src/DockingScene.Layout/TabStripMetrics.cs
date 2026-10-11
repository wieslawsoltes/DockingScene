using System.Collections.Immutable;

namespace DockingScene.Layout;

public readonly record struct TabRange(int First, int Count);

/// <summary>Renderer-independent measured tab geometry. Coordinates are in the strip's scrolling axis.</summary>
public sealed class TabStripMetrics
{
    public ImmutableArray<double> Offsets { get; }
    public int Count => Offsets.Length - 1;
    public double Length => Offsets[^1];
    public TabStripMetrics(IEnumerable<double> lengths)
    {
        ArgumentNullException.ThrowIfNull(lengths);
        var offsets = ImmutableArray.CreateBuilder<double>(); offsets.Add(0);
        foreach (double length in lengths)
        {
            double next = offsets[^1] + length;
            if (!double.IsFinite(length) || length <= 0 || !double.IsFinite(next)) throw new ArgumentOutOfRangeException(nameof(lengths));
            offsets.Add(next);
        }
        Offsets = offsets.ToImmutable();
    }
    public TabRange VisibleRange(double offset, double viewport, double overscan = 0)
    {
        Validate(offset, viewport); if (!double.IsFinite(overscan) || overscan < 0) throw new ArgumentOutOfRangeException(nameof(overscan));
        if (viewport == 0 && overscan == 0) return new(Math.Clamp(UpperBound(offset) - 1, 0, Count), 0);
        double start = Math.Max(0, offset - overscan), end = offset + viewport + overscan;
        int first = Math.Clamp(UpperBound(start) - 1, 0, Count), last = Math.Clamp(LowerBound(end), first, Count);
        return new(first, Math.Max(0, last - first));
    }
    public int InsertionIndex(double position)
    {
        if (!double.IsFinite(position)) throw new ArgumentOutOfRangeException(nameof(position));
        int low = 0, high = Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (position < Offsets[middle] + (Offsets[middle + 1] - Offsets[middle]) / 2) high = middle;
            else low = middle + 1;
        }
        return low;
    }
    public double BringIntoView(int index, double offset, double viewport)
    {
        Validate(offset, viewport);
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        double next = Offsets[index] < offset ? Offsets[index] : Offsets[index + 1] > offset + viewport ? Offsets[index + 1] - viewport : offset;
        // A tab wider than the viewport should expose its leading edge instead of hiding its title.
        if (Offsets[index + 1] - Offsets[index] > viewport) next = Offsets[index];
        return Math.Clamp(next, 0, Math.Max(0, Length - viewport));
    }
    private static void Validate(double offset, double viewport)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(viewport) || offset < 0 || viewport < 0) throw new ArgumentOutOfRangeException(nameof(offset));
    }
    private int LowerBound(double value)
    {
        int low = 0, high = Offsets.Length;
        while (low < high) { int middle = low + (high - low) / 2; if (Offsets[middle] < value) low = middle + 1; else high = middle; }
        return low;
    }
    private int UpperBound(double value)
    {
        int low = 0, high = Offsets.Length;
        while (low < high) { int middle = low + (high - low) / 2; if (Offsets[middle] <= value) low = middle + 1; else high = middle; }
        return low;
    }
}
