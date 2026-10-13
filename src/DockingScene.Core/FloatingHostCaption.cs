using System.Runtime.CompilerServices;

namespace DockingScene.Core;

/// <summary>Resolves the primary floating document (or tool in a tool-only host)
/// independently of the main site's active window. Structural membership is
/// cached weakly; activation and metadata are always read from the new snapshot.</summary>
public static class FloatingHostCaption
{
    private static readonly ConditionalWeakTable<DockNode, string[]> Members = new();
    public static DockWindow GetPrimaryWindow(DockLayout layout, string hostId)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var host = layout.FloatingHosts.FirstOrDefault(h => h.Id == hostId)
            ?? throw new ArgumentException("Unknown floating host.", nameof(hostId));
        var ids = Members.GetValue(host.Root, static root => DockTree.WindowIds(root).ToArray());
        var index = DockLayoutIndex.For(layout);
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
        return selected ?? throw new ArgumentException("Floating host has no windows.", nameof(hostId));
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
