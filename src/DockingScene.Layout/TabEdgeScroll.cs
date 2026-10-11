namespace DockingScene.Layout;

/// <summary>Frame-rate-independent drag edge scrolling in one logical axis. Positions
/// outside the lane do not scroll. Delayed ticks are capped at 100 ms to avoid jumps.</summary>
public static class TabEdgeScroll
{
    public static double NextOffset(double position, double viewport, double contentExtent, double offset,
        double elapsedSeconds, double margin = 24, double maximumSpeed = 600)
    {
        if (!double.IsFinite(position) || !double.IsFinite(viewport) || viewport < 0 ||
            !double.IsFinite(contentExtent) || contentExtent < 0 || !double.IsFinite(offset) || offset < 0 ||
            !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0 || !double.IsFinite(margin) || margin <= 0 ||
            !double.IsFinite(maximumSpeed) || maximumSpeed <= 0) throw new ArgumentOutOfRangeException(nameof(position));
        double maximum = Math.Max(0, contentExtent - viewport);
        offset = Math.Min(offset, maximum);
        double edge = Math.Min(margin, viewport / 2);
        if (edge == 0 || maximum == 0 || elapsedSeconds == 0 || position < 0 || position > viewport) return offset;
        double factor = position < edge ? -(edge - position) / edge : position > viewport - edge ? (position - (viewport - edge)) / edge : 0;
        double distance = factor * maximumSpeed * Math.Min(.1, elapsedSeconds);
        return distance >= 0 ? offset + Math.Min(distance, maximum - offset) : offset - Math.Min(-distance, offset);
    }
}
