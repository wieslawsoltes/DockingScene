using DockingScene.Core;

namespace DockingScene.Layout;

public static partial class WindowGeometry
{
    private static void ValidateRect(DockRect rect)
    {
        if (!rect.IsValid || !double.IsFinite(rect.Right) || !double.IsFinite(rect.Bottom))
            throw new ArgumentException("Window bounds and their edges must be finite and positive.");
    }
    private static void ValidateViewport(DockRect rect)
    {
        if (!double.IsFinite(rect.X) || !double.IsFinite(rect.Y) || !double.IsFinite(rect.Width) ||
            !double.IsFinite(rect.Height) || rect.Width < 0 || rect.Height < 0 ||
            !double.IsFinite(rect.Right) || !double.IsFinite(rect.Bottom))
            throw new ArgumentException("Viewport bounds and their edges must be finite and nonnegative.");
    }
    private static DockRect Checked(DockRect rect) { ValidateRect(rect); return rect; }
}
