using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

/// <summary>Retained overlapping-document host. Call Update to project a manager
/// snapshot; commands originating in this host also refresh its projection.</summary>
public sealed class StandardMdiHost : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly Dictionary<string, WindowControl> _windows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _gestureRevisions = new(StringComparer.Ordinal);
    private System.Collections.ObjectModel.ReadOnlyDictionary<string, WindowControl>? _windowView;
    private DockManager? _manager;
    private string[] _ids = [];
    private Func<string, AdvancedTabItem>? _content;
    private DockTheme _theme = DockTheme.MetroLight;
    private bool _active, _refreshing, _refreshQueued;
    public static readonly DependencyProperty CanWindowsMinimizeProperty = DependencyProperty.Register(nameof(CanWindowsMinimize), typeof(bool), typeof(StandardMdiHost), new PropertyMetadata(true, OptionsChanged));
    public static readonly DependencyProperty CanWindowsMaximizeProperty = DependencyProperty.Register(nameof(CanWindowsMaximize), typeof(bool), typeof(StandardMdiHost), new PropertyMetadata(true, OptionsChanged));
    public bool CanWindowsMinimize { get => (bool)GetValue(CanWindowsMinimizeProperty); set => SetValue(CanWindowsMinimizeProperty, value); }
    public bool CanWindowsMaximize { get => (bool)GetValue(CanWindowsMaximizeProperty); set => SetValue(CanWindowsMaximizeProperty, value); }
    public MinimizedWindowLayout? MinimizedLayout { get; private set; }
    public IReadOnlyDictionary<string, WindowControl> Windows => _windowView ??= new(_windows);
    public event Action<string>? OperationRejected;
    private static void OptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((StandardMdiHost)d).Refresh();

    public StandardMdiHost()
    {
        Content = _canvas;
        SizeChanged += (_, _) => Refresh();
        Unloaded += (_, _) => { foreach (var window in _windows.Values) window.CancelMovement(); };
    }
    public void Update(DockManager manager, IReadOnlyList<string> ids, Func<string, AdvancedTabItem> content, DockTheme theme)
    {
        ArgumentNullException.ThrowIfNull(manager); ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(theme);
        var next = ids.ToArray();
        if (next.Any(id => string.IsNullOrWhiteSpace(id) || !manager.Layout.Windows.ContainsKey(id)) || next.Distinct(StringComparer.Ordinal).Count() != next.Length)
            throw new ArgumentException("MDI ids must be registered, nonempty and unique.", nameof(ids));
        if (!ReferenceEquals(_manager, manager)) ReleaseContent();
        _manager = manager; _ids = next; _content = content; _theme = theme; _active = true; Refresh();
    }
    public void ReleaseContent()
    {
        _active = false;
        foreach (var window in _windows.Values) { window.CancelMovement(); window.SetWindowContent(null); }
        _gestureRevisions.Clear();
    }
    public IReadOnlyList<DockWindow> GetDocuments()
    {
        var layout = _manager?.Layout;
        return layout is null ? [] : _ids.Where(id => layout.Windows.ContainsKey(id) && DockTree.Locate(layout, id).Placement != WindowPlacement.Hidden).Select(id => layout.Windows[id]).ToArray();
    }
    public void ArrangeMinimizedWindows() => Refresh();
    public DockResult Arrange(MdiArrangement arrangement) => _manager is null ? new(false, 0, "No manager.") :
        WindowGeometry.ArrangeMdi(_manager, new(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight)), arrangement);
    private void Submit(DockResult result)
    {
        if (!result.Success) OperationRejected?.Invoke(result.Error ?? "MDI operation rejected.");
        Refresh();
    }
    private void CommitBounds(string id, DockRect rect)
    {
        if (_manager is null || !_gestureRevisions.Remove(id, out long revision)) return;
        var layout = _manager.Layout;
        if (layout.Revision != revision || !layout.Windows.TryGetValue(id, out var model))
        { Submit(new(false, layout.Revision, "The MDI layout changed during the gesture.")); return; }
        Submit(rect.Width == model.MdiBounds.Width && rect.Height == model.MdiBounds.Height
            ? _manager.MoveMdiWindow(id, rect.X, rect.Y, revision)
            : _manager.Transaction("ResizeMdiWindow", [m => m.SetMdiBounds(id, rect)], revision));
    }
    private void Refresh()
    {
        if (!_active || _manager is null || _content is null) return;
        if (_refreshing) { QueueRefresh(); return; }
        _refreshing = true;
        try
        {
            var manager = _manager; var layout = manager.Layout;
            var index = DockLayoutIndex.For(layout);
            var ordered = _ids.Where(id => layout.Windows.ContainsKey(id) && index.Locate(id).Placement != WindowPlacement.Hidden).ToArray(); var ids = ordered.ToHashSet(StringComparer.Ordinal);
            _canvas.Background = DockChrome.Brush(_theme.InactiveTab);
            _canvas.Clip = new RectangleGeometry { Rect = new(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)) };
            foreach (string id in _windows.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                _windows[id].CancelMovement(); _windows[id].SetWindowContent(null); _canvas.Children.Remove(_windows[id]);
                _windows.Remove(id); _gestureRevisions.Remove(id);
            }
            var viewport = new DockRect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
            MinimizedLayout = MinimizedWindowLayout.Arrange(ordered.Where(id => layout.Windows[id].MdiState == MdiWindowState.Minimized).ToArray(), viewport, new(168, _theme.TitleHeight + 2));
            foreach (string id in ordered)
            {
                if (!_active || !ReferenceEquals(manager, _manager) || manager.Layout.Revision != layout.Revision) { QueueRefresh(); break; }
                var model = layout.Windows[id];
                if (!_windows.TryGetValue(id, out var window))
                {
                    window = new() { WindowId = id, ShowMinimize = true };
                    string key = id;
                    window.Activated += () => { if (_active) Submit(_manager!.Activate(key)); };
                    window.MovementStarted += () => _gestureRevisions[key] = _manager!.Layout.Revision;
                    window.BoundsChanged += rect => CommitBounds(key, rect);
                    window.CloseRequested += () => { if (_active) Submit(_manager!.Close(key)); };
                    window.RestoreRequested += () => { if (_active) Submit(_manager!.SetMdiState(key, MdiWindowState.Normal)); };
                    window.MaximizeRequested += () => { if (_active && CanWindowsMaximize) Submit(_manager!.SetMdiState(key, MdiWindowState.Maximized)); };
                    window.MinimizeRequested += () => { if (_active && CanWindowsMinimize) Submit(_manager!.SetMdiState(key, MdiWindowState.Minimized)); };
                    _windows.Add(id, window); _canvas.Children.Add(window);
                }
                if (window.IsMoving && _gestureRevisions.GetValueOrDefault(id, -1) != layout.Revision)
                { window.CancelMovement(); _gestureRevisions.Remove(id); }
                bool canResize = manager.Allows(id, DockCapabilities.Resize) && !layout.IsLayoutLocked;
                window.MinimumSize = new(model.MinimumSize.Width + 2, model.MinimumSize.Height + _theme.TitleHeight + 2);
                window.MaximumSize = new(model.MaximumSize.Width + 2, model.MaximumSize.Height + _theme.TitleHeight + 2);
                window.CanResize = canResize && model.MdiState == MdiWindowState.Normal;
                window.CanMove = manager.Allows(id, DockCapabilities.Drag) && !layout.IsLayoutLocked && model.MdiState == MdiWindowState.Normal;
                window.CanClose = manager.Allows(id, DockCapabilities.Close); window.CanRestore = canResize;
                window.ShowMaximize = canResize && CanWindowsMaximize; window.ShowMinimize = canResize && CanWindowsMinimize;
                window.IsMaximized = model.MdiState == MdiWindowState.Maximized; window.IsMinimized = model.MdiState == MdiWindowState.Minimized;
                var bounds = model.MdiState switch
                {
                    MdiWindowState.Maximized => viewport,
                    MdiWindowState.Minimized => MinimizedLayout.Windows[id],
                    _ => WindowGeometry.KeepVisible(model.MdiBounds, viewport)
                };
                if (!window.IsMoving) window.SetBounds(bounds);
                int rank = index.ActivationRank(id);
                Canvas.SetZIndex(window, rank < 0 ? 0 : layout.ActivationHistory.Length - rank + 1);
                window.UpdateChrome(model.Title + (model.IsModified ? " *" : ""), _theme, layout.ActiveWindowId == id);
                if (window.IsMinimized) window.SetWindowContent(null);
                else { var content = _content(id); content.EnsureContent(); if (_active && ReferenceEquals(manager, _manager) && manager.Layout.Revision == layout.Revision) window.SetWindowContent(content); else QueueRefresh(); }
            }
        }
        finally { _refreshing = false; }
    }
    private void QueueRefresh()
    {
        if (_refreshQueued) return; _refreshQueued = true;
        if (!DispatcherQueue.TryEnqueue(() => { _refreshQueued = false; if (_active && IsLoaded) Refresh(); })) _refreshQueued = false;
    }
}
