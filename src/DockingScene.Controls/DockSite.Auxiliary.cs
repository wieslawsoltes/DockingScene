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

public partial class DockSite
{
    private readonly DockGeometryCache _geometryCache = new();
    private readonly Dictionary<string, FrameworkElement> _auxiliaryViews = new(StringComparer.Ordinal);
    private readonly HashSet<string> _usedAuxiliaries = new(StringComparer.Ordinal);
    public long GeometryBuildCount => _geometryCache.BuildCount;
    public long GeometryReuseCount => _geometryCache.ReuseCount;
    public long AuxiliaryCreationCount { get; private set; }
    public int AuxiliaryCount => _auxiliaryViews.Count;
    public IReadOnlyDictionary<string, int> AuxiliaryIdentities => _auxiliaryViews.ToDictionary(p => p.Key, p => RuntimeHelpers.GetHashCode(p.Value));

    private T UseAuxiliary<T>(string id, Func<T> create) where T : FrameworkElement
    {
        _usedAuxiliaries.Add(id);
        if (_auxiliaryViews.TryGetValue(id, out var existing)) return (T)existing;
        var element = create(); _auxiliaryViews.Add(id, element); _canvas.Children.Add(element); AuxiliaryCreationCount++;
        return element;
    }
    private void PruneAuxiliaries()
    {
        foreach (string id in _auxiliaryViews.Keys.Where(id => !_usedAuxiliaries.Contains(id)).ToArray())
        {
            var element = _auxiliaryViews[id];
            if (element is ContentPresenter presenter) ContentLease.Set(presenter, null);
            _canvas.Children.Remove(element); _auxiliaryViews.Remove(id);
        }
    }
    private void ClearAuxiliaries() { _usedAuxiliaries.Clear(); PruneAuxiliaries(); _geometryCache.Clear(); }

    private void RenderSplitters(DockLayout layout, IReadOnlySet<string> standardIds, IReadOnlyDictionary<string, int> z)
    {
        if (_geometry is null) return;
        foreach (var split in _geometry.Splitters)
        {
            var node = DockTree.Find(layout, split.Id)!;
            var ids = DockTree.WindowIds(node).ToArray();
            if (ids.Length > 0 && ids.All(standardIds.Contains)) continue;
            var thumb = UseAuxiliary("split:" + split.Id, () =>
            {
                var button = DockChrome.Button("Resize splitter", "splitter:" + split.Id, Theme, () => { });
                button.Content = null; button.Padding = new(0);
                button.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
                { if (button.Tag is SplitterGeometry current) BeginSplitDrag(current, e); }), true);
                button.KeyDown += (_, e) =>
                {
                    int direction = e.Key is VirtualKey.Left or VirtualKey.Up ? -1 : e.Key is VirtualKey.Right or VirtualKey.Down ? 1 : 0;
                    if (direction == 0 || button.Tag is not SplitterGeometry current) return;
                    Submit(Manager.ResizeSplit(current.Id, current.Ratio + direction * .025)); e.Handled = true;
                };
                return button;
            });
            thumb.Tag = split; thumb.Background = DockChrome.Brush(Theme.Background);
            thumb.IsEnabled = !layout.IsLayoutLocked && ids.All(id => Manager.Allows(id, DockCapabilities.Resize));
            string? host = ids.Select(id => DockTree.Locate(layout, id).HostId).FirstOrDefault(id => id is not null);
            DockChrome.PositionComposed(thumb, split.Bounds, host is null ? 4 : z[host] + 3);
        }
        RenderFloatingResizeGrips(z);
    }

    private void RenderAutoHideRails(DockLayout layout)
    {
        if (_geometry is null) return;
        foreach (var (side, rail) in _geometry.AutoHideRails)
        {
            double offset = 4;
            foreach (var group in layout.AutoHideGroups.Where(g => g.Side == side))
            foreach (string id in group.Windows)
            {
                var button = UseAuxiliary("rail:" + id, () => new AutoHideRailButton(this, id));
                offset += button.Update(layout.Windows[id], Theme, side, rail, offset) + 5;
            }
        }
    }

    private sealed class AutoHideRailButton : Button
    {
        private readonly Canvas _labelCanvas = new();
        private readonly TextBlock _label = new() { TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly RotateTransform _rotation = new();
        private (string Title, DockTheme Theme)? _measured;
        private double _length;
        public AutoHideRailButton(DockSite owner, string id)
        {
            Content = _labelCanvas; _labelCanvas.Children.Add(_label); _label.RenderTransform = _rotation;
            MinWidth = MinHeight = 0; Padding = new(0); CornerRadius = new(0); BorderThickness = new(0, 0, 0, 2);
            HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
            DockChrome.Identify(this, "autohide:" + id);
            Click += (_, _) => owner.OpenAutoHide(id);
            PointerEntered += (_, _) => { owner._hoverClose.Stop(); if (owner.OpenAutoHideOnHover) { owner._hoverId = id; owner._hoverOpen.Start(); } };
            PointerExited += (_, _) => { owner._hoverOpen.Stop(); if (owner._autoOpen == id) owner._hoverClose.Start(); };
        }
        public double Update(DockWindow model, DockTheme theme, DockSide side, DockRect rail, double offset)
        {
            var signature = (model.Title, theme);
            if (_measured != signature)
            {
                _measured = signature; _label.Text = model.Title; _label.FontSize = theme.FontSize;
                _label.Width = _label.Height = double.NaN; _label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                _length = Math.Clamp(_label.DesiredSize.Width + 24, 80, 190);
                _label.Foreground = DockChrome.Brush(theme.Text); Background = DockChrome.Brush(theme.Background); BorderBrush = DockChrome.Brush(theme.Border);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, model.Title); ToolTipService.SetToolTip(this, model.Title);
            }
            bool vertical = side is DockSide.Left or DockSide.Right;
            double cross = vertical ? rail.Width : rail.Height;
            _rotation.Angle = vertical ? 90 : 0;
            _label.Width = _length; _label.Height = cross;
            Canvas.SetLeft(_label, vertical ? cross : 0); Canvas.SetTop(_label, 0);
            _labelCanvas.Width = vertical ? cross : _length; _labelCanvas.Height = vertical ? _length : cross;
            DockChrome.Position(this, vertical ? new(rail.X, rail.Y + offset, cross, _length) : new(rail.X + offset, rail.Y, _length, cross), 2000);
            return _length;
        }
    }
}
