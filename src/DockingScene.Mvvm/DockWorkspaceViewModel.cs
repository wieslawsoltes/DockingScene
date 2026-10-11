using System.Collections.ObjectModel;
using System.ComponentModel;
using DockingScene.Core;

namespace DockingScene.Mvvm;

/// <summary>Observable projection with stable window identities and optional UI-dispatcher marshalling.
/// Bind DockSite.Manager to Manager. Content remains application-owned and is never serialized or disposed here.</summary>
public sealed class DockWorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ObservableCollection<DockWindowViewModel> _windows = [];
    private readonly ObservableCollection<DockWindowViewModel> _documents = [];
    private readonly ObservableCollection<DockWindowViewModel> _tools = [];
    private readonly Dictionary<string, DockWindowViewModel> _byId = new(StringComparer.Ordinal);
    private readonly Action<Action> _dispatch;
    private volatile bool _disposed;
    private long _revision = -1;
    private DockLayout _snapshot;
    public DockManager Manager { get; }
    public ReadOnlyObservableCollection<DockWindowViewModel> Windows { get; }
    public ReadOnlyObservableCollection<DockWindowViewModel> Documents { get; }
    public ReadOnlyObservableCollection<DockWindowViewModel> ToolWindows { get; }
    public long Revision => _revision;
    public DockWindowViewModel? ActiveWindow => _snapshot.ActiveWindowId is { } id ? _byId.GetValueOrDefault(id) : null;
    public bool IsLayoutLocked { get => _snapshot.IsLayoutLocked; set { EnsureAlive(); Execute(Manager.SetLayoutLocked(value)); } }
    public MdiMode MdiMode { get => _snapshot.MdiMode; set { EnsureAlive(); Execute(Manager.SetMdiMode(value)); } }
    public bool IsDisposed => _disposed;
    public DockCommand UndoCommand { get; }
    public DockCommand RedoCommand { get; }
    public DockResult? LastResult { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<DockResult>? OperationCompleted;
    public event Action<DockWindowViewModel>? WindowAdded;

    public DockWorkspaceViewModel(DockManager manager, Action<Action>? dispatch = null)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _dispatch = dispatch ?? (action => action()); _snapshot = manager.Layout;
        Windows = new(_windows); Documents = new(_documents); ToolWindows = new(_tools);
        UndoCommand = new(() => Execute(Manager.Undo()), () => !_disposed && Manager.CanUndo);
        RedoCommand = new(() => Execute(Manager.Redo()), () => !_disposed && Manager.CanRedo);
        manager.Changed += ManagerChanged; ApplySnapshot(manager.Layout);
    }
    public DockWindowViewModel? Find(string id) => _byId.GetValueOrDefault(id);
    public DockResult Register(DockWindow window, object? content = null, bool open = true)
    {
        EnsureAlive(); var result = Execute(Manager.Register(window, open));
        if (result.Success) _dispatch(() => { if (!_disposed && Find(window.Id) is { } model) model.Content = content; });
        return result;
    }
    internal DockResult Execute(DockResult result)
    {
        EnsureAlive();
        _dispatch(() =>
        {
            if (_disposed) return;
            LastResult = result; PropertyChanged?.Invoke(this, new(nameof(LastResult))); OperationCompleted?.Invoke(result);
        });
        return result;
    }
    internal void EnsureAlive() => ObjectDisposedException.ThrowIf(_disposed, this);
    private void ManagerChanged(object? sender, DockChangedEventArgs e) => _dispatch(() => ApplySnapshot(e.After));
    private void ApplySnapshot(DockLayout snapshot)
    {
        if (_disposed || snapshot.Revision <= _revision) return;
        _snapshot = snapshot; _revision = snapshot.Revision;
        foreach (var id in _byId.Keys.Where(id => !snapshot.Windows.ContainsKey(id)).ToArray())
        {
            var model = _byId[id]; model.Detach(); _windows.Remove(model); _documents.Remove(model); _tools.Remove(model); _byId.Remove(id);
        }
        foreach (var window in snapshot.Windows.Values.OrderBy(window => window.Id, StringComparer.Ordinal))
        {
            if (!_byId.TryGetValue(window.Id, out var model))
            {
                model = new(this, window); _byId.Add(window.Id, model); _windows.Add(model);
                (window.Kind == WindowKind.Document ? _documents : _tools).Add(model);
                model.Refresh(snapshot); WindowAdded?.Invoke(model);
            }
            else model.Refresh(snapshot);
        }
        UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged(); PropertyChanged?.Invoke(this, new(null));
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Manager.Changed -= ManagerChanged;
        foreach (var model in _windows) model.Detach();
        UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged();
    }
}
