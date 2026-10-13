using DockingScene.Core;
using DockingScene.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace DockingScene.Controls;

public sealed partial class StandardMdiHost
{
    private const double ScrollBarExtent = 16;
    private Grid? _scrollRoot;
    private ScrollBar? _horizontalScrollBar, _verticalScrollBar;
    private Border? _scrollCorner;
    private DockingScene.Themes.DockTheme? _scrollBarTheme;
    private bool _settingScrollBars;
    private DockRect[] _scrollInputs = [];
    private int _scrollInputCount = -1;
    private DockSize _scrollClient;
    private MdiScrollLayout _scrollBase;
    private double _viewportWorldX, _viewportWorldY;
    private string? _scrollActiveId;
    private MdiWindowState? _scrollActiveState;
    public string AutomationIdPrefix { get; set; } = DockTree.NewId("mdi");
    public MdiScrollLayout ScrollLayout { get; private set; }
    public long ScrollGeometryBuildCount { get; private set; }
    public long ScrollGeometryReuseCount { get; private set; }
    public static readonly DependencyProperty IsScrollingEnabledProperty = DependencyProperty.Register(
        nameof(IsScrollingEnabled), typeof(bool), typeof(StandardMdiHost), new PropertyMetadata(false, ScrollingChanged));
    public bool IsScrollingEnabled { get => (bool)GetValue(IsScrollingEnabledProperty); set => SetValue(IsScrollingEnabledProperty, value); }
    private static void ScrollingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (StandardMdiHost)d;
        host.CancelMagnetismGestures(); host.ResetScrollState(); host.AttachScrollSurface(); host.Refresh();
    }
    private void ResetScrollState()
    {
        _viewportWorldX = _viewportWorldY = 0; _scrollActiveId = null; _scrollActiveState = null;
        _scrollInputCount = -1; _scrollClient = default; _scrollBase = default; ScrollLayout = default;
        if (_horizontalScrollBar is not null)
        {
            _settingScrollBars = true;
            try { _horizontalScrollBar.Value = 0; _verticalScrollBar!.Value = 0; }
            finally { _settingScrollBars = false; }
        }
    }
    private void AttachScrollSurface()
    {
        if (!IsScrollingEnabled)
        {
            if (_scrollRoot is not null && ReferenceEquals(Content, _scrollRoot))
            { Content = null; _scrollRoot.Children.Remove(_canvas); Content = _canvas; }
            return;
        }
        if (_scrollRoot is null)
        {
            _scrollRoot = new Grid();
            _scrollRoot.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
            _scrollRoot.RowDefinitions.Add(new() { Height = new(0) });
            _scrollRoot.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            _scrollRoot.ColumnDefinitions.Add(new() { Width = new(0) });
            _horizontalScrollBar = new() { Orientation = Orientation.Horizontal, Minimum = 0, IndicatorMode = ScrollingIndicatorMode.MouseIndicator, SmallChange = 32, IsTabStop = true, MinHeight = 0 };
            _verticalScrollBar = new() { Orientation = Orientation.Vertical, Minimum = 0, IndicatorMode = ScrollingIndicatorMode.MouseIndicator, SmallChange = 32, IsTabStop = true, MinWidth = 0 };
            _scrollCorner = new Border();
            Grid.SetRow(_horizontalScrollBar, 1); Grid.SetColumn(_verticalScrollBar, 1);
            Grid.SetRow(_scrollCorner, 1); Grid.SetColumn(_scrollCorner, 1);
            _scrollRoot.Children.Add(_horizontalScrollBar); _scrollRoot.Children.Add(_verticalScrollBar); _scrollRoot.Children.Add(_scrollCorner);
            _horizontalScrollBar.ValueChanged += (_, _) => ScrollBarsChanged();
            _verticalScrollBar.ValueChanged += (_, _) => ScrollBarsChanged();
            _scrollRoot.PointerWheelChanged += (_, e) =>
            {
                if (e.Handled || !IsScrollingEnabled || !_active || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)) return;
                var properties = e.GetCurrentPoint(_scrollRoot).Properties;
                bool horizontal = properties.IsHorizontalMouseWheel || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift) || ScrollLayout.ScrollableHeight == 0;
                double delta = -properties.MouseWheelDelta;
                if (horizontal && ScrollLayout.ScrollableWidth > 0)
                { ScrollTo(ScrollLayout.HorizontalOffset + delta, ScrollLayout.VerticalOffset); e.Handled = true; }
                else if (!horizontal && ScrollLayout.ScrollableHeight > 0)
                { ScrollTo(ScrollLayout.HorizontalOffset, ScrollLayout.VerticalOffset + delta); e.Handled = true; }
            };
        }
        if (!ReferenceEquals(Content, _scrollRoot))
        { Content = null; _scrollRoot.Children.Add(_canvas); Content = _scrollRoot; }
    }
    private void ScrollBarsChanged()
    {
        if (!_settingScrollBars && IsScrollingEnabled && _active)
            ScrollTo(_horizontalScrollBar!.Value, _verticalScrollBar!.Value);
    }
    /// <summary>Set nonnegative extent offsets without altering layout history.
    /// Both pointer and keyboard geometry sessions are cancelled before scrolling.</summary>
    public void ScrollTo(double horizontalOffset, double verticalOffset)
    {
        if (!double.IsFinite(horizontalOffset) || !double.IsFinite(verticalOffset)) throw new ArgumentException("Scroll offsets must be finite.");
        if (!IsScrollingEnabled || !_active || _scrollInputCount < 0) return;
        var next = ScrollLayout.WithOffsets(horizontalOffset, verticalOffset);
        if (next.Viewport == ScrollLayout.Viewport) return;
        CancelMagnetismGestures();
        _viewportWorldX = next.Viewport.X; _viewportWorldY = next.Viewport.Y;
        Refresh();
    }
    /// <summary>Reveal a hosted normal/minimized frame. Does not activate it,
    /// materialize another editor or alter its stored world-space bounds.</summary>
    public bool ScrollWindowIntoView(string id)
    {
        if (!IsScrollingEnabled || !_active || _manager is null || !_windows.ContainsKey(id) ||
            !_manager.Layout.Windows.TryGetValue(id, out var window) || Array.IndexOf(_ids, id) < 0 ||
            DockTree.Locate(_manager.Layout, id).Placement == WindowPlacement.Hidden) return false;
        if (window.MdiState == MdiWindowState.Maximized) return true;
        var target = window.MdiState == MdiWindowState.Minimized && MinimizedLayout?.Windows.TryGetValue(id, out var minimized) == true
            ? minimized : window.MdiBounds;
        var next = ScrollLayout.Reveal(target);
        ScrollTo(next.HorizontalOffset, next.VerticalOffset); return true;
    }
    private DockRect PrepareScrollProjection(DockLayout layout, string[] ordered, string[] minimizedIds)
    {
        var client = new DockSize(Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
        var full = new DockRect(0, 0, client.Width, client.Height);
        if (!IsScrollingEnabled)
        {
            MinimizedLayout = _minimizedCache.Arrange(minimizedIds, full, new(168, _theme.TitleHeight + 2));
            _canvas.Clip = new RectangleGeometry { Rect = new(0, 0, client.Width, client.Height) };
            return full;
        }
        AttachScrollSurface();
        int normalCount = 0;
        foreach (var id in ordered) if (layout.Windows[id].MdiState == MdiWindowState.Normal) normalCount++;
        int count = normalCount + minimizedIds.Length;
        bool changed = _scrollInputCount != count || _scrollClient != client;
        if (_scrollInputs.Length < count) { _scrollInputs = new DockRect[count]; changed = true; }
        int at = 0;
        foreach (var id in ordered)
            if (layout.Windows[id].MdiState == MdiWindowState.Normal)
            { var rect = layout.Windows[id].MdiBounds; changed |= _scrollInputs[at] != rect; _scrollInputs[at++] = rect; }
        var area = _scrollClient == client && _scrollInputCount >= 0
            ? new DockRect(0, 0, _scrollBase.Viewport.Width, _scrollBase.Viewport.Height) : full;
        MinimizedLayout = _minimizedCache.Arrange(minimizedIds, area, new(168, _theme.TitleHeight + 2));
        foreach (var id in minimizedIds)
        { var rect = MinimizedLayout.Windows[id]; changed |= _scrollInputs[at] != rect; _scrollInputs[at++] = rect; }
        if (changed)
        {
            // Rewrap minimized rows if newly visible bars changed the viewport.
            // They participate in extents, so captions above y=0 remain reachable.
            for (int pass = 0; pass < 3; pass++)
            {
                _scrollBase = MdiScrollLayout.Measure(_scrollInputs.AsSpan(0, count), client, scrollBarSize: ScrollBarExtent);
                var nextArea = new DockRect(0, 0, _scrollBase.Viewport.Width, _scrollBase.Viewport.Height);
                if (minimizedIds.Length == 0 || nextArea == area) break;
                area = nextArea; MinimizedLayout = _minimizedCache.Arrange(minimizedIds, area, new(168, _theme.TitleHeight + 2));
                at = normalCount; foreach (var id in minimizedIds) _scrollInputs[at++] = MinimizedLayout.Windows[id];
            }
            _scrollInputCount = count; _scrollClient = client; ScrollGeometryBuildCount++;
        }
        else ScrollGeometryReuseCount++;
        ScrollLayout = _scrollBase.WithOffsets(_viewportWorldX - _scrollBase.ContentBounds.X, _viewportWorldY - _scrollBase.ContentBounds.Y);
        var activeState = layout.ActiveWindowId is { } activeId && layout.Windows.TryGetValue(activeId, out var activeModel) ? (MdiWindowState?)activeModel.MdiState : null;
        if (_scrollActiveId != layout.ActiveWindowId || _scrollActiveState != activeState)
        {
            _scrollActiveId = layout.ActiveWindowId; _scrollActiveState = activeState;
            if (_scrollActiveId is { } id && Array.IndexOf(ordered, id) >= 0)
            {
                var active = layout.Windows[id];
                if (active.MdiState != MdiWindowState.Maximized)
                    ScrollLayout = ScrollLayout.Reveal(active.MdiState == MdiWindowState.Minimized ? MinimizedLayout.Windows[id] : active.MdiBounds);
            }
        }
        _viewportWorldX = ScrollLayout.Viewport.X; _viewportWorldY = ScrollLayout.Viewport.Y;
        PaintScrollBars();
        var viewport = new DockRect(0, 0, ScrollLayout.Viewport.Width, ScrollLayout.Viewport.Height);
        _canvas.Clip = new RectangleGeometry { Rect = new(0, 0, viewport.Width, viewport.Height) };
        return viewport;
    }
    private void PaintScrollBars()
    {
        var geometry = ScrollLayout;
        _settingScrollBars = true;
        try
        {
            var h = _horizontalScrollBar!; var v = _verticalScrollBar!;
            DockChrome.Identify(h, "mdi-scroll:" + AutomationIdPrefix + ":horizontal", "Scroll MDI horizontally");
            DockChrome.Identify(v, "mdi-scroll:" + AutomationIdPrefix + ":vertical", "Scroll MDI vertically");
            DockChrome.Identify(_canvas, "mdi-scroll:" + AutomationIdPrefix + ":viewport", "MDI viewport");
            h.Visibility = geometry.HasHorizontalScrollBar ? Visibility.Visible : Visibility.Collapsed;
            v.Visibility = geometry.HasVerticalScrollBar ? Visibility.Visible : Visibility.Collapsed;
            _scrollRoot!.RowDefinitions[1].Height = new(geometry.HasHorizontalScrollBar ? Math.Min(ScrollBarExtent, Math.Max(0, ActualHeight - 1)) : 0);
            _scrollRoot.ColumnDefinitions[1].Width = new(geometry.HasVerticalScrollBar ? Math.Min(ScrollBarExtent, Math.Max(0, ActualWidth - 1)) : 0);
            h.Maximum = geometry.ScrollableWidth; v.Maximum = geometry.ScrollableHeight;
            h.ViewportSize = geometry.Viewport.Width; v.ViewportSize = geometry.Viewport.Height;
            h.LargeChange = Math.Max(1, geometry.Viewport.Width * .9); v.LargeChange = Math.Max(1, geometry.Viewport.Height * .9);
            h.Value = geometry.HorizontalOffset; v.Value = geometry.VerticalOffset;
            if (_scrollBarTheme != _theme)
            { _scrollBarTheme = _theme; _scrollCorner!.Background = DockChrome.Brush(_theme.Background); }
        }
        finally { _settingScrollBars = false; }
    }
}
