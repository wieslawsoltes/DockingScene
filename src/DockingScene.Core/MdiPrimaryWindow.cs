namespace DockingScene.Core;

/// <summary>Current host-scoped primary window and state-command availability.
/// This is presentation state, not permission to bypass manager validation.</summary>
public readonly record struct MdiPrimaryWindowState(string? Id, bool CanMinimize, bool CanMaximize, bool CanRestore, bool CanClose);

public static class MdiPrimaryWindow
{
    /// <summary>Resolve the most recently activated open member of a host. An
    /// active tool outside the host never becomes a target. Caller order breaks
    /// ties for windows without activation history. Unknown/hidden ids are ignored.</summary>
    public static MdiPrimaryWindowState Resolve(DockLayout layout, IReadOnlyList<string> windowIds,
        DockCapabilities allowed = DockCapabilities.All, bool canMinimize = true, bool canMaximize = true)
    {
        ArgumentNullException.ThrowIfNull(layout); ArgumentNullException.ThrowIfNull(windowIds);
        var index = DockLayoutIndex.For(layout);
        DockWindow? primary = null;
        int bestRank = int.MaxValue;
        foreach (string id in windowIds)
        {
            if (id is null || !layout.Windows.TryGetValue(id, out var window) || index.Locate(id).Placement == WindowPlacement.Hidden ||
                window.Kind == WindowKind.Tool && index.GroupOf(id)?.Kind != GroupKind.Documents) continue;
            if (layout.ActiveWindowId == id) { primary = window; break; }
            int rank = index.ActivationRank(id); if (rank < 0) rank = int.MaxValue;
            if (primary is null || rank < bestRank) { primary = window; bestRank = rank; }
        }
        if (primary is null) return default;
        var capabilities = primary.Capabilities & allowed;
        bool resize = !layout.IsLayoutLocked && capabilities.HasFlag(DockCapabilities.Resize);
        return new(primary.Id,
            resize && canMinimize && primary.CanStandardMdiMinimize && primary.MdiState != MdiWindowState.Minimized,
            resize && canMaximize && primary.CanStandardMdiMaximize && primary.MdiState != MdiWindowState.Maximized,
            resize && primary.MdiState != MdiWindowState.Normal,
            capabilities.HasFlag(DockCapabilities.Close));
    }
}
