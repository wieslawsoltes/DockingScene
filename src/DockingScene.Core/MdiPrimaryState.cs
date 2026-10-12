namespace DockingScene.Core;

/// <summary>Renderer-independent command/presentation state for one MDI host.
/// The active hosted document wins; tool activation retains the hosted MRU document.
/// Consume validated layouts and an ordered list of IDs owned by the host.</summary>
public readonly record struct MdiPrimaryState(DockWindow? Window, bool CanMinimize, bool CanMaximize,
    bool CanRestore, bool CanClose, bool RequiresExternalChrome)
{
    public static MdiPrimaryState Create(DockLayout layout, IReadOnlyList<string> hostedIds,
        DockCapabilities allowed = DockCapabilities.All, bool canMinimize = true,
        bool canMaximize = true, bool maximizedFramesVisible = true)
    {
        ArgumentNullException.ThrowIfNull(layout); ArgumentNullException.ThrowIfNull(hostedIds);
        var index = DockLayoutIndex.For(layout);
        DockWindow? primary = null; int bestRank = int.MaxValue;
        foreach (string id in hostedIds)
        {
            if (id is null || !layout.Windows.TryGetValue(id, out var window) || window.Kind != WindowKind.Document ||
                index.Locate(id).Placement == WindowPlacement.Hidden) continue;
            int rank = layout.ActiveWindowId == id ? -1 : index.ActivationRank(id);
            if (rank < 0 && layout.ActiveWindowId != id) rank = int.MaxValue;
            if (primary is null || rank < bestRank) { primary = window; bestRank = rank; }
            if (rank == -1) break;
        }
        if (primary is null) return default;
        var capabilities = primary.Capabilities & allowed;
        bool resize = !layout.IsLayoutLocked && capabilities.HasFlag(DockCapabilities.Resize);
        return new(primary,
            resize && canMinimize && primary.MdiState != MdiWindowState.Minimized,
            resize && canMaximize && primary.MdiState != MdiWindowState.Maximized,
            resize && primary.MdiState != MdiWindowState.Normal,
            capabilities.HasFlag(DockCapabilities.Close),
            primary.MdiState == MdiWindowState.Maximized && !maximizedFramesVisible);
    }
}
