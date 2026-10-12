using DockingScene.Core;

namespace DockingScene.Layout;

public static partial class WindowGeometry
{
    /// <summary>Arrange only the supplied open document-host members in caller order.
    /// Unrelated workspaces and floating hosts are unchanged. A single immutable
    /// builder and one Restore validation replace per-window trial transactions.</summary>
    public static DockResult ArrangeMdi(DockManager manager, IReadOnlyList<string> hostedIds,
        DockRect area, MdiArrangement arrangement, double titleHeight = 26, long? expectedRevision = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(hostedIds);
        var snapshot = manager.Layout;
        if (expectedRevision is { } revision && revision != snapshot.Revision)
            return new(false, snapshot.Revision, "Stale layout revision.");
        try
        {
            int count = hostedIds.Count;
            if (count < 0 || count > manager.Policy.MaximumWindows)
                throw new ArgumentException("The host exceeds the configured window limit.");
            var ids = new string[count];
            for (int i = 0; i < count; i++) ids[i] = hostedIds[i];
            // Caller-provided indexers can invoke application code. Never overwrite
            // a newer layout, including when this would otherwise be a no-op.
            if (!ReferenceEquals(manager.Layout, snapshot))
                return new(false, manager.Layout.Revision, "The layout changed while reading host members.");
            var placements = ArrangeMdi(ids, area, arrangement, titleHeight);
            var index = DockLayoutIndex.For(snapshot);
            foreach (string id in ids)
            {
                if (!snapshot.Windows.TryGetValue(id, out var window) ||
                    index.Locate(id).Placement == WindowPlacement.Hidden || index.GroupOf(id)?.Kind != GroupKind.Documents)
                    throw new ArgumentException("The host contains an unknown, hidden or non-document-group window: " + id);
                if (snapshot.IsLayoutLocked || (window.Capabilities & manager.Policy.AllowedCapabilities & DockCapabilities.Resize) == 0)
                    throw new InvalidOperationException("MDI arrangement is not allowed: " + id);
            }
            var windows = snapshot.Windows.ToBuilder();
            bool changed = false;
            foreach (var (id, rect) in placements)
            {
                var window = windows[id];
                if (window.MdiBounds == rect && window.MdiState == MdiWindowState.Normal) continue;
                windows[id] = window with { MdiBounds = rect, MdiState = MdiWindowState.Normal };
                changed = true;
            }
            // An empty host must not change the global presentation mode. A
            // value-equal arrangement must retain undo/redo and snapshot identity.
            var next = changed || ids.Length > 0 && snapshot.MdiMode != MdiMode.Standard
                ? snapshot with { Windows = changed ? windows.ToImmutable() : snapshot.Windows, MdiMode = MdiMode.Standard }
                : snapshot;
            return manager.Restore(next, snapshot.Revision);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { return new(false, manager.Layout.Revision, error.Message); }
    }
}
