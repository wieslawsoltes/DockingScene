using System.Collections.ObjectModel;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DockingScene.Controls;

public class AdvancedTabItem : ContentControl
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Header { get; set; } = "Document";
    public bool IsPinned { get; set; }
    public bool IsPreview { get; set; }
    public bool IsModified { get; set; }
    public bool IsReadOnly { get; set; }
    public bool CanClose { get; set; } = true;
    public string? TabColor { get; set; }
    public Func<object?>? ContentFactory { get; set; }
    public void EnsureContent() { if (ContentFactory is { } factory) { Content = factory(); ContentFactory = null; } }
}
public class DockingWindow : AdvancedTabItem
{
    public DockWindow Model { get; private set; } = new("window", "Window", WindowKind.Document);
    public void Update(DockWindow model)
    {
        Model = model; Id = model.Id; Header = model.Title; IsPinned = model.DisplayMode == TabDisplayMode.Pinned;
        IsPreview = model.DisplayMode == TabDisplayMode.Preview; IsModified = model.IsModified;
        IsReadOnly = model.IsReadOnly; CanClose = model.Capabilities.HasFlag(DockCapabilities.Close); TabColor = model.TabColor;
    }
}
public sealed class DocumentWindow : DockingWindow { }
public sealed class ToolWindow : DockingWindow { }
public sealed class TabPointerEventArgs(string id, FrameworkElement source, PointerRoutedEventArgs pointer) : EventArgs
{
    public string Id { get; } = id;
    public FrameworkElement Source { get; } = source;
    public PointerRoutedEventArgs Pointer { get; } = pointer;
}

