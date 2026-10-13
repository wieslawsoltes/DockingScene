using System.Runtime.CompilerServices;

namespace DockingScene.Core;

/// <summary>Resolves the primary floating document (or tool in a tool-only host)
/// independently of the main site's active window. Weak, identity-keyed caches
/// retain structural membership and one primary result per immutable snapshot.
/// Repeated host queries do not rescan the host collection or its members.</summary>
public static class FloatingHostCaption
{
    private static readonly ConditionalWeakTable<DockNode, string[]> Members = new();
    private static readonly ConditionalWeakTable<DockLayout, SnapshotCaptions> Snapshots = new();

    public static DockWindow GetPrimaryWindow(DockLayout layout, string hostId)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(hostId);
        // The common path performs only read lookups. A new immutable snapshot
        // gets a complete independent projection even when its revision is equal.
        if (!Snapshots.TryGetValue(layout, out var captions))
            captions = Snapshots.GetValue(layout, static snapshot => new(snapshot));
        return captions.Get(hostId);
    }

    private sealed class SnapshotCaptions
    {
        // Immutable after construction; concurrent readers never see a partial map.
        // No reference to the source snapshot is retained by this cache value.
        private readonly Dictionary<string, DockWindow?> _primary = new(StringComparer.Ordinal);
        public SnapshotCaptions(DockLayout layout)
        {
            var index = DockLayoutIndex.For(layout);
            foreach (var host in layout.FloatingHosts)
            {
                var ids = Members.GetValue(host.Root, static root => DockTree.WindowIds(root).ToArray());
                DockWindow? selected = null;
                int bestRank = int.MaxValue;
                foreach (var id in ids)
                {
                    var window = layout.Windows[id];
                    bool document = window.Kind == WindowKind.Document;
                    if (selected?.Kind == WindowKind.Document && !document) continue;
                    int rank = index.ActivationRank(id);
                    if (rank < 0) rank = int.MaxValue;
                    if (selected is null || document && selected.Kind != WindowKind.Document || rank < bestRank)
                    { selected = window; bestRank = rank; }
                }
                _primary.Add(host.Id, selected);
            }
        }
        public DockWindow Get(string hostId)
        {
            if (!_primary.TryGetValue(hostId, out var window))
                throw new ArgumentException("Unknown floating host.", nameof(hostId));
            return window ?? throw new ArgumentException("Floating host has no windows.", nameof(hostId));
        }
    }

    public static string GetTitle(DockLayout layout, string hostId, string? applicationTitle = null, string? delimiter = " - ")
        => Format(GetPrimaryWindow(layout, hostId), applicationTitle, delimiter);
    public static string Format(DockWindow primary, string? applicationTitle, string? delimiter = " - ")
    {
        ArgumentNullException.ThrowIfNull(primary);
        return primary.Kind == WindowKind.Document && !string.IsNullOrEmpty(applicationTitle)
            ? string.Concat(primary.Title, delimiter, applicationTitle) : primary.Title;
    }
}
