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
    private readonly HashSet<AdvancedTabItem> _observed = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<AdvancedTabItem, MeasuredHeader> _measured = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, AdvancedTabItem> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (int Lane, int Slot)> _slots = new(StringComparer.Ordinal);
    private bool _itemsDirty = true;
    private long _itemsRevision, _measuresRevision, _arrangedRevision = -1;
    private double _arrangedExtent = -1;
    private TabOverflowBehavior _arrangedOverflow;
    private bool _arrangedVertical;
    public long TextMeasurementCount { get; private set; }
    public long HeaderLayoutCount { get; private set; }
    public long HeaderCreationCount { get; private set; }
    public long RefreshCount { get; private set; }
    private HeaderStyle? _measuredStyle;
    private readonly record struct HeaderStyle(DockTheme Theme, TabPlacement Placement, double Minimum, double Maximum, bool Close, bool Pin, bool Promote, bool Tools);
    private readonly record struct HeaderKey(string Id, string Header, bool Pinned, bool Preview, bool Modified, bool ReadOnly, bool Close, string? Color);
    private sealed record MeasuredHeader(HeaderKey Key, TabMeasurement Measurement);
    private TabMeasurement[] _measurements = [];
    private TabStripLayout? _plan;
    private bool _batch, _refreshQueued, _ready, _arranging;
    private string? _selectedId;
    private string _layoutSignature = "", _actionSignature = "";
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
    private sealed record HeaderVisual(Border Border, Button Select, TextBlock Text, List<Button> Actions)
    {
        public (string Background, string Foreground)? Paint { get; set; }
    }

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
            _itemsRevision++; _itemsDirty = true;
            if (!_batch) QueueRefresh();
        };
        SizeChanged += (_, _) => ArrangeHeaders(true);
        Loaded += (_, _) => { _itemsDirty = true; Refresh(); Select(_selectedId ?? Items.FirstOrDefault()?.Id); };
        Unloaded += (_, _) =>
        {
            CancelTabDrag();
            foreach (var item in _observed) item.MetadataChanged -= ItemMetadataChanged;
            _observed.Clear(); _itemsDirty = true;
        };
        ConfigureTabDragging();
        _header.AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
            if (e.Handled || Items.Count == 0 || !(e.Key is VirtualKey.Home or VirtualKey.End || (vertical ? e.Key is VirtualKey.Up or VirtualKey.Down : e.Key is VirtualKey.Left or VirtualKey.Right))) return;
            if (_itemsDirty) Refresh();
            var order = _plan?.Lanes.SelectMany(lane => lane.Indices).ToArray() ?? Enumerable.Range(0, Items.Count).ToArray();
            int index = Math.Max(0, Array.FindIndex(order, i => Items[i].Id == _selectedId));
            int direction = e.Key is VirtualKey.Left or VirtualKey.Up ? -1 : 1;
            index = e.Key == VirtualKey.Home ? 0 : e.Key == VirtualKey.End ? order.Length - 1 : (index + direction + order.Length) % order.Length;
            Select(Items[order[index]].Id, true);
            RealizeHeaders(); // Keyboard focus needs the newly revealed header now.
            if (_headers.TryGetValue(_selectedId!, out var header)) header.Select.Focus(FocusState.Keyboard);
            e.Handled = true;
        }), false);
        _ready = true;
    }
    private static TabDisplayMode Mode(AdvancedTabItem item) => item.IsPinned ? TabDisplayMode.Pinned : item.IsPreview ? TabDisplayMode.Preview : TabDisplayMode.Normal;
    private void ItemMetadataChanged(AdvancedTabItem item) { _itemsRevision++; _itemsDirty = true; QueueRefresh(); }
    private void ObserveItems()
    {
        var current = new HashSet<AdvancedTabItem>(Items, ReferenceEqualityComparer.Instance);
        foreach (var item in _observed.Where(item => !current.Contains(item)).ToArray())
        { item.MetadataChanged -= ItemMetadataChanged; _observed.Remove(item); _measured.Remove(item); }
        foreach (var item in _measured.Keys.Where(item => !current.Contains(item)).ToArray()) _measured.Remove(item);
        foreach (var item in current) if (_observed.Add(item)) item.MetadataChanged += ItemMetadataChanged;
    }
    private void QueueRefresh()
    {
        if (!_ready || _batch || _refreshQueued) return;
        _refreshQueued = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _refreshQueued = false; Refresh();
            Select(_selectedId is not null && _byId.ContainsKey(_selectedId) ? _selectedId : Items.FirstOrDefault()?.Id);
        })) _refreshQueued = false;
    }
    public void Update(IReadOnlyList<AdvancedTabItem> items, string? selectedId, DockTheme theme, TabPlacement placement, bool active)
    {
        ArgumentNullException.ThrowIfNull(items); ArgumentNullException.ThrowIfNull(theme);
        if (!Enum.IsDefined(placement)) throw new ArgumentOutOfRangeException(nameof(placement));
        _batch = true;
        try { if (!Items.SequenceEqual(items)) { Items.Clear(); foreach (var item in items) Items.Add(item); } }
        finally { _batch = false; }
        Theme = theme; Placement = placement; IsGroupActive = active;
        Refresh(); Select(selectedId);
    }
    public void Select(string? id, bool notify = false) => SelectCore(id, notify);
    public void ReleaseContent() => ContentLease.Set(_presenter, null);

    private void Refresh()
    {
        if (!_ready) return;
        RefreshCount++;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        var style = new HeaderStyle(Theme, Placement, MinTabExtent, MaxTabExtent, HasTabCloseButtons, HasTabPinButtons, CanTabsPromote, IsToolGroup);
        if (_itemsDirty || _measuredStyle != style) ReconcileMeasurements(style);
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
        BuildActions(); ArrangeHeaders(true);
    }
    private void RemoveHeader(string id)
    {
        if (!_headers.Remove(id, out var header)) return;
        if (header.Border.Parent is Panel parent) parent.Children.Remove(header.Border);
    }
    private void ReconcileMeasurements(HeaderStyle style)
    {
        ObserveItems();
        bool all = _measuredStyle != style;
        if (all) { _measured.Clear(); foreach (string id in _headers.Keys.ToArray()) RemoveHeader(id); }
        _byId.Clear();
        var next = new TabMeasurement[Items.Count];
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (string.IsNullOrWhiteSpace(item.Id) || !_byId.TryAdd(item.Id, item)) throw new InvalidOperationException("Tab ids must be nonempty and unique.");
            var key = new HeaderKey(item.Id, item.Header, item.IsPinned, item.IsPreview, item.IsModified, item.IsReadOnly, item.CanClose, item.TabColor);
            if (!_measured.TryGetValue(item, out var previous) || previous.Key != key)
            {
                if (previous is not null) RemoveHeader(previous.Key.Id);
                RemoveHeader(item.Id); // A different item instance may reuse the same public id.
                bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
                double minimum = vertical ? Math.Min(MinTabExtent, Theme.TabHeight) : MinTabExtent;
                double maximum = vertical ? Math.Max(minimum, Theme.TabHeight) : MaxTabExtent;
                var text = DockChrome.Text(item.Header + (item.IsModified ? " *" : ""), Theme);
                if (item.IsPreview) text.FontStyle = Windows.UI.Text.FontStyle.Italic;
                TextMeasurementCount++; text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double desired = vertical ? Theme.TabHeight : text.DesiredSize.Width + 16 + (item.IsReadOnly ? 16 : 0) + HeaderActionCount(item) * 23;
                previous = new(key, new(item.Id, Mode(item), desired, minimum, maximum));
                _measured[item] = previous;
            }
            next[i] = previous.Measurement;
        }
        foreach (string id in _headers.Keys.Where(id => !_byId.ContainsKey(id)).ToArray()) RemoveHeader(id);
        if (!next.AsSpan().SequenceEqual(_measurements)) { _measurements = next; _measuresRevision++; }
        _measuredStyle = style; _itemsDirty = false;
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
