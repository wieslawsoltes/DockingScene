using System.Collections.ObjectModel;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;

namespace DockingScene.Controls;

/// <summary>Standalone retained-content tab control with measured, virtualized headers and four placements.</summary>
public class AdvancedTabControl : UserControl
{
    private readonly Grid _root = new();
    private readonly Grid _header = new();
    private readonly Canvas _strip = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled,
        HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top
    };
    private readonly ContentPresenter _presenter = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Dictionary<string, Border> _headers = new(StringComparer.Ordinal);
    private readonly HashSet<AdvancedTabItem> _observed = [];
    private TabStripMetrics _metrics = new([]);
    private bool _batch, _refreshQueued;
    private string? _selectedId;
    private string _signature = "", _layoutSignature = "";
    public ObservableCollection<AdvancedTabItem> Items { get; } = [];
    public DockTheme Theme { get; private set; } = DockTheme.MetroLight;
    public TabPlacement Placement { get; private set; }
    public bool IsGroupActive { get; private set; }
    public bool ShowNewTabButton { get; set; }
    public bool IsToolGroup { get; set; }
    public string? SelectedId => _selectedId;
    public int RealizedHeaderCount => _headers.Count;
    public event Action<string>? Activated;
    public event Action<string>? CloseRequested;
    public event EventHandler<TabPointerEventArgs>? TabPointerPressed;
    public event Action<string, FrameworkElement>? ContextRequested;
    public event Action<string>? TabDoubleTapped;
    public event Action? NewTabRequested;

    public AdvancedTabControl()
    {
        Content = _root; _root.Children.Add(_header); _root.Children.Add(_presenter);
        _header.Children.Add(_scroll); _header.Children.Add(_actions); _scroll.Content = _strip;
        Items.CollectionChanged += (_, _) => { if (!_batch) { ObserveItems(); Refresh(); Select(Items.FirstOrDefault(i => i.Id == _selectedId)?.Id ?? Items.FirstOrDefault()?.Id); } };
        _scroll.ViewChanged += (_, _) => RealizeHeaders(); SizeChanged += (_, _) => RealizeHeaders();
        Loaded += (_, _) => { ObserveItems(); Refresh(); Select(_selectedId ?? Items.FirstOrDefault()?.Id); };
        _scroll.PointerWheelChanged += (_, e) =>
        {
            if (Placement is TabPlacement.Top or TabPlacement.Bottom)
            { _scroll.ChangeView(Math.Max(0, _scroll.HorizontalOffset - e.GetCurrentPoint(_scroll).Properties.MouseWheelDelta), null, null, true); e.Handled = true; }
        };
        // Navigation belongs only to the header. Editor caret keys must never switch document tabs.
        _header.AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
            if (e.Handled || Items.Count == 0 || !(e.Key is VirtualKey.Home or VirtualKey.End || (vertical ? e.Key is VirtualKey.Up or VirtualKey.Down : e.Key is VirtualKey.Left or VirtualKey.Right))) return;
            int index = Math.Max(0, Items.ToList().FindIndex(i => i.Id == _selectedId));
            int direction = e.Key is VirtualKey.Left or VirtualKey.Up ? -1 : 1;
            index = e.Key == VirtualKey.Home ? 0 : e.Key == VirtualKey.End ? Items.Count - 1 : (index + direction + Items.Count) % Items.Count;
            Select(Items[index].Id, true);
            if (_headers.TryGetValue(Items[index].Id, out var border)) ((Grid)border.Child).Children.OfType<Button>().First().Focus(FocusState.Keyboard);
            e.Handled = true;
        }), false);
    }
    private void ObserveItems()
    {
        foreach (var item in _observed.Where(item => !Items.Contains(item)).ToArray()) { item.MetadataChanged -= QueueRefresh; _observed.Remove(item); }
        foreach (var item in Items) if (_observed.Add(item)) item.MetadataChanged += QueueRefresh;
    }
    private void QueueRefresh()
    {
        if (_batch || _refreshQueued) return;
        _refreshQueued = true;
        if (!DispatcherQueue.TryEnqueue(() => { _refreshQueued = false; Refresh(); })) _refreshQueued = false;
    }
    public void Update(IReadOnlyList<AdvancedTabItem> items, string? selectedId, DockTheme theme, TabPlacement placement, bool active)
    {
        _batch = true;
        // Identity includes the actual content control, not just its key. Replacing an item must release the previous visual.
        if (!Items.SequenceEqual(items)) { Items.Clear(); foreach (var item in items) Items.Add(item); }
        _batch = false; ObserveItems();
        Theme = theme; Placement = placement; IsGroupActive = active; _selectedId = selectedId;
        Refresh(); Select(selectedId);
    }
    public void Select(string? id, bool notify = false)
    {
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (id is not null && item is null) throw new ArgumentException("Unknown tab id.", nameof(id));
        _selectedId = id; item?.EnsureContent(); ContentLease.Set(_presenter, item);
        RefreshHeaderColors(); BringSelectedIntoView();
        if (notify && id is not null) Activated?.Invoke(id);
    }
    public void ReleaseContent() => ContentLease.Set(_presenter, null);
    /// <summary>Hit-tests only the actual scrolling header viewport, never the document content or overflow buttons.</summary>
    public bool TryGetInsertionIndex(Point point, out int index)
    {
        index = -1;
        if (_scroll.ActualWidth <= 0 || _scroll.ActualHeight <= 0) return false;
        var local = TransformToVisual(_scroll).TransformPoint(point);
        if (local.X < 0 || local.Y < 0 || local.X >= _scroll.ActualWidth || local.Y >= _scroll.ActualHeight) return false;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        index = _metrics.InsertionIndex(vertical ? local.Y + _scroll.VerticalOffset : local.X + _scroll.HorizontalOffset); return true;
    }
    private void Refresh()
    {
        if (Items.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != Items.Count) throw new InvalidOperationException("Tab ids must be unique.");
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        _root.Background = DockChrome.Brush(Theme.Surface); _header.Background = DockChrome.Brush(Theme.InactiveTab);
        string layoutSignature = $"{Placement}:{Theme.TabHeight}";
        if (_layoutSignature != layoutSignature)
        {
            _layoutSignature = layoutSignature;
            _root.RowDefinitions.Clear(); _root.ColumnDefinitions.Clear(); _header.RowDefinitions.Clear(); _header.ColumnDefinitions.Clear();
            Grid.SetRow(_header, 0); Grid.SetColumn(_header, 0); Grid.SetRow(_presenter, 0); Grid.SetColumn(_presenter, 0);
            Grid.SetRow(_scroll, 0); Grid.SetColumn(_scroll, 0); Grid.SetRow(_actions, 0); Grid.SetColumn(_actions, 0);
            if (vertical)
            {
                _root.ColumnDefinitions.Add(new() { Width = Placement == TabPlacement.Left ? new(155) : new(1, GridUnitType.Star) });
                _root.ColumnDefinitions.Add(new() { Width = Placement == TabPlacement.Right ? new(155) : new(1, GridUnitType.Star) });
                Grid.SetColumn(_header, Placement == TabPlacement.Left ? 0 : 1); Grid.SetColumn(_presenter, Placement == TabPlacement.Left ? 1 : 0);
                _header.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); _header.RowDefinitions.Add(new() { Height = GridLength.Auto }); Grid.SetRow(_actions, 1);
            }
            else
            {
                _root.RowDefinitions.Add(new() { Height = Placement == TabPlacement.Top ? new(Theme.TabHeight) : new(1, GridUnitType.Star) });
                _root.RowDefinitions.Add(new() { Height = Placement == TabPlacement.Bottom ? new(Theme.TabHeight) : new(1, GridUnitType.Star) });
                Grid.SetRow(_header, Placement == TabPlacement.Top ? 0 : 1); Grid.SetRow(_presenter, Placement == TabPlacement.Top ? 1 : 0);
                _header.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _header.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); Grid.SetColumn(_actions, 1);
            }
            _scroll.HorizontalScrollMode = vertical ? ScrollMode.Disabled : ScrollMode.Enabled;
            _scroll.VerticalScrollMode = vertical ? ScrollMode.Enabled : ScrollMode.Disabled;
            _scroll.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden;
            _scroll.VerticalScrollBarVisibility = vertical ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        }
        string signature = Theme + ":" + Placement + ShowNewTabButton + string.Join('|', Items.Select(i => $"{i.Id.Length}:{i.Id}:{i.Header.Length}:{i.Header}:{i.IsPinned}:{i.IsPreview}:{i.IsModified}:{i.IsReadOnly}:{i.CanClose}:{i.TabColor}"));
        if (signature != _signature)
        {
            _signature = signature; _headers.Clear(); _strip.Children.Clear();
            _metrics = new(Items.Select(item =>
            {
                if (vertical) return Theme.TabHeight;
                var text = DockChrome.Text(item.Header + (item.IsModified ? " *" : ""), Theme);
                if (item.IsPreview) text.FontStyle = Windows.UI.Text.FontStyle.Italic;
                text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return Math.Clamp(text.DesiredSize.Width + 16 + (item.CanClose ? 23 : 0) + (item.IsPinned ? 17 : 0) + (item.IsReadOnly ? 16 : 0) + (item.IsPreview ? 23 : 0), 60, 300);
            }));
            _strip.Width = vertical ? 155 : _metrics.Length; _strip.Height = vertical ? _metrics.Length : Theme.TabHeight;
            _actions.Children.Clear();
            var more = DockChrome.Button("Window list", "tabs:overflow", Theme, ShowOverflow, DockGlyphs.Chevron); more.Width = 23; _actions.Children.Add(more);
            if (ShowNewTabButton) { var add = DockChrome.Button("New document", "tabs:new", Theme, () => NewTabRequested?.Invoke(), DockGlyphs.Add); add.Width = 24; _actions.Children.Add(add); }
        }
        RealizeHeaders();
    }
    private void RealizeHeaders()
    {
        if (_metrics.Count != Items.Count) return;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        double offset = vertical ? _scroll.VerticalOffset : _scroll.HorizontalOffset;
        double viewport = vertical ? _scroll.ViewportHeight : _scroll.ViewportWidth;
        if (viewport <= 0) viewport = vertical ? ActualHeight : ActualWidth;
        if (viewport <= 0) viewport = 800;
        var range = _metrics.VisibleRange(offset, viewport, 250);
        var visible = Items.Skip(range.First).Take(range.Count).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _headers.Keys.Where(id => !visible.Contains(id)).ToArray()) { _strip.Children.Remove(_headers[id]); _headers.Remove(id); }
        for (int i = range.First; i < range.First + range.Count; i++)
        {
            var item = Items[i];
            if (!_headers.TryGetValue(item.Id, out var header)) { header = CreateHeader(item); _headers.Add(item.Id, header); _strip.Children.Add(header); }
            DockChrome.Position(header, vertical ? new(0, _metrics.Offsets[i], 155, Theme.TabHeight) : new(_metrics.Offsets[i], 0, _metrics.Offsets[i + 1] - _metrics.Offsets[i], Theme.TabHeight));
        }
        RefreshHeaderColors();
    }
    private Border CreateHeader(AdvancedTabItem item)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var text = DockChrome.Text(item.Header + (item.IsModified ? " *" : ""), Theme);
        if (item.IsPreview) text.FontStyle = Windows.UI.Text.FontStyle.Italic;
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        if (item.IsPinned) label.Children.Add(DockChrome.Glyph(DockGlyphs.Pin, Theme.Text, 12));
        if (item.IsReadOnly) label.Children.Add(DockChrome.Glyph(DockGlyphs.Lock, Theme.MutedText, 11));
        label.Children.Add(text);
        var select = DockChrome.Button(item.Header, "tab:" + item.Id, Theme, () => Select(item.Id, true)); select.Content = label;
        select.HorizontalAlignment = HorizontalAlignment.Stretch; select.HorizontalContentAlignment = HorizontalAlignment.Left; select.Height = Theme.TabHeight;
        select.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (e.GetCurrentPoint(select).Properties.IsMiddleButtonPressed) { if (item.CanClose) CloseRequested?.Invoke(item.Id); e.Handled = true; return; }
            if (e.GetCurrentPoint(select).Properties.IsLeftButtonPressed) { Select(item.Id, true); TabPointerPressed?.Invoke(this, new(item.Id, select, e)); }
        }), true);
        select.RightTapped += (_, e) => { ContextRequested?.Invoke(item.Id, select); e.Handled = true; };
        select.DoubleTapped += (_, e) => { TabDoubleTapped?.Invoke(item.Id); e.Handled = true; };
        grid.Children.Add(select);
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(actions, 1); grid.Children.Add(actions);
        if (item.IsPreview)
        {
            var keep = DockChrome.Button("Keep " + item.Header + " open", "keep-open:" + item.Id, Theme, () => TabDoubleTapped?.Invoke(item.Id), DockGlyphs.Pin);
            keep.Width = 23; keep.Padding = new(4); keep.Height = Theme.TabHeight; actions.Children.Add(keep);
        }
        if (item.CanClose)
        {
            var close = DockChrome.Button("Close " + item.Header, "close:" + item.Id, Theme, () => CloseRequested?.Invoke(item.Id), DockGlyphs.Close);
            close.Width = 23; close.Padding = new(4); close.Height = Theme.TabHeight; actions.Children.Add(close);
        }
        return new Border { Child = grid, BorderThickness = new(0, 0, 1, 0), BorderBrush = DockChrome.Brush(Theme.Border) };
    }
    private void RefreshHeaderColors()
    {
        var items = Items.ToDictionary(i => i.Id, StringComparer.Ordinal);
        foreach (var (id, border) in _headers)
        {
            if (!items.TryGetValue(id, out var item)) continue;
            bool selected = id == _selectedId;
            string color = selected ? IsGroupActive && !IsToolGroup ? Theme.Accent : Theme.Surface : Theme.InactiveTab;
            if (!selected && item.TabColor is { Length: 7 or 9 } custom && custom[0] == '#' && custom.Skip(1).All(Uri.IsHexDigit)) color = custom;
            string foreground = selected && IsGroupActive && !IsToolGroup ? Theme.AccentText : selected && IsToolGroup ? Theme.Accent : Theme.Text;
            border.Background = DockChrome.Brush(color);
            var stack = new Stack<DependencyObject>(); stack.Push(border.Child);
            while (stack.TryPop(out var node))
            {
                if (node is Button button) { button.Background = DockChrome.Brush(color); button.Foreground = DockChrome.Brush(foreground); }
                if (node is TextBlock text) text.Foreground = DockChrome.Brush(foreground);
                if (node is Microsoft.UI.Xaml.Shapes.Path path) path.Stroke = DockChrome.Brush(foreground);
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
            }
        }
    }
    private void BringSelectedIntoView()
    {
        int index = Items.ToList().FindIndex(i => i.Id == _selectedId);
        if (index < 0 || _metrics.Count != Items.Count) return;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        double offset = vertical ? _scroll.VerticalOffset : _scroll.HorizontalOffset, viewport = vertical ? _scroll.ViewportHeight : _scroll.ViewportWidth;
        if (viewport <= 0) return;
        double next = _metrics.BringIntoView(index, offset, viewport);
        if (Math.Abs(next - offset) > .5) _scroll.ChangeView(vertical ? null : next, vertical ? next : null, null, true);
    }
    private void ShowOverflow()
    {
        var menu = new MenuFlyout();
        foreach (var item in Items)
        {
            var option = DockChrome.Identify(new MenuFlyoutItem { Text = (item.Id == _selectedId ? "✓  " : "    ") + item.Header, MinHeight = 24, FontSize = Theme.FontSize, Padding = new(8, 3, 8, 3) }, "tab-list:" + item.Id);
            string id = item.Id; option.Click += (_, _) => Select(id, true); menu.Items.Add(option);
        }
        menu.ShowAt(_actions);
    }
}
