using DockingScene.Core;

namespace DockingScene.Layout;

public enum WindowKeyboardMode { Move, Resize }
public enum WindowKeyboardDirection { Left, Right, Up, Down }

/// <summary>UI-independent provisional window geometry. Resize chooses each axis's
/// edge with its first arrow; later arrows move that edge while its opposite stays
/// anchored. Complete/cancel returns one final rectangle, never a model mutation.</summary>
public sealed class WindowKeyboardSession
{
    public DockRect OriginalBounds { get; }
    public DockRect Bounds { get; private set; }
    public WindowKeyboardMode Mode { get; }
    public DockSide HorizontalEdge { get; private set; } = DockSide.Center;
    public DockSide VerticalEdge { get; private set; } = DockSide.Center;
    public bool IsCompleted { get; private set; }
    public bool HasChanges => Bounds != OriginalBounds;

    public WindowKeyboardSession(DockRect bounds, WindowKeyboardMode mode)
    {
        ValidateBounds(bounds);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        OriginalBounds = Bounds = bounds; Mode = mode;
    }
    public bool Step(WindowKeyboardDirection direction, double increment, DockSize minimum, DockSize maximum)
    {
        if (IsCompleted) throw new InvalidOperationException("The keyboard session has ended.");
        if (!Enum.IsDefined(direction) || !double.IsFinite(increment) || increment <= 0)
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (!minimum.IsFinite || !maximum.IsFinite || minimum.Width < 0 || minimum.Height < 0 ||
            maximum.Width <= 0 || maximum.Height <= 0 || minimum.Width > maximum.Width || minimum.Height > maximum.Height)
            throw new ArgumentException("Invalid window size constraints.");
        bool horizontal = direction is WindowKeyboardDirection.Left or WindowKeyboardDirection.Right;
        double delta = direction is WindowKeyboardDirection.Left or WindowKeyboardDirection.Up ? -increment : increment;
        var next = Bounds;
        if (Mode == WindowKeyboardMode.Move)
            next = horizontal ? next with { X = next.X + delta } : next with { Y = next.Y + delta };
        else if (horizontal)
        {
            if (HorizontalEdge == DockSide.Center)
            { HorizontalEdge = direction == WindowKeyboardDirection.Left ? DockSide.Left : DockSide.Right; return false; }
            double width = Math.Clamp(next.Width + (HorizontalEdge == DockSide.Left ? -delta : delta), Math.Max(double.Epsilon, minimum.Width), maximum.Width);
            next = next with { X = HorizontalEdge == DockSide.Left ? next.Right - width : next.X, Width = width };
        }
        else
        {
            if (VerticalEdge == DockSide.Center)
            { VerticalEdge = direction == WindowKeyboardDirection.Up ? DockSide.Top : DockSide.Bottom; return false; }
            double height = Math.Clamp(next.Height + (VerticalEdge == DockSide.Top ? -delta : delta), Math.Max(double.Epsilon, minimum.Height), maximum.Height);
            next = next with { Y = VerticalEdge == DockSide.Top ? next.Bottom - height : next.Y, Height = height };
        }
        ValidateBounds(next);
        bool changed = next != Bounds; Bounds = next; return changed;
    }
    public DockRect Complete(bool commit)
    {
        if (IsCompleted) throw new InvalidOperationException("The keyboard session has ended.");
        IsCompleted = true;
        if (!commit) Bounds = OriginalBounds;
        return Bounds;
    }
    private static void ValidateBounds(DockRect bounds)
    {
        if (!bounds.IsValid || !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom))
            throw new ArgumentException("Window bounds must have finite positive extents and finite edges.");
    }
}
