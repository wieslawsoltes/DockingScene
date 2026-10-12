using System.Collections.Immutable;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace DockingScene.Controls;

public partial class DockSite
{
    private ImmutableArray<string> _dragIds = [];
    private Point _dragStart;
    private DockRect _dragBounds;
    private long _dragRevision;
    private bool _dragging;
    private DockDragPreview? _preview;
    private int _tabDropIndex = -1;
    private SplitterGeometry? _splitDrag;
    private double _splitRatio;
    private long _splitRevision;
    private StandardSwitcher? _switcher;
    public DockDragPreview? DragPreview => _preview;
    public string? OpenAutoHideWindowId => _autoOpen;
    public DockRect OpenAutoHideBounds => _autoBounds;
    public bool IsInteractionActive => !_dragIds.IsEmpty || _splitDrag is not null || _autoResizeId is not null;
    private void MoveInteraction(PointerRoutedEventArgs e)
    {
        if (CancelStaleInteraction()) { e.Handled = true; return; }
        var p = e.GetCurrentPoint(_canvas).Position;
        if (MoveAutoHideResize(p)) { e.Handled = true; return; }
        if (_splitDrag is { } split)
        {
            _splitRatio = DockLayoutEngine.RatioAt(split, p.X, p.Y, Theme.SplitterSize);
            var temp = DockTree.Map(Manager.Layout, n => n.Id == split.Id && n is DockSplit s ? s with { Ratio = _splitRatio } : n);
            var geometry = DockLayoutEngine.Arrange(temp, new(0, 0, ActualWidth, ActualHeight), new() { Splitter = Theme.SplitterSize, TabHeight = Theme.TabHeight, TitleHeight = Theme.TitleHeight });
            foreach (var (id, group) in _groups) if (geometry.Nodes.TryGetValue(id, out var rect)) { Canvas.SetLeft(group, rect.X); Canvas.SetTop(group, rect.Y); group.Width = rect.Width; group.Height = rect.Height; }
            _overlay.Children.Clear(); var bar = geometry.Splitters.First(s => s.Id == split.Id);
            var visual = new Border { Background = DockChrome.Brush(Theme.Accent), Opacity = .6 }; DockChrome.Position(visual, bar.Bounds); _overlay.Children.Add(visual);
            e.Handled = true; return;
        }
        if (_dragIds.IsEmpty || _geometry is null) return;
        double dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
        if (!_dragging && dx * dx + dy * dy < 25) return;
        _dragging = true; _tabDropIndex = -1;
        bool suppress = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
        var floating = ApplyFloatingDrag(_dragBounds with { X = _dragBounds.X + dx, Y = _dragBounds.Y + dy }, suppress);
        _preview = DockGuides.HitTest(Manager.Layout, _geometry, _dragIds, p.X, p.Y, floating, suppress);
        if (!suppress && _dragIds.All(id => Manager.Allows(id, DockCapabilities.Dock | DockCapabilities.Attach)))
        {
            foreach (var view in _groups.Values.OrderByDescending(Canvas.GetZIndex))
            {
                var group = view.Group;
                if (group is null || !CanDropInto(group)) continue;
                var local = _canvas.TransformToVisual(view.Tabs).TransformPoint(p);
                if (!view.Tabs.TryGetInsertionIndex(local, out int index)) continue;
                _tabDropIndex = index - group.Windows.Take(index).Count(_dragIds.Contains);
                var bounds = _geometry.Nodes[group.Id];
                _preview = new([], new(group.Id, DockSide.Center, bounds, bounds, false), floating); break;
            }
        }
        if (_preview.Target is { } target && (_dragIds.Any(id => !Manager.Allows(id, DockCapabilities.Dock)) ||
            (target.Side == DockSide.Center && (_dragIds.Any(id => !Manager.Allows(id, DockCapabilities.Attach)) ||
                DockTree.Find(Manager.Layout, target.TargetId ?? "") is DockGroup destination && !CanDropInto(destination)))))
        { _preview = new([], null, floating); _tabDropIndex = -1; }
        RenderDragPreview(_preview); e.Handled = true;
    }
    private bool CanDropInto(DockGroup group)
    {
        var layout = Manager.Layout;
        if (group.Kind == GroupKind.Tools) return _dragIds.All(id => layout.Windows[id].Kind == WindowKind.Tool);
        return _dragIds.All(id => layout.Windows[id].Kind == WindowKind.Document || Manager.Policy.AllowMixedDocumentGroups && Manager.Allows(id, DockCapabilities.EnterWorkspace));
    }
    private void RenderDragPreview(DockDragPreview preview) => UpdateDragFeedback(preview);
    private void EndInteraction(PointerRoutedEventArgs e)
    {
        if (IsInteractionActive) TraceInput("Release", e);
        if (EndAutoHideResize(e)) return;
        if (_splitDrag is not null)
        {
            var split = _splitDrag; _splitDrag = null; _canvas.ReleasePointerCapture(e.Pointer); _overlay.Children.Clear();
            Submit(Manager.ResizeSplit(split.Id, _splitRatio, _splitRevision)); QueueRender(); e.Handled = true; return;
        }
        if (_dragIds.IsEmpty) return;
        var ids = _dragIds; var preview = _preview; var plan = _floatingDragPlan;
        bool dragged = _dragging; long revision = _dragRevision; int index = _tabDropIndex;
        _dragIds = []; _dragging = false; _preview = null; _tabDropIndex = -1;
        ReleaseFloatingDragPlan(); _canvas.ReleasePointerCapture(e.Pointer); _overlay.Children.Clear();
        if (dragged && preview is not null)
        {
            if (Manager.Layout.Revision != revision) Submit(new(false, Manager.Layout.Revision, "The layout changed while dragging. Please drag again."));
            else if (preview.Target is { } target) Submit(Manager.DockMany(ids, target.TargetId, target.Side, index, revision));
            else if (plan is not null) Submit(plan.Commit(Manager, preview.FloatingBounds));
        }
        QueueRender(); e.Handled = true;
    }
    private void CancelInteraction()
    {
        _dragIds = []; _dragging = false; _preview = null; _splitDrag = null; _tabDropIndex = -1; _autoResizeId = null;
        ReleaseGestureKeyboard(); ReleaseFloatingDragPlan(); _canvas.ReleasePointerCaptures(); _overlay.Children.Clear();
        foreach (var window in _floats.Values) window.CancelMovement(); QueueRender();
    }
    public void ShowSwitcher(int direction = 1)
    {
        if (_switcher is not null) { _switcher.Move(direction); return; }
        _switcher = new(Manager.Layout, Theme, Strings) { Width = Math.Min(620, Math.Max(240, ActualWidth - 30)), Height = Math.Min(370, Math.Max(180, ActualHeight - 30)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _switcher.Cancelled += () => CloseSwitcher(false);
        _switcher.Committed += id => { var current = _switcher; _switcher = null; if (current is not null) _root.Children.Remove(current); Submit(Manager.Activate(id)); };
        // Attach before adding the visual: a fast key may arrive before focus
        // leaves an external toolbar, even though the overlay is already painted.
        _switcher.AcquireInputScope(XamlRoot?.Content);
        if (direction < 0) _switcher.Move(-2); _root.Children.Add(_switcher);
        _switcher.TakeInputFocus();
    }
    private void CloseSwitcher(bool commit)
    {
        if (_switcher is null) return;
        if (commit) _switcher.Commit(); else { _switcher.ReleaseInputScope(); _root.Children.Remove(_switcher); _switcher = null; }
    }
    public void ShowWindowMenu(string id, FrameworkElement source)
    {
        var layout = Manager.Layout;
        if (!layout.Windows.TryGetValue(id, out var window)) return;
        var group = DockTree.GroupOf(layout, id); var location = DockTree.Locate(layout, id); var menu = new MenuFlyout();
        void Item(string text, Action action, bool enabled = true)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, IsEnabled = enabled, FontSize = Theme.FontSize, MinHeight = 24, Padding = new(8, 3, 8, 3) }, "menu:" + text, text);
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        bool Can(DockCapabilities ability) => Manager.Allows(id, ability) && !layout.IsLayoutLocked;
        Item(Strings.Float, () => Submit(Manager.Float(id)), Can(DockCapabilities.Float));
        Item(Strings.Dock, () => Submit(Manager.Dock(id)), Can(DockCapabilities.Dock));
        if (window.Kind == WindowKind.Tool)
        {
            bool hidden = location.Placement == WindowPlacement.AutoHidden;
            Item(hidden ? Strings.Pin : Strings.AutoHide, () => Submit(hidden ? Manager.PinAutoHide(id) : Manager.AutoHide(id, group is null ? DockSide.Left : NearestEdge(group.Id))), Can(hidden ? DockCapabilities.Dock : DockCapabilities.AutoHide));
            Item(Strings.MoveToWorkspace, () => Submit(Manager.MoveToWorkspace(id)), Can(DockCapabilities.EnterWorkspace | DockCapabilities.Dock));
        }
        else
        {
            if (window.DisplayMode == TabDisplayMode.Preview) Item("Keep open", () => Submit(Manager.KeepOpen(id)));
            Item(window.DisplayMode == TabDisplayMode.Pinned ? Strings.Unpin : Strings.Pin, () => Submit(Manager.SetPinned(id, window.DisplayMode != TabDisplayMode.Pinned)));
            Item(Strings.NewHorizontalGroup, () => Submit(Manager.Dock(id, group!.Id, DockSide.Bottom)), group?.Windows.Length > 1 && Can(DockCapabilities.Dock));
            Item(Strings.NewVerticalGroup, () => Submit(Manager.Dock(id, group!.Id, DockSide.Right)), group?.Windows.Length > 1 && Can(DockCapabilities.Dock));
        }
        if (group is not null)
        {
            var placement = DockChrome.Identify(new MenuFlyoutSubItem { Text = "Tab placement", FontSize = Theme.FontSize, IsEnabled = !layout.IsLayoutLocked }, "menu:Tab placement");
            foreach (var side in Enum.GetValues<TabPlacement>())
            {
                var option = DockChrome.Identify(new MenuFlyoutItem { Text = side.ToString(), FontSize = Theme.FontSize, MinHeight = 24 }, "menu:Tab placement:" + side);
                option.Click += (_, _) => Submit(Manager.SetTabPlacement(group.Id, side)); placement.Items.Add(option);
            }
            menu.Items.Add(placement);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        Item(Strings.Close, () => Submit(Manager.Close(id)), Manager.Allows(id, DockCapabilities.Close));
        if (group is not null)
        {
            Item(Strings.CloseOthers, () => Submit(Manager.CloseOtherTabs(id)), group.Windows.Length > 1);
            Item("Close tabs to the left", () => Submit(Manager.CloseTabsToLeft(id)), group.Windows.IndexOf(id) > 0);
            Item("Close tabs to the right", () => Submit(Manager.CloseTabsToRight(id)), group.Windows.IndexOf(id) < group.Windows.Length - 1);
            Item(Strings.CloseAll, () => Submit(Manager.CloseMany(group.Windows)));
        }
        menu.ShowAt(source);
    }
}
