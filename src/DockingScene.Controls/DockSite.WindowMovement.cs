using DockingScene.Core;

namespace DockingScene.Controls;

public partial class DockSite
{
    private readonly Dictionary<string, long> _floatingGestureRevisions = new(StringComparer.Ordinal);
    private void ConfigureFloatingMovement(WindowControl frame, string hostId)
    {
        var owner = Manager;
        bool Current() => ReferenceEquals(owner, Manager) && _floats.TryGetValue(hostId, out var current) && ReferenceEquals(frame, current);
        frame.Activated += () =>
        {
            if (!Current()) return;
            var layout = owner.Layout; var host = layout.FloatingHosts.FirstOrDefault(h => h.Id == hostId);
            if (host is null) return;
            var ids = DockTree.WindowIds(host.Root).ToArray();
            if (!ids.Contains(layout.ActiveWindowId) && ids.Length > 0) Submit(owner.Activate(ids[0]));
        };
        frame.MovementStarted += () =>
        {
            if (!Current()) { frame.CancelMovement(); return; }
            var layout = owner.Layout;
            _floatingGestureRevisions[hostId] = layout.Revision;
            if (!BeginFloatingLayout(frame, hostId)) return;
            if (frame.IsKeyboardInteractionActive || MagnetismSnapDistance == 0) return;
            var host = layout.FloatingHosts.FirstOrDefault(h => h.Id == hostId);
            if (host is null) { frame.CancelMovement(); return; }
            var plan = CreateFloatingDragPlan(layout, DockTree.WindowIds(host.Root).ToArray(), frame.Bounds);
            if (plan.Magnetism.NeighborCount > 0) frame.SetPointerMagnetism(plan.Magnetism);
        };
        frame.VisualBoundsChanged += () => { if (Current()) UpdateFloatingLayout(frame); };
        frame.MovementEnded += version => EndFloatingLayout(frame, hostId, version);
        frame.BoundsChanged += bounds =>
        {
            if (!Current() || !_floatingGestureRevisions.Remove(hostId, out long revision)) return;
            var layout = owner.Layout; var host = layout.FloatingHosts.FirstOrDefault(h => h.Id == hostId);
            if (host is null || layout.Revision != revision)
            { Submit(new(false, layout.Revision, "The floating layout changed during the gesture.")); QueueRender(); return; }
            Submit(bounds.Width == host.Bounds.Width && bounds.Height == host.Bounds.Height
                ? owner.MoveFloatingHost(hostId, bounds.X, bounds.Y, revision)
                : owner.Transaction("ResizeFloatingHost", [m => m.SetFloatingBounds(hostId, bounds)], revision));
            QueueRender();
        };
    }
    private void CancelStaleFloatingMovements()
    {
        long revision = Manager.Layout.Revision;
        foreach (var (id, frame) in _floats)
            if (frame.IsMoving && _floatingGestureRevisions.GetValueOrDefault(id, -1) != revision)
            { frame.CancelMovement(); _floatingGestureRevisions.Remove(id); }
    }
    private void ReleaseFloatingFrames()
    {
        foreach (var frame in _floats.Values)
        { frame.CancelMovement(); frame.DismissSystemMenu(); _canvas.Children.Remove(frame); }
        _floats.Clear(); _floatingGestureRevisions.Clear(); _floatingLayouts.Clear();
    }
}
