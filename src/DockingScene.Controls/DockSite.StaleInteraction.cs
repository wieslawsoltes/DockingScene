namespace DockingScene.Controls;

public partial class DockSite
{
    private bool CancelStaleInteraction()
    {
        if (!IsInteractionActive) return false;
        long revision = Manager.Layout.Revision;
        bool stale = (!_dragIds.IsEmpty && revision != _dragRevision) ||
            (_splitDrag is not null && revision != _splitRevision) ||
            (_autoResizeId is not null && revision != _autoResizeRevision);
        if (!stale) return false;
        CancelInteraction();
        OperationRejected?.Invoke("The layout changed during the gesture; the provisional change was cancelled.");
        return true;
    }
}
