using System.Collections.Specialized;
using System.ComponentModel;
using DockingScene.Core;

namespace DockingScene.Mvvm;

/// <summary>Synchronizes an application-owned collection into a manager without touching explicitly registered windows.
/// Collection/metadata changes are atomic. If a close veto or invalid item rejects an external collection change,
/// the manager retains its previous snapshot and SynchronizationFailed reports the mismatch; call Synchronize after resolving it.
/// Call on the same dispatcher as the source collection. Dispose unsubscribes without closing application windows.</summary>
public sealed class DockSourceBinding<T> : IDisposable where T : class
{
    private readonly DockManager _manager;
    private readonly IEnumerable<T> _source;
    private readonly Func<T, DockWindow> _describe;
    private readonly INotifyCollectionChanged? _observable;
    private Dictionary<string, T> _owned = new(StringComparer.Ordinal);
    private readonly HashSet<INotifyPropertyChanged> _observed = new(ReferenceEqualityComparer.Instance);
    private bool _disposed, _synchronizing;
    public bool OpenNewWindows { get; }
    public DockResult LastResult { get; private set; }
    public event Action<DockResult>? SynchronizationFailed;

    public DockSourceBinding(DockManager manager, IEnumerable<T> source, Func<T, DockWindow> describe, bool openNewWindows = true)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _describe = describe ?? throw new ArgumentNullException(nameof(describe));
        OpenNewWindows = openNewWindows; _observable = source as INotifyCollectionChanged;
        Synchronize().ThrowIfFailed();
        if (_observable is not null) _observable.CollectionChanged += CollectionChanged;
    }
    public T? Resolve(string id) => _owned.GetValueOrDefault(id);
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (!_disposed) Synchronize(); }
    private void PropertyChanged(object? sender, PropertyChangedEventArgs e) { if (!_disposed) Synchronize(); }
    public DockResult Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_synchronizing) return new(false, _manager.Layout.Revision, "Reentrant source synchronization is not allowed.");
        _synchronizing = true;
        try
        {
            // Describing an item calls application code. Capture the revision
            // first so a callback cannot make this projection overwrite a newer
            // manager snapshot, even when there would otherwise be no operations.
            var before = _manager.Layout;
            var next = new Dictionary<string, T>(StringComparer.Ordinal);
            var descriptions = new Dictionary<string, DockWindow>(StringComparer.Ordinal);
            foreach (var item in _source)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (next.Count >= _manager.Policy.MaximumWindows) throw new InvalidOperationException("Source exceeds the configured window limit.");
                if (item is null) throw new ArgumentException("A docking source cannot contain null items.");
                var description = _describe(item) ?? throw new ArgumentException("The window descriptor cannot be null.");
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!next.TryAdd(description.Id, item)) throw new ArgumentException("Duplicate source window id: " + description.Id);
                descriptions.Add(description.Id, description);
            }
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_manager.Layout.Revision != before.Revision) throw new InvalidOperationException("The layout changed while describing the source.");
            var operations = new List<Func<DockManager, DockResult>>();
            foreach (var id in _owned.Keys.Where(id => !next.ContainsKey(id) && before.Windows.ContainsKey(id)))
            { string key = id; operations.Add(manager => manager.Unregister(key)); }
            foreach (var (id, description) in descriptions)
            {
                if (before.Windows.ContainsKey(id))
                {
                    if (!_owned.ContainsKey(id)) throw new ArgumentException("Source id collides with an explicitly registered window: " + id);
                    var current = before.Windows[id];
                    // Source owns metadata, while the docking engine owns current placement and geometric state.
                    var updated = current with { Title = description.Title, Description = description.Description, ContentKey = description.ContentKey,
                        Capabilities = description.Capabilities, IsModified = description.IsModified, IsReadOnly = description.IsReadOnly,
                        IsFlashing = description.IsFlashing, HasTitleBar = description.HasTitleBar, TabColor = description.TabColor,
                        MinimumSize = description.MinimumSize, MaximumSize = description.MaximumSize, DesiredSize = description.DesiredSize };
                    if (current.Kind != description.Kind) throw new ArgumentException("A source window cannot change kind: " + id);
                    if (updated != current) operations.Add(manager => manager.UpdateWindow(id, _ => updated));
                }
                else operations.Add(manager => manager.Register(description, OpenNewWindows));
            }
            LastResult = operations.Count == 0 ? new(true, before.Revision) : _manager.Transaction("SynchronizeSources", operations, before.Revision);
            // Changed observers run before Transaction returns and may dispose
            // this binding. A committed success stays a success; disposal only
            // prevents the obsolete adapter from reattaching item subscriptions.
            if (LastResult.Success && !_disposed)
            {
                var observed = new HashSet<INotifyPropertyChanged>(next.Values.OfType<INotifyPropertyChanged>(), ReferenceEqualityComparer.Instance);
                foreach (var item in _observed.Where(item => !observed.Contains(item)).ToArray())
                { item.PropertyChanged -= PropertyChanged; _observed.Remove(item); }
                foreach (var item in observed)
                {
                    if (_disposed) break;
                    if (!_observed.Add(item)) continue;
                    item.PropertyChanged += PropertyChanged;
                    // A custom event accessor can itself invoke application code.
                    if (_disposed) item.PropertyChanged -= PropertyChanged;
                }
                if (!_disposed) _owned = next;
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        { LastResult = new(false, _manager.Layout.Revision, error.Message); }
        finally { _synchronizing = false; }
        if (!LastResult.Success) SynchronizationFailed?.Invoke(LastResult);
        return LastResult;
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (_observable is not null) _observable.CollectionChanged -= CollectionChanged;
        foreach (var item in _observed) item.PropertyChanged -= PropertyChanged;
        _observed.Clear();
    }
}
