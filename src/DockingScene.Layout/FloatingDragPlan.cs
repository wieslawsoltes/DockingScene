using System.Collections.Immutable;
using DockingScene.Core;

namespace DockingScene.Layout;

/// <summary>Snapshot-scoped floating drag geometry and commit routing. Moving an
/// entire normal host preserves its tree, identity and size instead of re-floating
/// its documents. Queries reuse one immutable, self-excluding magnetism plan.</summary>
public sealed class FloatingDragPlan
{
    private readonly DockLayout _snapshot;
    public ImmutableArray<string> WindowIds { get; }
    public string? SourceHostId { get; }
    public DockRect InitialBounds { get; }
    public WindowMagnetism Magnetism { get; }
    public long Revision => _snapshot.Revision;

    public FloatingDragPlan(DockLayout snapshot, IReadOnlyList<string> windowIds, DockRect fallbackBounds,
        double snapDistance = 5, double gapDistance = 1)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(windowIds);
        ValidateBounds(fallbackBounds);
        int count = windowIds.Count;
        if (count <= 0 || count > WindowMagnetism.MaximumNeighbors) throw new ArgumentOutOfRangeException(nameof(windowIds));
        var ids = ImmutableArray.CreateBuilder<string>(count);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        var index = DockLayoutIndex.For(snapshot);
        var sourceHosts = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string id = windowIds[i];
            if (string.IsNullOrWhiteSpace(id) || !unique.Add(id) || !snapshot.Windows.ContainsKey(id))
                throw new ArgumentException("Drag members must be registered, nonempty and unique.", nameof(windowIds));
            var location = index.Locate(id);
            if (location.Placement == WindowPlacement.Hidden) throw new ArgumentException("Hidden windows cannot be dragged.", nameof(windowIds));
            if (location.HostId is { } hostId) sourceHosts.Add(hostId);
            ids.Add(id);
        }
        WindowIds = ids.MoveToImmutable(); _snapshot = snapshot; InitialBounds = fallbackBounds;
        if (sourceHosts.Count == 1)
        {
            var source = snapshot.FloatingHosts.First(host => sourceHosts.Contains(host.Id));
            if (!source.IsMaximized && DockTree.WindowIds(source.Root).ToHashSet(StringComparer.Ordinal).SetEquals(unique))
            { SourceHostId = source.Id; InitialBounds = source.Bounds; }
        }
        var neighbors = new List<DockRect>();
        foreach (var host in snapshot.FloatingHosts)
            if (!host.IsMaximized && !sourceHosts.Contains(host.Id)) neighbors.Add(host.Bounds);
        Magnetism = new(neighbors, snapDistance, gapDistance);
    }
    public DockRect Move(DockRect raw, bool bypass = false)
    {
        if (!bypass) return Magnetism.Move(raw);
        ValidateBounds(raw); return raw;
    }
    /// <summary>Commit only against the captured snapshot. A whole-host move
    /// needs Drag, not Float or Resize; detaching documents still requires Float.</summary>
    public DockResult Commit(DockManager manager, DockRect bounds)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (!ReferenceEquals(manager.Layout, _snapshot)) return new(false, manager.Layout.Revision, "The layout changed during the floating drag.");
        try { ValidateBounds(bounds); }
        catch (ArgumentException error) { return new(false, manager.Layout.Revision, error.Message); }
        if (SourceHostId is { } hostId)
        {
            if (bounds.Width != InitialBounds.Width || bounds.Height != InitialBounds.Height)
                return new(false, manager.Layout.Revision, "A whole-host drag cannot resize its source.");
            return manager.MoveFloatingHost(hostId, bounds.X, bounds.Y, Revision);
        }
        return manager.Transaction("FloatDraggedWindows", [m => m.FloatMany(WindowIds, bounds)], Revision);
    }
    private static void ValidateBounds(DockRect bounds)
    {
        if (!bounds.IsValid || !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom))
            throw new ArgumentException("Floating drag bounds must have finite positive extents and finite edges.");
    }
}
