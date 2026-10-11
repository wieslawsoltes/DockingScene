using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

/// <summary>Standalone overlapping-document host, backed by the same transactional document model.</summary>
public sealed class StandardMdiHost : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly Dictionary<string, WindowControl> _windows = new(StringComparer.Ordinal);
    private DockManager? _manager;
    private IReadOnlyList<string> _ids = [];
    private Func<string, AdvancedTabItem>? _content;
    private DockTheme _theme = DockTheme.MetroLight;
    public StandardMdiHost()
    {
        Content = _canvas;
        SizeChanged += (_, _) => Refresh();
    }
    public void Update(DockManager manager, IReadOnlyList<string> ids, Func<string, AdvancedTabItem> content, DockTheme theme)
    { _manager = manager; _ids = ids; _content = content; _theme = theme; Refresh(); }
    public void ReleaseContent() { foreach (var window in _windows.Values) window.SetWindowContent(null); }
    public DockResult Arrange(MdiArrangement arrangement) => _manager is null ? new(false, 0, "No manager.") : WindowGeometry.ArrangeMdi(_manager, new(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight)), arrangement);
    private void Refresh()
    {
        if (_manager is null || _content is null) return;
        var layout = _manager.Layout;
        var index = DockLayoutIndex.For(layout);
        var ids = _ids.ToHashSet(StringComparer.Ordinal);
        _canvas.Background = DockChrome.Brush(_theme.InactiveTab);
        foreach (var id in _windows.Keys.Where(id => !ids.Contains(id)).ToArray())
        { _windows[id].SetWindowContent(null); _canvas.Children.Remove(_windows[id]); _windows.Remove(id); }
        var viewport = new DockRect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
        int minimized = 0;
        foreach (var id in _ids)
        {
            var model = layout.Windows[id];
            if (!_windows.TryGetValue(id, out var window))
            {
                window = new() { WindowId = id, ShowMinimize = true };
                string key = id;
                window.Activated += () => _manager.Activate(key);
                window.BoundsChanged += rect => _manager.SetMdiBounds(key, rect);
                window.CloseRequested += () => _manager.Close(key);
                window.MaximizeRequested += () => _manager.SetMdiState(key, _manager.Layout.Windows[key].MdiState == MdiWindowState.Maximized ? MdiWindowState.Normal : MdiWindowState.Maximized);
                window.MinimizeRequested += () => _manager.SetMdiState(key, MdiWindowState.Minimized);
                _windows.Add(id, window); _canvas.Children.Add(window);
            }
            window.MinimumSize = model.MinimumSize with { Height = model.MinimumSize.Height + _theme.TitleHeight };
            window.MaximumSize = model.MaximumSize;
            window.CanResize = _manager.Allows(id, DockCapabilities.Resize) && !layout.IsLayoutLocked;
            window.CanMove = _manager.Allows(id, DockCapabilities.Drag) && !layout.IsLayoutLocked;
            window.CanClose = _manager.Allows(id, DockCapabilities.Close);
            window.ShowMaximize = window.CanResize; window.ShowMinimize = window.CanResize;
            window.IsMaximized = model.MdiState == MdiWindowState.Maximized;
            var bounds = model.MdiState switch
            {
                MdiWindowState.Maximized => viewport,
                MdiWindowState.Minimized => new DockRect((minimized++ * 170) % Math.Max(170, viewport.Width), Math.Max(0, viewport.Height - _theme.TitleHeight - 2), 168, _theme.TitleHeight + 2),
                _ => WindowGeometry.KeepVisible(model.MdiBounds, viewport)
            };
            window.SetBounds(bounds);
            int rank = index.ActivationRank(id);
            Canvas.SetZIndex(window, rank < 0 ? 0 : layout.ActivationHistory.Length - rank + 1);
            window.UpdateChrome(model.Title + (model.IsModified ? " *" : ""), _theme, layout.ActiveWindowId == id);
            if (model.MdiState == MdiWindowState.Minimized) window.SetWindowContent(null);
            else { var content = _content(id); content.EnsureContent(); window.SetWindowContent(content); }
        }
    }
}
