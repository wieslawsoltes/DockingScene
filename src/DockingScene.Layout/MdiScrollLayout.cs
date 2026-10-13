using DockingScene.Core;

namespace DockingScene.Layout;

/// <summary>Renderer-independent Standard MDI scroll extents. World coordinates
/// (including negative coordinates) remain separate from visible frame positions.
/// The value-type query and mapping methods allocate no managed objects.</summary>
public readonly record struct MdiScrollLayout(DockRect ContentBounds, DockRect Viewport,
    bool HasHorizontalScrollBar, bool HasVerticalScrollBar)
{
    public const int MaximumWindows = 10000;
    public double HorizontalOffset => Viewport.X - ContentBounds.X;
    public double VerticalOffset => Viewport.Y - ContentBounds.Y;
    public double ScrollableWidth => Math.Max(0, ContentBounds.Width - Viewport.Width);
    public double ScrollableHeight => Math.Max(0, ContentBounds.Height - Viewport.Height);

    public static MdiScrollLayout Measure(ReadOnlySpan<DockRect> windows, DockSize client,
        double viewportX = 0, double viewportY = 0, double scrollBarSize = 16)
    {
        if (!client.IsFinite || client.Width < 1 || client.Height < 1 ||
            !double.IsFinite(viewportX) || !double.IsFinite(viewportY) ||
            !double.IsFinite(scrollBarSize) || scrollBarSize < 0 || windows.Length > MaximumWindows)
            throw new ArgumentException("Invalid MDI scrolling input.");
        double left = 0, top = 0, right = 0, bottom = 0;
        foreach (var rect in windows)
        {
            Validate(rect);
            left = Math.Min(left, rect.X); top = Math.Min(top, rect.Y);
            right = Math.Max(right, rect.Right); bottom = Math.Max(bottom, rect.Bottom);
        }
        bool horizontal = false, vertical = false;
        double width = client.Width, height = client.Height;
        // The appearance of either bar can require the other. Visibility grows
        // monotonically and stabilizes after at most two additions.
        for (int i = 0; i < 3; i++)
        {
            width = Math.Max(1, client.Width - (vertical ? scrollBarSize : 0));
            height = Math.Max(1, client.Height - (horizontal ? scrollBarSize : 0));
            horizontal |= left < 0 || right > width;
            vertical |= top < 0 || bottom > height;
        }
        var content = new DockRect(left, top, Math.Max(width, right) - left, Math.Max(height, bottom) - top);
        Validate(content);
        var view = new DockRect(Math.Clamp(viewportX, left, content.Right - width),
            Math.Clamp(viewportY, top, content.Bottom - height), width, height);
        return new(content, view, horizontal, vertical);
    }
    public DockRect ToDisplay(DockRect world) => world with { X = world.X - Viewport.X, Y = world.Y - Viewport.Y };
    public DockRect ToWorld(DockRect displayed) => displayed with { X = displayed.X + Viewport.X, Y = displayed.Y + Viewport.Y };
    public MdiScrollLayout WithOffsets(double horizontal, double vertical)
    {
        if (!double.IsFinite(horizontal) || !double.IsFinite(vertical)) throw new ArgumentException("Scroll offsets must be finite.");
        return this with { Viewport = Viewport with {
            X = ContentBounds.X + Math.Clamp(horizontal, 0, ScrollableWidth),
            Y = ContentBounds.Y + Math.Clamp(vertical, 0, ScrollableHeight) } };
    }
    /// <summary>Reveal a frame using minimal movement. An oversized frame aligns
    /// its leading edge, rather than alternating between its opposite edges.</summary>
    public MdiScrollLayout Reveal(DockRect target, double margin = 8)
    {
        Validate(target);
        if (!double.IsFinite(margin) || margin < 0) throw new ArgumentOutOfRangeException(nameof(margin));
        static double Axis(double start, double extent, double view, double size, double padding)
        {
            padding = Math.Min(padding, size / 2);
            if (extent >= size - 2 * padding || start - padding < view) return start - padding;
            if (start + extent + padding > view + size) return start + extent + padding - size;
            return view;
        }
        double x = Axis(target.X, target.Width, Viewport.X, Viewport.Width, margin);
        double y = Axis(target.Y, target.Height, Viewport.Y, Viewport.Height, margin);
        return WithOffsets(x - ContentBounds.X, y - ContentBounds.Y);
    }
    private static void Validate(DockRect rect)
    {
        if (!rect.IsValid || !double.IsFinite(rect.Right) || !double.IsFinite(rect.Bottom))
            throw new ArgumentException("Scroll geometry requires finite positive extents and finite edges.");
    }
}
