namespace DockingScene.Core;

public sealed partial class DockManager
{
    /// <summary>Move a floating host without requiring permission to resize it.
    /// The entire host moves atomically and retains its normal width and height.</summary>
    public DockResult MoveFloatingHost(string hostId, double x, double y, long? expectedRevision = null) => Apply("MoveFloatingHost", layout =>
    {
        int index = IndexOfHost(layout, hostId); var host = layout.FloatingHosts[index];
        var ids = DockTree.WindowIds(host.Root).ToArray();
        Require(layout, ids, DockCapabilities.Drag);
        if (host.IsMaximized) throw new InvalidOperationException("Restore the floating host before moving it.");
        var bounds = host.Bounds with { X = x, Y = y };
        if (!bounds.IsValid || !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom))
            throw new ArgumentException("Invalid floating coordinates.");
        if (bounds == host.Bounds) return layout;
        var windows = layout.Windows;
        foreach (var id in ids) windows = windows.SetItem(id, windows[id] with { FloatingBounds = bounds });
        return layout with { Windows = windows, FloatingHosts = layout.FloatingHosts.SetItem(index, host with { Bounds = bounds }) };
    }, expectedRevision);
}
