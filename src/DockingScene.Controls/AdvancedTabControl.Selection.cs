namespace DockingScene.Controls;

public partial class AdvancedTabControl
{
    private long _selectionIntent;
    private bool _headerPresentationPending;

    private void SelectCore(string? id, bool notify)
    {
        if (_itemsDirty) Refresh();
        var item = id is null ? null : _byId.GetValueOrDefault(id);
        if (id is not null && item is null) throw new ArgumentException("Unknown tab id.", nameof(id));
        long intent = ++_selectionIntent;
        // Factories are application callbacks. A nested selection must win, and a
        // failed factory must not leave the selected id ahead of its old content.
        item?.EnsureContent();
        if (intent != _selectionIntent) return;
        if (_itemsDirty) Refresh();
        if (item is not null && (!_byId.TryGetValue(id!, out var current) || !ReferenceEquals(item, current)))
        { QueueRefresh(); return; }
        _selectedId = id;
        ContentLease.Set(_presenter, item);
        if (intent != _selectionIntent) return;
        RevealSelection();
        RequestHeaderPresentation();
        if (notify && id is not null) Activated?.Invoke(id);
    }

    private void RequestHeaderPresentation()
    {
        if (_headerPresentationPending) return;
        _headerPresentationPending = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (!_headerPresentationPending) return;
            _headerPresentationPending = false;
            if (IsLoaded) RealizeHeaders();
        })) _headerPresentationPending = false;
    }
}
