using System.Collections.Immutable;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace DockingScene.Controls;

/// <summary>Retained Uno visual projection of a DockManager. The entire visual tree uses Uno's selected renderer.</summary>
public partial class DockSite : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Grid _root = new();
    private readonly Dictionary<string, DockContainer> _groups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WindowControl> _floats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DockingWindow> _windows = new(StringComparer.Ordinal);
    private readonly List<UIElement> _auxiliary = [];
    private StandardMdiHost? _mdi;
    private DockContainer? _autoPopup;
    private string? _autoOpen;
    private DockRect _autoBounds;
    private bool _queued;
    private bool _rendering;
    private bool _subscribed;
    private DockGeometry? _geometry;
    private readonly DispatcherTimer _hoverOpen = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _hoverClose = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private string? _hoverId;
    public static readonly DependencyProperty ManagerProperty = DependencyProperty.Register(nameof(Manager), typeof(DockManager), typeof(DockSite), new PropertyMetadata(null, ManagerChanged));
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(nameof(Theme), typeof(DockTheme), typeof(DockSite), new PropertyMetadata(DockTheme.MetroLight, (d, _) => ((DockSite)d).QueueRender()));
    public DockManager Manager { get => (DockManager)GetValue(ManagerProperty); set => SetValue(ManagerProperty, value ?? throw new ArgumentNullException(nameof(value))); }
    public DockTheme Theme { get => (DockTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    public DockStrings Strings { get; set; } = new();
    public Func<DockWindow, object?>? ContentFactory { get; set; }
    public DataTemplate? WindowContentTemplate { get; set; }
    public DataTemplateSelector? WindowContentTemplateSelector { get; set; }
    public object? WorkspaceContent { get; set; }
    public bool OpenAutoHideOnHover { get; set; } = true;
    public bool ReduceMotion { get; set; }
    public DockGeometry? Geometry => _geometry;
    public int CachedContentCount => _windows.Values.Count(w => w.ContentFactory is null);
    public event Action<string>? OperationRejected;
    public event Action? NewDocumentRequested;
    public event EventHandler<DockChangedEventArgs>? LayoutChanged;
    public event Action? VisualsUpdated;

    public DockSite()
    {
        Content = _root; _root.Children.Add(_canvas); _root.Children.Add(_overlay);
        Manager = new DockManager();
        SizeChanged += (_, _) => QueueRender();
        Loaded += (_, _) => { Subscribe(); QueueRender(); };
        Unloaded += (_, _) => { Unsubscribe(); _hoverOpen.Stop(); _hoverClose.Stop(); CancelInteraction(); };
        _hoverOpen.Tick += (_, _) => { _hoverOpen.Stop(); if (_hoverId is not null) OpenAutoHide(_hoverId); };
        _hoverClose.Tick += (_, _) => { _hoverClose.Stop(); CloseAutoHide(); };
        _canvas.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (_autoOpen is null || _geometry is null) return;
            var p = e.GetCurrentPoint(_canvas).Position;
            if (!_autoBounds.Contains(p.X, p.Y) && !_geometry.AutoHideRails.Values.Any(r => r.Contains(p.X, p.Y))) CloseAutoHide();
        }), true);
        ConfigureInput();
    }
    private static void ManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var site = (DockSite)d;
        if (e.OldValue is DockManager old) old.Changed -= site.ModelChanged;
        site._subscribed = false;
        site.CancelInteraction();
        foreach (var group in site._groups.Values) group.Tabs.ReleaseContent();
        site._mdi?.ReleaseContent();
        site._windows.Clear();
        site.Subscribe(); site.QueueRender();
    }
    private void Subscribe() { if (!_subscribed && GetValue(ManagerProperty) is DockManager manager) { manager.Changed += ModelChanged; _subscribed = true; } }
    private void Unsubscribe() { if (_subscribed) { Manager.Changed -= ModelChanged; _subscribed = false; } }
    private void ModelChanged(object? sender, DockChangedEventArgs e) { QueueRender(); LayoutChanged?.Invoke(this, e); }
    private void QueueRender()
    {
        if (_queued) return;
        _queued = true;
        if (!DispatcherQueue.TryEnqueue(() => { _queued = false; Render(); })) _queued = false;
    }
    public void Refresh() => QueueRender();
    private void Submit(DockResult result) { if (!result.Success) OperationRejected?.Invoke(result.Error ?? "Operation rejected."); }
    private DockingWindow Window(string id)
    {
        var model = Manager.Layout.Windows[id];
        if (!_windows.TryGetValue(id, out var window))
        {
            window = model.Kind == WindowKind.Document ? new DocumentWindow() : new ToolWindow();
            window.HorizontalContentAlignment = HorizontalAlignment.Stretch; window.VerticalContentAlignment = VerticalAlignment.Stretch;
            window.ContentTemplate = WindowContentTemplate; window.ContentTemplateSelector = WindowContentTemplateSelector;
            string key = id;
            window.ContentFactory = () => ContentFactory?.Invoke(Manager.Layout.Windows[key]) ?? DockChrome.Text(Manager.Layout.Windows[key].Title, Theme, 18);
            _windows[id] = window;
        }
        window.Update(model); window.Background = DockChrome.Brush(Theme.Surface); window.Foreground = DockChrome.Brush(Theme.Text);
        return window;
    }
    private DockContainer CreateContainer(bool tools, bool autoHide = false)
    {
        DockContainer view = tools ? new ToolWindowContainer() : new TabbedMdiContainer();
        view.Tabs.Activated += id => Submit(Manager.Activate(id));
        view.Tabs.CloseRequested += id => Submit(Manager.Close(id));
        view.Tabs.ContextRequested += (id, source) => ShowWindowMenu(id, source);
        view.Tabs.TabPointerPressed += (_, e) => BeginDockDrag([e.Id], e.Pointer);
        view.Tabs.TabDoubleTapped += id => { if (Manager.Layout.Windows[id].DisplayMode == TabDisplayMode.Preview) Submit(Manager.UpdateWindow(id, w => w with { DisplayMode = TabDisplayMode.Normal })); };
        view.Tabs.NewTabRequested += () => NewDocumentRequested?.Invoke();
        view.OptionsRequested += source => { if (view.Group?.SelectedId is { } id) ShowWindowMenu(id, source); };
        view.CloseRequested += () => { if (view.Group?.SelectedId is { } id) Submit(Manager.Close(id)); };
        view.PinRequested += () =>
        {
            if (view.Group?.SelectedId is not { } id) return;
            if (autoHide) { Submit(Manager.PinAutoHide(id)); _autoOpen = null; }
            else Submit(Manager.AutoHide(id, NearestEdge(view.Group.Id)));
        };
        view.HeaderPointerPressed += (_, e) => { if (view.Group is { } group) { BeginDockDrag(group.Windows, e.Pointer); e.Handled = true; } };
        view.HeaderDoubleTapped += () =>
        {
            if (view.Group is not { } group) return;
            bool floating = DockTree.Locate(Manager.Layout, group.Windows[0]).Placement == WindowPlacement.Floating;
            Submit(floating ? Manager.DockMany(group.Windows, null, DockSide.Center) : Manager.FloatMany(group.Windows));
        };
        view.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) =>
        {
            if (view.Group?.SelectedId is { } id && Manager.Layout.ActiveWindowId != id) Submit(Manager.Activate(id));
        }), true);
        return view;
    }
    private DockSide NearestEdge(string groupId)
    {
        if (_geometry is null || !_geometry.Nodes.TryGetValue(groupId, out var r)) return DockSide.Left;
        var a = _geometry.WorkspaceBounds;
        return new[] { (DockSide.Left, r.X - a.X), (DockSide.Right, a.Right - r.Right), (DockSide.Top, r.Y - a.Y), (DockSide.Bottom, a.Bottom - r.Bottom) }.OrderBy(p => p.Item2).First().Item1;
    }

    private void Render()
    {
        if (_rendering || ActualWidth <= 0 || ActualHeight <= 0 || GetValue(ManagerProperty) is not DockManager manager) return;
        _rendering = true;
        try
        {
            var layout = manager.Layout;
            _geometry = DockLayoutEngine.Arrange(layout, new(0, 0, ActualWidth, ActualHeight), new() { TabHeight = Theme.TabHeight, TitleHeight = Theme.TitleHeight, Splitter = Theme.SplitterSize, DpiScale = XamlRoot?.RasterizationScale ?? 1 });
            _canvas.Background = DockChrome.Brush(Theme.Background);
            _canvas.Clip = new RectangleGeometry { Rect = new(0, 0, ActualWidth, ActualHeight) };
            foreach (var element in _auxiliary) _canvas.Children.Remove(element); _auxiliary.Clear();
            var standardIds = layout.MdiMode == MdiMode.Standard ? DockTree.Descendants(layout.Root).OfType<DockWorkspace>().SelectMany(w => DockTree.WindowIds(w.Content)).ToHashSet(StringComparer.Ordinal) : [];
            var groups = DockTree.Groups(layout).Where(g => !g.Windows.All(standardIds.Contains)).ToArray();
            var validGroups = groups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var key in _groups.Keys.Where(k => !validGroups.Contains(k)).ToArray()) { _groups[key].Tabs.ReleaseContent(); _canvas.Children.Remove(_groups[key]); _groups.Remove(key); }
            var hostOrder = layout.FloatingHosts.OrderByDescending(h => DockTree.WindowIds(h.Root).Select(id => layout.ActivationHistory.IndexOf(id) is var i && i >= 0 ? i : int.MaxValue).DefaultIfEmpty(int.MaxValue).Min()).ToArray();
            var z = hostOrder.Select((h, i) => (h.Id, Z: 100 + i * 10)).ToDictionary(p => p.Id, p => p.Z);
            foreach (var key in _floats.Keys.Where(k => !layout.FloatingHosts.Any(h => h.Id == k)).ToArray()) { _canvas.Children.Remove(_floats[key]); _floats.Remove(key); }
            foreach (var host in hostOrder)
            {
                if (!_floats.TryGetValue(host.Id, out var window))
                {
                    window = new() { WindowId = host.Id };
                    string key = host.Id;
                    window.HeaderDragStarting += (_, e) =>
                    {
                        var current = Manager.Layout.FloatingHosts.FirstOrDefault(h => h.Id == key);
                        if (current is not null) { BeginDockDrag(DockTree.WindowIds(current.Root).ToImmutableArray(), e.Pointer); e.Handled = true; }
                    };
                    window.BoundsChanged += rect => Submit(Manager.SetFloatingBounds(key, rect));
                    window.CloseRequested += () => { var current = Manager.Layout.FloatingHosts.FirstOrDefault(h => h.Id == key); if (current is not null) Submit(Manager.CloseMany(DockTree.WindowIds(current.Root))); };
                    window.MaximizeRequested += () => { var current = Manager.Layout.FloatingHosts.FirstOrDefault(h => h.Id == key); if (current is not null) Submit(Manager.SetFloatingMaximized(key, !current.IsMaximized)); };
                    _floats[key] = window; _canvas.Children.Add(window);
                }
                var ids = DockTree.WindowIds(host.Root).ToArray();
                string selected = ids.FirstOrDefault(id => id == layout.ActiveWindowId) ?? ids[0];
                window.IsMaximized = host.IsMaximized; window.CanResize = !layout.IsLayoutLocked && ids.All(id => manager.Allows(id, DockCapabilities.Resize));
                window.SetBounds(_geometry.FloatingHosts[host.Id]); Canvas.SetZIndex(window, z[host.Id]);
                window.UpdateChrome(layout.Windows[selected].Title, Theme, ids.Contains(layout.ActiveWindowId));
            }
            foreach (var group in groups)
            {
                if (!_groups.TryGetValue(group.Id, out var view)) { view = CreateContainer(group.Kind == GroupKind.Tools); _groups.Add(group.Id, view); _canvas.Children.Add(view); }
                var host = DockTree.Locate(layout, group.Windows[0]).HostId;
                DockChrome.Position(view, _geometry.Nodes[group.Id], host is null ? 1 : z[host] + 2);
                view.Update(group, group.Windows.Select(id => (AdvancedTabItem)Window(id)).ToArray(), layout, Theme);
            }
            foreach (var split in _geometry.Splitters)
            {
                var node = DockTree.Find(layout, split.Id)!;
                if (DockTree.WindowIds(node).Any() && DockTree.WindowIds(node).All(standardIds.Contains)) continue;
                var thumb = DockChrome.Button("Resize splitter", "splitter:" + split.Id, Theme, () => { });
                thumb.Content = null; thumb.Padding = new(0); thumb.IsEnabled = !layout.IsLayoutLocked;
                thumb.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) => BeginSplitDrag(split, e)), true);
                thumb.KeyDown += (_, e) =>
                {
                    int direction = e.Key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Up ? -1 : e.Key is Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Down ? 1 : 0;
                    if (direction != 0) { Submit(Manager.ResizeSplit(split.Id, split.Ratio + direction * .025)); e.Handled = true; }
                };
                string? host = DockTree.WindowIds(node).Select(id => DockTree.Locate(layout, id).HostId).FirstOrDefault(id => id is not null);
                DockChrome.Position(thumb, split.Bounds, host is null ? 4 : z[host] + 3); AddAuxiliary(thumb);
            }
            if (layout.MdiMode == MdiMode.Standard && standardIds.Count > 0)
            {
                var workspace = DockTree.Descendants(layout.Root).OfType<DockWorkspace>().First();
                if (_mdi is null) { _mdi = new(); _canvas.Children.Add(_mdi); }
                _mdi.Visibility = Visibility.Visible; DockChrome.Position(_mdi, _geometry.Nodes[workspace.Id], 2);
                _mdi.Update(manager, standardIds.ToArray(), Window, Theme);
            }
            else if (_mdi is not null) { _mdi.ReleaseContent(); _mdi.Visibility = Visibility.Collapsed; }
            foreach (var workspace in DockTree.Descendants(layout.Root).OfType<DockWorkspace>().Where(w => w.Content is null))
            {
                var presenter = new ContentPresenter { Content = WorkspaceContent ?? DockChrome.Text("Drag documents here or create a new document.", Theme, 16), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
                DockChrome.Position(presenter, _geometry.Nodes[workspace.Id], 0); AddAuxiliary(presenter);
            }
            RenderAutoHide(layout);
            foreach (var id in _windows.Keys.Where(id => !layout.Windows.ContainsKey(id)).ToArray()) { if (_windows[id].Content is IDisposable disposable) disposable.Dispose(); _windows.Remove(id); }
            VisualsUpdated?.Invoke();
        }
        finally { _rendering = false; }
    }
    private void AddAuxiliary(UIElement element) { _auxiliary.Add(element); _canvas.Children.Add(element); }

    public void OpenAutoHide(string id) { if (!Manager.Layout.AutoHideGroups.Any(g => g.Windows.Contains(id))) return; _autoOpen = id; Submit(Manager.Activate(id)); QueueRender(); }
    public void CloseAutoHide() { if (_autoOpen is null) return; _autoOpen = null; QueueRender(); }
    private void RenderAutoHide(DockLayout layout)
    {
        if (_geometry is null) return;
        foreach (var (side, rail) in _geometry.AutoHideRails)
        {
            double offset = 4;
            foreach (var group in layout.AutoHideGroups.Where(g => g.Side == side))
            foreach (var id in group.Windows)
            {
                var model = layout.Windows[id];
                double length = Math.Clamp(model.Title.Length * Theme.FontSize * .57 + 24, 80, 190);
                var button = DockChrome.Button(model.Title, "autohide:" + id, Theme, () => OpenAutoHide(id));
                button.BorderBrush = DockChrome.Brush(Theme.Border); button.BorderThickness = new(0, 0, 0, 2);
                if (side is DockSide.Left or DockSide.Right)
                {
                    var content = new Canvas { Width = rail.Width, Height = length };
                    var text = DockChrome.Text(model.Title, Theme); text.Width = length; text.Height = rail.Width;
                    text.RenderTransform = new RotateTransform { Angle = 90 }; Canvas.SetLeft(text, rail.Width); content.Children.Add(text); button.Content = content; button.Padding = new(0);
                    DockChrome.Position(button, new(rail.X, rail.Y + offset, rail.Width, length), 2000);
                }
                else DockChrome.Position(button, new(rail.X + offset, rail.Y, length, rail.Height), 2000);
                button.PointerEntered += (_, _) => { _hoverClose.Stop(); if (OpenAutoHideOnHover) { _hoverId = id; _hoverOpen.Start(); } };
                button.PointerExited += (_, _) => { _hoverOpen.Stop(); if (_autoOpen == id) _hoverClose.Start(); };
                AddAuxiliary(button); offset += length + 5;
            }
        }
        var current = _autoOpen is null ? null : layout.AutoHideGroups.FirstOrDefault(g => g.Windows.Contains(_autoOpen));
        if (current is null)
        {
            _autoOpen = null;
            if (_autoPopup is not null) { _autoPopup.Tabs.ReleaseContent(); _canvas.Children.Remove(_autoPopup); _autoPopup = null; }
            return;
        }
        if (_autoPopup is null)
        {
            _autoPopup = CreateContainer(true, true); _canvas.Children.Add(_autoPopup);
            _autoPopup.PointerEntered += (_, _) => _hoverClose.Stop(); _autoPopup.PointerExited += (_, _) => _hoverClose.Start();
        }
        var area = _geometry.WorkspaceBounds;
        double size = layout.Windows[_autoOpen!].AutoHideSize;
        _autoBounds = current.Side switch
        {
            DockSide.Left => area with { Width = Math.Min(size, area.Width) },
            DockSide.Right => new(area.Right - Math.Min(size, area.Width), area.Y, Math.Min(size, area.Width), area.Height),
            DockSide.Top => area with { Height = Math.Min(size, area.Height) },
            _ => new(area.X, area.Bottom - Math.Min(size, area.Height), area.Width, Math.Min(size, area.Height))
        };
        var popupGroup = new DockGroup(current.Id, GroupKind.Tools, current.Windows, _autoOpen) { TabPlacement = TabPlacement.Bottom };
        _autoPopup.Update(popupGroup, current.Windows.Select(id => (AdvancedTabItem)Window(id)).ToArray(), layout, Theme, true);
        DockChrome.Position(_autoPopup, _autoBounds, 5000);
    }
}