/// <summary>Standalone tab control with retained content, four placements, overflow and viewport-virtualized headers.</summary>
public class AdvancedTabControl : UserControl
{
    private readonly Grid _root = new();
    private readonly Grid _header = new();
    private readonly Canvas _strip = new();
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled };
    private readonly ContentPresenter _presenter = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Dictionary<string, Border> _headers = new(StringComparer.Ordinal);
    private double[] _offsets = [0];
    private bool _batch;
    private string? _selectedId;
    private string _signature = "";
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
        Content = _root;
        _root.Children.Add(_header); _root.Children.Add(_presenter);
        _header.Children.Add(_scroll); _header.Children.Add(_actions);
        _scroll.Content = _strip;
        Items.CollectionChanged += (_, _) => { if (!_batch) Refresh(); };
        _scroll.ViewChanged += (_, _) => RealizeHeaders();
        SizeChanged += (_, _) => RealizeHeaders();
        Loaded += (_, _) => { Refresh(); BringSelectedIntoView(); };
        _scroll.PointerWheelChanged += (_, e) =>
        {
            if (Placement is TabPlacement.Top or TabPlacement.Bottom)
            { _scroll.ChangeView(Math.Max(0, _scroll.HorizontalOffset - e.GetCurrentPoint(_scroll).Properties.MouseWheelDelta), null, null, true); e.Handled = true; }
        };
        AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key is not (VirtualKey.Left or VirtualKey.Right or VirtualKey.Home or VirtualKey.End) || Items.Count == 0) return;
            int index = Math.Max(0, Items.ToList().FindIndex(x => x.Id == _selectedId));
            index = e.Key == VirtualKey.Home ? 0 : e.Key == VirtualKey.End ? Items.Count - 1 : (index + (e.Key == VirtualKey.Left ? -1 : 1) + Items.Count) % Items.Count;
            Select(Items[index].Id, true); e.Handled = true;
        }), false);
    }

    public void Update(IReadOnlyList<AdvancedTabItem> items, string? selectedId, DockTheme theme, TabPlacement placement, bool active)
    {
        _batch = true;
        if (!Items.Select(i => i.Id).SequenceEqual(items.Select(i => i.Id))) { Items.Clear(); foreach (var item in items) Items.Add(item); }
        _batch = false;
        Theme = theme; Placement = placement; IsGroupActive = active;
        _selectedId = selectedId;
        Refresh(); Select(selectedId, false);
    }

    public void Select(string? id, bool notify = false)
    {
        _selectedId = id;
        var item = Items.FirstOrDefault(i => i.Id == id);
        item?.EnsureContent();
        ContentLease.Set(_presenter, item);
        RefreshHeaderColors();
        BringSelectedIntoView();
        if (notify && id is not null) Activated?.Invoke(id);
    }
    public void ReleaseContent() => ContentLease.Set(_presenter, null);

    private void Refresh()
    {
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        _root.Background = DockChrome.Brush(Theme.Surface);
        _header.Background = DockChrome.Brush(Theme.InactiveTab);
        _root.RowDefinitions.Clear(); _root.ColumnDefinitions.Clear();
        _header.RowDefinitions.Clear(); _header.ColumnDefinitions.Clear();
        Grid.SetRow(_header, 0); Grid.SetColumn(_header, 0); Grid.SetRow(_presenter, 0); Grid.SetColumn(_presenter, 0);
        Grid.SetRow(_scroll, 0); Grid.SetColumn(_scroll, 0); Grid.SetRow(_actions, 0); Grid.SetColumn(_actions, 0);
        if (vertical)
        {
            _root.ColumnDefinitions.Add(new() { Width = Placement == TabPlacement.Left ? new(155) : new(1, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new() { Width = Placement == TabPlacement.Right ? new(155) : new(1, GridUnitType.Star) });
            Grid.SetColumn(_header, Placement == TabPlacement.Left ? 0 : 1); Grid.SetColumn(_presenter, Placement == TabPlacement.Left ? 1 : 0);
            _header.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); _header.RowDefinitions.Add(new() { Height = GridLength.Auto });
            Grid.SetRow(_actions, 1);
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
        string signature = Theme.Name + Placement + ShowNewTabButton + string.Join('|', Items.Select(i => $"{i.Id}:{i.Header}:{i.IsPinned}:{i.IsPreview}:{i.IsModified}:{i.IsReadOnly}:{i.CanClose}:{i.TabColor}"));
        if (signature != _signature)
        {
            _signature = signature; _headers.Clear(); _strip.Children.Clear();
            _offsets = new double[Items.Count + 1];
            for (int i = 0; i < Items.Count; i++)
                _offsets[i + 1] = _offsets[i] + (vertical ? Theme.TabHeight : Math.Clamp(Items[i].Header.Length * Theme.FontSize * .57 + 52 + (Items[i].IsPinned || Items[i].IsReadOnly ? 15 : 0), 72, 250));
            _strip.Width = vertical ? 155 : _offsets[^1]; _strip.Height = vertical ? _offsets[^1] : Theme.TabHeight;
            _actions.Children.Clear();
            var more = DockChrome.Button("Window list", "tabs:overflow", Theme, ShowOverflow, DockGlyphs.Chevron); more.Width = 23; _actions.Children.Add(more);
            if (ShowNewTabButton) { var add = DockChrome.Button("New document", "tabs:new", Theme, () => NewTabRequested?.Invoke(), DockGlyphs.Add); add.Width = 24; _actions.Children.Add(add); }
        }
        RealizeHeaders();
    }

    private void RealizeHeaders()
    {
        if (_offsets.Length != Items.Count + 1) return;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        double offset = vertical ? _scroll.VerticalOffset : _scroll.HorizontalOffset;
        double viewport = vertical ? _scroll.ViewportHeight : _scroll.ViewportWidth;
        if (viewport <= 0) viewport = vertical ? ActualHeight : ActualWidth;
        if (viewport <= 0) viewport = 800;
        int first = Math.Max(0, Array.BinarySearch(_offsets, Math.Max(0, offset - 250)) is var search && search < 0 ? ~search - 1 : search);
        int last = first;
        while (last < Items.Count && _offsets[last] < offset + viewport + 250) last++;
        var visible = Items.Skip(first).Take(last - first).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _headers.Keys.Where(id => !visible.Contains(id)).ToArray()) { _strip.Children.Remove(_headers[id]); _headers.Remove(id); }
        for (int i = first; i < last; i++)
        {
            var item = Items[i];
            if (!_headers.TryGetValue(item.Id, out var header)) { header = CreateHeader(item); _headers.Add(item.Id, header); _strip.Children.Add(header); }
            DockChrome.Position(header, vertical ? new(0, _offsets[i], 155, Theme.TabHeight) : new(_offsets[i], 0, _offsets[i + 1] - _offsets[i], Theme.TabHeight));
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
        var select = DockChrome.Button(item.Header, "tab:" + item.Id, Theme, () => Select(item.Id, true)); select.Content = label; select.HorizontalAlignment = HorizontalAlignment.Stretch; select.HorizontalContentAlignment = HorizontalAlignment.Left; select.Height = Theme.TabHeight;
        select.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (e.GetCurrentPoint(select).Properties.IsMiddleButtonPressed) { if (item.CanClose) CloseRequested?.Invoke(item.Id); e.Handled = true; return; }
            if (e.GetCurrentPoint(select).Properties.IsLeftButtonPressed) { Select(item.Id, true); TabPointerPressed?.Invoke(this, new(item.Id, select, e)); }
        }), true);
        select.RightTapped += (_, e) => { ContextRequested?.Invoke(item.Id, select); e.Handled = true; };
        select.DoubleTapped += (_, e) => { TabDoubleTapped?.Invoke(item.Id); e.Handled = true; };
        grid.Children.Add(select);
        if (item.CanClose)
        {
            var close = DockChrome.Button("Close " + item.Header, "close:" + item.Id, Theme, () => CloseRequested?.Invoke(item.Id), DockGlyphs.Close);
            close.Width = 23; close.Padding = new(4); close.Height = Theme.TabHeight; Grid.SetColumn(close, 1); grid.Children.Add(close);
        }
        return new Border { Child = grid, BorderThickness = new(0, 0, 1, 0), BorderBrush = DockChrome.Brush(Theme.Border) };
    }
    private void RefreshHeaderColors()
    {
        foreach (var (id, border) in _headers)
        {
            bool selected = id == _selectedId;
            string color = selected ? IsGroupActive && !IsToolGroup ? Theme.Accent : Theme.Surface : Theme.InactiveTab;
            var item = Items.First(i => i.Id == id);
            if (!selected && item.TabColor is not null) color = item.TabColor;
            border.Background = DockChrome.Brush(color);
            foreach (var button in ((Grid)border.Child).Children.OfType<Button>())
            {
                button.Background = DockChrome.Brush(color);
                string foreground = selected && IsGroupActive && !IsToolGroup ? Theme.AccentText : selected && IsToolGroup ? Theme.Accent : Theme.Text;
                button.Foreground = DockChrome.Brush(foreground);
                if (button.Content is StackPanel panel) foreach (var text in panel.Children.OfType<TextBlock>()) text.Foreground = DockChrome.Brush(foreground);
            }
        }
    }
    private void BringSelectedIntoView()
    {
        int index = Items.ToList().FindIndex(i => i.Id == _selectedId);
        if (index < 0 || _offsets.Length <= index + 1) return;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        double offset = vertical ? _scroll.VerticalOffset : _scroll.HorizontalOffset;
        double view = vertical ? _scroll.ViewportHeight : _scroll.ViewportWidth;
        if (view <= 0) return;
        double next = _offsets[index] < offset ? _offsets[index] : _offsets[index + 1] > offset + view ? _offsets[index + 1] - view : offset;
        if (Math.Abs(next - offset) > 1) _scroll.ChangeView(vertical ? null : next, vertical ? next : null, null, true);
    }
    private void ShowOverflow()
    {
        var menu = new MenuFlyout();
        foreach (var item in Items)
        {
            var option = new MenuFlyoutItem { Text = (item.Id == _selectedId ? "✓  " : "    ") + item.Header, MinHeight = 24, FontSize = Theme.FontSize, Padding = new(8, 3, 8, 3) };
            string id = item.Id; option.Click += (_, _) => Select(id, true); menu.Items.Add(option);
        }
        menu.ShowAt(_actions);
    }
}
