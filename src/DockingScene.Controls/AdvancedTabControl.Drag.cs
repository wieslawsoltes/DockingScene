using System.Diagnostics;
using DockingScene.Layout;
using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;

namespace DockingScene.Controls;

public partial class AdvancedTabControl
{
    private readonly DispatcherTimer _dragScroll = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly Stopwatch _dragScrollClock = new();
    private Point _dragPosition;
    private bool _dragScrollRunning;
    public long DragAutoScrollSteps { get; private set; }
    public static readonly DependencyProperty CanTabsAutoScrollProperty = DependencyProperty.Register(nameof(CanTabsAutoScroll), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(true));
    public bool CanTabsAutoScroll { get => (bool)GetValue(CanTabsAutoScrollProperty); set => SetValue(CanTabsAutoScrollProperty, value); }
    private string? _dragTabId;
    private long _dragItemsRevision;
    private Point _tabDragStart;
    private int _tabInsertion = -1;
    private bool _tabDragMoved;
    private readonly Border _tabInsertionMarker = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    public static readonly DependencyProperty CanTabsDragProperty = DependencyProperty.Register(nameof(CanTabsDrag), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(true));
    public bool CanTabsDrag { get => (bool)GetValue(CanTabsDragProperty); set => SetValue(CanTabsDragProperty, value); }
    public bool IsTabDragActive => _dragTabId is not null;
    public event EventHandler<TabReorderingEventArgs>? TabReordering;
    public event EventHandler<TabReorderedEventArgs>? TabReordered;

