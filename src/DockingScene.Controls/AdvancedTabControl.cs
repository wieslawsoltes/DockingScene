using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
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

/// <summary>Standalone retained-content tab control. Measured headers are virtualized
/// inside three independently clipped lanes; scrolling normal tabs cannot move pinned/preview tabs.</summary>
public partial class AdvancedTabControl : UserControl
{
    private readonly Grid _root = new();
    private readonly Canvas _header = new();
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal };
    private readonly ContentPresenter _presenter = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Lane[] _lanes = [new(), new(), new()];
    private readonly Dictionary<string, HeaderVisual> _headers = new(StringComparer.Ordinal);
    private readonly HashSet<AdvancedTabItem> _observed = [];
    private TabMeasurement[] _measurements = [];
    private TabStripLayout? _plan;
    private bool _batch, _refreshQueued, _ready, _arranging;
    private string? _selectedId;
    private string _signature = "", _layoutSignature = "", _actionSignature = "";
    private bool _showNewTabButton;
    public ObservableCollection<AdvancedTabItem> Items { get; } = [];
    public DockTheme Theme { get; private set; } = DockTheme.MetroLight;
    public TabPlacement Placement { get; private set; }
    public bool IsGroupActive { get; private set; }
    public bool ShowNewTabButton { get => _showNewTabButton; set { if (_showNewTabButton == value) return; _showNewTabButton = value; QueueRefresh(); } }
    public bool IsToolGroup { get; set; }
    public string AutomationIdPrefix { get; set; } = DockTree.NewId("tabs");
    public string? SelectedId => _selectedId;
    public int RealizedHeaderCount => _headers.Count;
    public TabStripLayout? HeaderLayout => _plan;
    public double GetLaneOffset(TabDisplayMode mode) => _lanes[TabLayoutEngine.LaneIndex(mode)].Offset;
    public event Action<string>? Activated;
    public event Action<string>? CloseRequested;
    public event EventHandler<TabClosingEventArgs>? TabClosing;
    public event EventHandler<TabMenuOpeningEventArgs>? MenuOpening;
    public event Action<string, bool>? PinRequested;
    public event EventHandler<TabPointerEventArgs>? TabPointerPressed;
    public new event Action<string, FrameworkElement>? ContextRequested;
    public event Action<string>? TabDoubleTapped;
    public event Action? NewTabRequested;

    private sealed class Lane
    {
        public Canvas View { get; } = new();
        public TabLaneLayout? Layout { get; set; }
        public double Offset { get; set; }
    }
    private sealed record HeaderVisual(Border Border, Button Select, TextBlock Text, List<Button> Actions);

    public AdvancedTabControl()
    {
        Content = _root; _root.Children.Add(_header); _root.Children.Add(_presenter);
        foreach (var lane in _lanes)
        {
            _header.Children.Add(lane.View);
            lane.View.AddHandler(PointerWheelChangedEvent, new PointerEventHandler((_, e) =>
            {
                if (TabLayoutEngine.CanScroll(OverflowBehavior)) ScrollLane(lane, -e.GetCurrentPoint(lane.View).Properties.MouseWheelDelta);
                e.Handled = true;
            }), true);
        }
        _header.Children.Add(_actions);
        Items.CollectionChanged += (_, _) =>
        {
            if (_batch) return;
            ObserveItems(); Refresh(); Select(Items.FirstOrDefault(i => i.Id == _selectedId)?.Id ?? Items.FirstOrDefault()?.Id);
        };
        SizeChanged += (_, _) => ArrangeHeaders(true);
        Loaded += (_, _) => { ObserveItems(); Refresh(); Select(_selectedId ?? Items.FirstOrDefault()?.Id); };
        _header.AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
            if (e.Handled || Items.Count == 0 || !(e.Key is VirtualKey.Home or VirtualKey.End || (vertical ? e.Key is VirtualKey.Up or VirtualKey.Down : e.Key is VirtualKey.Left or VirtualKey.Right))) return;
            var order = _plan?.Lanes.SelectMany(lane => lane.Indices).ToArray() ?? Enumerable.Range(0, Items.Count).ToArray();
            int index = Math.Max(0, Array.FindIndex(order, i => Items[i].Id == _selectedId));
            int direction = e.Key is VirtualKey.Left or VirtualKey.Up ? -1 : 1;
            index = e.Key == VirtualKey.Home ? 0 : e.Key == VirtualKey.End ? order.Length - 1 : (index + direction + order.Length) % order.Length;
            Select(Items[order[index]].Id, true);
            if (_headers.TryGetValue(_selectedId!, out var header)) header.Select.Focus(FocusState.Keyboard);
            e.Handled = true;
        }), false);
        _ready = true;
    }
    private static TabDisplayMode Mode(AdvancedTabItem item) => item.IsPinned ? TabDisplayMode.Pinned : item.IsPreview ? TabDisplayMode.Preview : TabDisplayMode.Normal;
    private void ObserveItems()
    {
        foreach (var item in _observed.Where(item => !Items.Contains(item)).ToArray()) { item.MetadataChanged -= QueueRefresh; _observed.Remove(item); }
        foreach (var item in Items) if (_observed.Add(item)) item.MetadataChanged += QueueRefresh;
    }
    private void QueueRefresh()
    {
        if (!_ready || _batch || _refreshQueued) return;
        _refreshQueued = true;
        if (!DispatcherQueue.TryEnqueue(() => { _refreshQueued = false; Refresh(); })) _refreshQueued = false;
    }
    public void Update(IReadOnlyList<AdvancedTabItem> items, string? selectedId, DockTheme theme, TabPlacement placement, bool active)
    {
        ArgumentNullException.ThrowIfNull(items); ArgumentNullException.ThrowIfNull(theme);
        if (!Enum.IsDefined(placement)) throw new ArgumentOutOfRangeException(nameof(placement));
        _batch = true;
        try { if (!Items.SequenceEqual(items)) { Items.Clear(); foreach (var item in items) Items.Add(item); } }
        finally { _batch = false; }
        ObserveItems(); Theme = theme; Placement = placement; IsGroupActive = active;
        Refresh(); Select(selectedId);
    }
    public void Select(string? id, bool notify = false)
    {
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (id is not null && item is null) throw new ArgumentException("Unknown tab id.", nameof(id));
        _selectedId = id; item?.EnsureContent(); ContentLease.Set(_presenter, item);
        RevealSelection(); RealizeHeaders();
        if (notify && id is not null) Activated?.Invoke(id);
    }
    public void ReleaseContent() => ContentLease.Set(_presenter, null);

    private void Refresh()
    {
        if (!_ready) return;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        if (Items.Any(i => string.IsNullOrWhiteSpace(i.Id)) || Items.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != Items.Count)
            throw new InvalidOperationException("Tab ids must be nonempty and unique.");
        _root.Background = DockChrome.Brush(Theme.Surface); _header.Background = DockChrome.Brush(Theme.InactiveTab);
        string layout = $"{Placement}:{Theme.TabHeight}:{IsTabStripVisible}";
        if (_layoutSignature != layout)
        {
            _layoutSignature = layout;
            _root.RowDefinitions.Clear(); _root.ColumnDefinitions.Clear();
            Grid.SetRow(_header, 0); Grid.SetColumn(_header, 0); Grid.SetRow(_presenter, 0); Grid.SetColumn(_presenter, 0);
            double size = IsTabStripVisible ? vertical ? 155 : Theme.TabHeight : 0;
            if (vertical)
            {
                _root.ColumnDefinitions.Add(new() { Width = Placement == TabPlacement.Left ? new(size) : new(1, GridUnitType.Star) });
                _root.ColumnDefinitions.Add(new() { Width = Placement == TabPlacement.Right ? new(size) : new(1, GridUnitType.Star) });
                Grid.SetColumn(_header, Placement == TabPlacement.Left ? 0 : 1); Grid.SetColumn(_presenter, Placement == TabPlacement.Left ? 1 : 0);
            }
            else
            {
                _root.RowDefinitions.Add(new() { Height = Placement == TabPlacement.Top ? new(size) : new(1, GridUnitType.Star) });
                _root.RowDefinitions.Add(new() { Height = Placement == TabPlacement.Bottom ? new(size) : new(1, GridUnitType.Star) });
                Grid.SetRow(_header, Placement == TabPlacement.Top ? 0 : 1); Grid.SetRow(_presenter, Placement == TabPlacement.Top ? 1 : 0);
            }
            _header.Visibility = IsTabStripVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        string signature = $"{Theme}:{Placement}:{MinTabExtent}:{MaxTabExtent}:{HasTabCloseButtons}:{HasTabPinButtons}:{CanTabsPromote}:" +
            string.Join('|', Items.Select(i => $"{RuntimeHelpers.GetHashCode(i)}:{i.Id.Length}:{i.Id}:{i.Header.Length}:{i.Header}:{i.IsPinned}:{i.IsPreview}:{i.IsModified}:{i.IsReadOnly}:{i.CanClose}:{i.TabColor}"));
        if (signature != _signature)
        {
            _signature = signature; _headers.Clear(); foreach (var lane in _lanes) lane.View.Children.Clear();
            _measurements = Items.Select(item =>
            {
                double minimum = vertical ? Math.Min(MinTabExtent, Theme.TabHeight) : MinTabExtent;
                double maximum = vertical ? Math.Max(minimum, Theme.TabHeight) : MaxTabExtent;
                var text = DockChrome.Text(item.Header + (item.IsModified ? " *" : ""), Theme);
                if (item.IsPreview) text.FontStyle = Windows.UI.Text.FontStyle.Italic;
                text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double desired = vertical ? Theme.TabHeight : text.DesiredSize.Width + 16 + (item.IsReadOnly ? 16 : 0) + HeaderActionCount(item) * 23;
                return new TabMeasurement(item.Id, Mode(item), desired, minimum, maximum);
            }).ToArray();
        }
        BuildActions(); ArrangeHeaders(true);
    }
    private int HeaderActionCount(AdvancedTabItem item) => (HasTabCloseButtons && item.CanClose ? 1 : 0) +
        (!IsToolGroup && (item.IsPinned || HasTabPinButtons && !item.IsPreview) ? 1 : 0) + (item.IsPreview && CanTabsPromote ? 1 : 0);
}

public sealed class TabClosingEventArgs(AdvancedTabItem item) : EventArgs
{
    public AdvancedTabItem Item { get; } = item;
    public bool Cancel { get; set; }
}
public sealed class TabMenuOpeningEventArgs(MenuFlyout menu) : EventArgs
{
    public MenuFlyout Menu { get; } = menu;
    public bool Cancel { get; set; }
}
