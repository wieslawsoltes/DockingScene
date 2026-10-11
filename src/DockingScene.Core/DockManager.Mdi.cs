namespace DockingScene.Core;

public sealed partial class DockManager
{
    /// <summary>Move a normal MDI frame without resizing its restored bounds.
    /// Drag capability is sufficient; Resize is deliberately not required.</summary>
    public DockResult MoveMdiWindow(string id, double x, double y, long? expectedRevision = null) => Apply("MoveMdiWindow", layout =>
    {
        Require(layout, [id], DockCapabilities.Drag);
        var window = Get(layout, id);
        if (window.MdiState != MdiWindowState.Normal) throw new InvalidOperationException("Restore the MDI window before moving it.");
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("MDI coordinates must be finite.");
        if (window.MdiBounds.X == x && window.MdiBounds.Y == y) return layout;
        return layout with { Windows = layout.Windows.SetItem(id, window with { MdiBounds = window.MdiBounds with { X = x, Y = y } }) };
    }, expectedRevision);
}
