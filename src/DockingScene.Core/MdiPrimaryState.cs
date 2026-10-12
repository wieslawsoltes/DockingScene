namespace DockingScene.Core;

/// <summary>Renderer-independent primary document and external-chrome state.
/// Retained from the earlier presentation branch; command resolution is shared
/// with MdiPrimaryWindow rather than maintained as a second algorithm.</summary>
public readonly record struct MdiPrimaryState(DockWindow? Window, bool CanMinimize, bool CanMaximize,
    bool CanRestore, bool CanClose, bool RequiresExternalChrome)
{
    public static MdiPrimaryState Create(DockLayout layout, IReadOnlyList<string> hostedIds,
        DockCapabilities allowed = DockCapabilities.All, bool canMinimize = true,
        bool canMaximize = true, bool maximizedFramesVisible = true)
    {
        var state = MdiPrimaryWindow.Resolve(layout, hostedIds, allowed, canMinimize, canMaximize);
        if (state.Id is not { } id) return default;
        var window = layout.Windows[id];
        return new(window, state.CanMinimize, state.CanMaximize, state.CanRestore, state.CanClose,
            window.MdiState == MdiWindowState.Maximized && !maximizedFramesVisible);
    }
}