    /// <summary>Reorder within an item's display-mode lane. The new index is the final collection index.
    /// A cancellable request precedes mutation. Selection and content identities are retained.</summary>
    public bool ReorderTab(string id, int newIndex)
    {
        if (!CanTabsDrag) return false;
        if (_itemsDirty) Refresh();
        if (!_byId.TryGetValue(id, out var item) || newIndex < 0 || newIndex >= Items.Count) return false;
        int oldIndex = Items.IndexOf(item);
        if (oldIndex == newIndex) return true;
        if (Mode(item) != Mode(Items[newIndex])) return false;
        long revision = _itemsRevision;
        var args = new TabReorderingEventArgs(item, oldIndex, newIndex); TabReordering?.Invoke(this, args);
        if (args.Cancel || revision != _itemsRevision || !CanTabsDrag) return false;
        string? selected = _selectedId;
        _batch = true;
        try { Items.Move(oldIndex, newIndex); }
        finally { _batch = false; }
        Refresh(); Select(selected);
        TabReordered?.Invoke(this, new(item, oldIndex, newIndex)); return true;
    }
    private void ConfigureTabDragging()
    {
        _dragScroll.Tick += (_, _) => AutoScrollDrag();
        _header.Children.Add(_tabInsertionMarker); Canvas.SetZIndex(_tabInsertionMarker, 100);
        _header.AddHandler(PointerMovedEvent, new PointerEventHandler((_, e) => MoveTabDrag(e)), true);
        _header.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, e) =>
        {
            if (_dragTabId is not { } id) return;
            int insertion = _tabInsertion; bool commit = _tabDragMoved && _dragItemsRevision == _itemsRevision;
            bool clicked = !_tabDragMoved;
            CancelTabDrag();
            if (clicked) FocusSelectedContent(FocusState.Pointer);
            if (commit && insertion >= 0 && _byId.TryGetValue(id, out var item))
            {
                int oldIndex = Items.IndexOf(item);
                ReorderTab(id, Math.Clamp(insertion - (oldIndex < insertion ? 1 : 0), 0, Items.Count - 1));
            }
            e.Handled = true;
        }), true);
        _header.AddHandler(PointerCanceledEvent, new PointerEventHandler((_, _) => CancelTabDrag()), true);
        _header.AddHandler(PointerCaptureLostEvent, new PointerEventHandler((_, e) =>
        {
            if (IsTabDragActive && _header.PointerCaptures?.Any(p => p.PointerId == e.Pointer.PointerId) != true) CancelTabDrag();
        }), true);
        _header.AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        { if (IsTabDragActive && e.Key == VirtualKey.Escape) { CancelTabDrag(); e.Handled = true; } }), true);
    }
    private void BeginTabDrag(AdvancedTabItem item, PointerRoutedEventArgs e)
    {
        if (!CanTabsDrag || IsTabDragActive || !e.GetCurrentPoint(_header).Properties.IsLeftButtonPressed) return;
        // Standalone only: hosted tabs delegate pointer ownership to their DockSite.
        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, _header); node = VisualTreeHelper.GetParent(node))
            if (node is UIElement element && element.PointerCaptures?.Any(p => p.PointerId == e.Pointer.PointerId) == true)
                element.ReleasePointerCapture(e.Pointer);
        if (!_header.CapturePointer(e.Pointer)) return;
        _dragTabId = item.Id; _dragItemsRevision = _itemsRevision; _tabDragStart = e.GetCurrentPoint(_header).Position;
        _tabDragMoved = false; _tabInsertion = -1; e.Handled = true;
    }
    private void MoveTabDrag(PointerRoutedEventArgs e)
    {
        if (_dragTabId is null) return;
        if (!TryGetDragLane(out var lane)) { CancelTabDrag(); return; }
        _dragPosition = e.GetCurrentPoint(_header).Position;
        double dx = _dragPosition.X - _tabDragStart.X, dy = _dragPosition.Y - _tabDragStart.Y;
        if (!_tabDragMoved && dx * dx + dy * dy < 25) return;
        _tabDragMoved = true;
        UpdateTabInsertion(lane);
        if (CanTabsAutoScroll && NextDragOffset(lane, .03) != lane.Offset)
        {
            if (!_dragScrollRunning) { _dragScrollRunning = true; _dragScrollClock.Restart(); _dragScroll.Start(); }
        }
        else StopDragScroll();
        e.Handled = true;
    }
    private bool TryGetDragLane(out Lane lane)
    {
        lane = _lanes[1];
        if (_dragTabId is not { } id || !CanTabsDrag || _dragItemsRevision != _itemsRevision || !_slots.TryGetValue(id, out var slot)) return false;
        lane = _lanes[slot.Lane]; return lane.Layout is not null;
    }
    private double NextDragOffset(Lane lane, double elapsed)
    {
        if (lane.Layout is not { } plan) return lane.Offset;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        double axis = vertical ? _dragPosition.Y : _dragPosition.X, cross = vertical ? _dragPosition.X : _dragPosition.Y;
        if (cross < 0 || cross >= (vertical ? 155 : Theme.TabHeight)) return lane.Offset;
        return TabEdgeScroll.NextOffset(axis - plan.Start, plan.Extent, plan.Metrics.Length, lane.Offset, elapsed);
    }
    private void AutoScrollDrag()
    {
        if (!_dragScrollRunning) return;
        if (!TryGetDragLane(out var lane)) { CancelTabDrag(); return; }
        if (!CanTabsAutoScroll) { StopDragScroll(); return; }
        double next = NextDragOffset(lane, _dragScrollClock.Elapsed.TotalSeconds); _dragScrollClock.Restart();
        if (next == lane.Offset) { StopDragScroll(); return; }
        lane.Offset = next; DragAutoScrollSteps++; RealizeHeaders(); UpdateTabInsertion(lane);
    }
    private void UpdateTabInsertion(Lane lane)
    {
        _tabInsertion = -1; _tabInsertionMarker.Visibility = Visibility.Collapsed;
        var plan = lane.Layout;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        double axis = vertical ? _dragPosition.Y : _dragPosition.X, cross = vertical ? _dragPosition.X : _dragPosition.Y;
        if (plan is null || axis < plan.Start || axis >= plan.Start + plan.Extent || cross < 0 || cross >= (vertical ? 155 : Theme.TabHeight)) return;
        int slot = plan.Metrics.InsertionIndex(axis - plan.Start + lane.Offset);
        _tabInsertion = slot < plan.Indices.Length ? plan.Indices[slot] : plan.Indices[^1] + 1;
        double edge = Math.Clamp(plan.Start + plan.Metrics.Offsets[slot] - lane.Offset, plan.Start, plan.Start + Math.Max(0, plan.Extent - 2));
        _tabInsertionMarker.Background = DockChrome.Brush(Theme.Accent);
        DockChrome.Position(_tabInsertionMarker, vertical ? new(0, edge, 155, 2) : new(edge, 0, 2, Theme.TabHeight), 100);
        _tabInsertionMarker.Visibility = Visibility.Visible;
    }
    private void StopDragScroll() { _dragScrollRunning = false; _dragScroll.Stop(); _dragScrollClock.Stop(); }
    private void CancelTabDrag()
    {
        StopDragScroll();
        _dragTabId = null; _tabInsertion = -1; _tabDragMoved = false;
        _tabInsertionMarker.Visibility = Visibility.Collapsed; _header.ReleasePointerCaptures();
    }

}
public sealed class TabReorderingEventArgs(AdvancedTabItem item, int oldIndex, int newIndex) : EventArgs
{
    public AdvancedTabItem Item { get; } = item;
    public int OldIndex { get; } = oldIndex;
    public int NewIndex { get; } = newIndex;
    public bool Cancel { get; set; }
}
public sealed class TabReorderedEventArgs(AdvancedTabItem item, int oldIndex, int newIndex) : EventArgs
{
    public AdvancedTabItem Item { get; } = item;
    public int OldIndex { get; } = oldIndex;
    public int NewIndex { get; } = newIndex;
}
