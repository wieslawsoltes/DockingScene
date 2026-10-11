using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace DockingScene.Controls;

public partial class AdvancedTabControl
{
    private int ActionCount => (TabLayoutEngine.HasMenu(OverflowBehavior) ? 1 : 0) + (TabLayoutEngine.CanScroll(OverflowBehavior) ? 2 : 0) + (ShowNewTabButton ? 1 : 0);
    private void BuildActions()
    {
        string signature = $"{Theme}:{OverflowBehavior}:{ShowNewTabButton}:{AutomationIdPrefix}";
        if (_actionSignature == signature) return;
        _actionSignature = signature; _actions.Children.Clear();
        void Add(string title, string name, string glyph, Action action)
        {
            var button = DockChrome.Button(title, "tabs:" + AutomationIdPrefix + ":" + name, Theme, action, glyph);
            button.Tag = name; button.Width = 23; button.Height = Theme.TabHeight; button.Padding = new(5); _actions.Children.Add(button);
        }
        if (TabLayoutEngine.CanScroll(OverflowBehavior))
        {
            Add("Scroll tabs backward", "back", "M10,2 L4,8 L10,14", () => ScrollActiveLane(-1));
            Add("Scroll tabs forward", "forward", "M4,2 L10,8 L4,14", () => ScrollActiveLane(1));
        }
        if (TabLayoutEngine.HasMenu(OverflowBehavior)) Add("Window list", "overflow", DockGlyphs.Chevron, ShowOverflowMenu);
        if (ShowNewTabButton) Add("New document", "new", DockGlyphs.Add, () => NewTabRequested?.Invoke());
    }
    private void ArrangeHeaders(bool reveal = false)
    {
        if (!_ready || _itemsDirty || _arranging || _measurements.Length != Items.Count) return;
        _arranging = true;
        try
        {
            bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
            double total = Math.Max(0, vertical ? ActualHeight : ActualWidth);
            double actions = Math.Min(total, vertical ? ActionCount > 0 ? Theme.TabHeight : 0 : ActionCount * 23);
            double available = Math.Max(0, total - actions);
            if (_plan is null || _arrangedRevision != _measuresRevision || _arrangedExtent != available || _arrangedOverflow != OverflowBehavior || _arrangedVertical != vertical)
            {
                _plan = TabLayoutEngine.Arrange(_measurements, available, OverflowBehavior); HeaderLayoutCount++;
                _arrangedRevision = _measuresRevision; _arrangedExtent = available; _arrangedOverflow = OverflowBehavior; _arrangedVertical = vertical;
                _slots.Clear();
                for (int l = 0; l < _plan.Lanes.Length; l++)
                    for (int j = 0; j < _plan.Lanes[l].Indices.Length; j++) _slots.Add(Items[_plan.Lanes[l].Indices[j]].Id, (l, j));
            }
            for (int i = 0; i < 3; i++)
            {
                var lane = _lanes[i]; var plan = _plan.Lanes[i]; lane.Layout = plan;
                lane.Offset = OverflowBehavior == TabOverflowBehavior.None ? 0 : Math.Clamp(lane.Offset, 0, Math.Max(0, plan.Metrics.Length - plan.Extent));
                var rect = vertical ? new DockRect(0, plan.Start, 155, plan.Extent) : new DockRect(plan.Start, 0, plan.Extent, Theme.TabHeight);
                DockChrome.Position(lane.View, rect); lane.View.Clip = new RectangleGeometry { Rect = new Rect(0, 0, rect.Width, rect.Height) };
            }
            DockChrome.Position(_actions, vertical ? new(0, available, 155, actions) : new(available, 0, actions, Theme.TabHeight));
            _header.Clip = new RectangleGeometry { Rect = vertical ? new Rect(0, 0, 155, total) : new Rect(0, 0, total, Theme.TabHeight) };
            if (reveal) RevealSelection(); RealizeHeaders();
        }
        finally { _arranging = false; }
    }
    private void RevealSelection()
    {
        if (_selectedId is null || OverflowBehavior == TabOverflowBehavior.None) return;
        if (_slots.TryGetValue(_selectedId, out var slot))
        {
            var lane = _lanes[slot.Lane];
            if (lane.Layout is { Extent: > 0 } plan) lane.Offset = plan.Metrics.BringIntoView(slot.Slot, lane.Offset, plan.Extent);
        }
    }
    private void ScrollLane(Lane lane, double distance)
    {
        if (lane.Layout is not { } plan || !double.IsFinite(distance)) return;
        lane.Offset = Math.Clamp(lane.Offset + distance, 0, Math.Max(0, plan.Metrics.Length - plan.Extent));
        RealizeHeaders();
    }
    private Lane ActiveScrollLane()
    {
        var selected = _selectedId is null ? null : _byId.GetValueOrDefault(_selectedId);
        var lane = selected is null ? _lanes[1] : _lanes[TabLayoutEngine.LaneIndex(Mode(selected))];
        return lane.Layout?.IsOverflowing == true ? lane : _lanes.FirstOrDefault(l => l.Layout?.IsOverflowing == true) ?? lane;
    }
    private void ScrollActiveLane(int direction)
    {
        var lane = ActiveScrollLane();
        ScrollLane(lane, direction * Math.Max(40, (lane.Layout?.Extent ?? 200) * .7));
    }
    private void RealizeHeaders()
    {
        if (_itemsDirty || _plan is null || _measurements.Length != Items.Count) return;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        var visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lane in _lanes)
        {
            if (lane.Layout is not { } plan || plan.Extent <= 0 || !IsTabStripVisible) continue;
            var range = plan.Metrics.VisibleRange(lane.Offset, plan.Extent, 100);
            for (int i = range.First; i < range.First + range.Count; i++)
            {
                var item = Items[plan.Indices[i]]; visible.Add(item.Id);
                if (!_headers.TryGetValue(item.Id, out var header)) { header = CreateHeader(item); _headers.Add(item.Id, header); lane.View.Children.Add(header.Border); }
                double start = plan.Metrics.Offsets[i] - lane.Offset, size = plan.Metrics.Offsets[i + 1] - plan.Metrics.Offsets[i];
                var rect = vertical ? new DockRect(0, start, 155, size) : new DockRect(start, 0, size, Theme.TabHeight);
                DockChrome.Position(header.Border, rect);
                header.Border.Clip = new RectangleGeometry { Rect = new Rect(0, 0, rect.Width, rect.Height) };
                // Reserve a real label target when shrinking; actions that cannot fit
                // collapse instead of consuming the complete selection surface.
                int capacity = Math.Clamp((int)Math.Floor(Math.Max(0, rect.Width - 30) / 23), 0, header.Actions.Count);
                for (int action = 0; action < header.Actions.Count; action++)
                    header.Actions[action].Visibility = action >= header.Actions.Count - capacity ? Visibility.Visible : Visibility.Collapsed;
                PaintHeader(header, item);
            }
        }
        foreach (var id in _headers.Keys.Where(id => !visible.Contains(id)).ToArray())
        { var border = _headers[id].Border; if (border.Parent is Panel parent) parent.Children.Remove(border); _headers.Remove(id); }
        var active = ActiveScrollLane();
        foreach (var button in _actions.Children.OfType<Button>())
            button.IsEnabled = (button.Tag as string) switch
            {
                "back" => active.Offset > .01,
                "forward" => active.Layout is { } layout && active.Offset + layout.Extent < layout.Metrics.Length - .01,
                "overflow" => Items.Count > 0,
                _ => true
            };
    }
    public bool TryGetInsertionIndex(Point point, out int index)
    {
        index = -1; if (!IsTabStripVisible || _plan is null) return false;
        bool vertical = Placement is TabPlacement.Left or TabPlacement.Right;
        var local = TransformToVisual(_header).TransformPoint(point);
        double axis = vertical ? local.Y : local.X, cross = vertical ? local.X : local.Y;
        if (cross < 0 || cross >= (vertical ? 155 : Theme.TabHeight) || axis < 0 || axis >= _plan.Extent) return false;
        foreach (var lane in _lanes)
        {
            if (lane.Layout is not { } plan || axis < plan.Start || axis >= plan.Start + plan.Extent) continue;
            int slot = plan.Metrics.InsertionIndex(axis - plan.Start + lane.Offset);
            if (slot < plan.Indices.Length) index = plan.Indices[slot];
            else if (plan.Indices.Length > 0) index = plan.Indices[^1] + 1;
            else index = _plan.Lanes.Take(TabLayoutEngine.LaneIndex(plan.Mode)).Sum(l => l.Indices.Length);
            return true;
        }
        return false;
    }
    public void ShowOverflowMenu()
    {
        var menu = new MenuFlyout();
        var order = _plan?.Lanes.SelectMany(l => l.Indices) ?? Enumerable.Range(0, Items.Count);
        foreach (int index in order)
        {
            var item = Items[index]; string id = item.Id;
            var option = DockChrome.Identify(new MenuFlyoutItem { Text = (id == _selectedId ? "✓  " : "    ") + item.Header, MinHeight = 24, FontSize = Theme.FontSize, Padding = new(8, 3, 8, 3) }, "tab-list:" + id);
            option.Click += (_, _) => Select(id, true); menu.Items.Add(option);
        }
        var args = new TabMenuOpeningEventArgs(menu); MenuOpening?.Invoke(this, args);
        if (!args.Cancel) menu.ShowAt(_actions);
    }
}
