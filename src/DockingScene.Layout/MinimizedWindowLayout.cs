using System.Collections.Immutable;
using DockingScene.Core;

namespace DockingScene.Layout;

/// <summary>Bottom-up rows of minimized captions. When the host is too short,
/// rows extend above it rather than overlapping. RequiredHeight explicitly reports
/// the required extent; clipping/scrolling is a host decision.</summary>
public sealed record MinimizedWindowLayout(ImmutableDictionary<string, DockRect> Windows, double RequiredHeight, bool IsOverflowing)
{
    public static MinimizedWindowLayout Arrange(IReadOnlyList<string> ids, DockRect area, DockSize? captionSize = null, double gap = 2)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var size = captionSize ?? new DockSize(168, 26);
        if (!area.IsValid || !double.IsFinite(area.Right) || !double.IsFinite(area.Bottom) ||
            !size.IsFinite || size.Width <= 0 || size.Height <= 0 || !double.IsFinite(gap) || gap < 0)
            throw new ArgumentException("Minimized layout inputs must have finite positive extents and a nonnegative gap.");
        double width = Math.Min(size.Width, area.Width), stepX = width + gap, stepY = size.Height + gap;
        if (!double.IsFinite(stepX) || !double.IsFinite(stepY)) throw new ArgumentException("Minimized layout spacing overflowed.");
        var result = ImmutableDictionary.CreateBuilder<string, DockRect>(StringComparer.Ordinal);
        if (ids.Count == 0) return new(result.ToImmutable(), 0, false);
        // Subtract one caption before dividing to avoid area.Width + gap overflow.
        double fit = Math.Floor(Math.Max(0, area.Width - width) / stepX);
        int columns = fit >= ids.Count - 1 ? ids.Count : 1 + (int)fit;
        int rows = 1 + (ids.Count - 1) / columns;
        double height = size.Height + (rows - 1d) * stepY;
        if (!double.IsFinite(height)) throw new ArgumentException("Minimized layout height overflowed.");
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (string.IsNullOrWhiteSpace(id) || result.ContainsKey(id)) throw new ArgumentException("Window ids must be nonempty and unique.");
            double x = area.X + (i % columns) * stepX;
            double y = area.Bottom - size.Height - (i / columns) * stepY;
            var rect = new DockRect(x, y, width, size.Height);
            if (!rect.IsValid || !double.IsFinite(rect.Right) || !double.IsFinite(rect.Bottom)) throw new ArgumentException("Minimized layout coordinates overflowed.");
            result.Add(id, rect);
        }
        return new(result.ToImmutable(), height, height > area.Height);
    }
}
