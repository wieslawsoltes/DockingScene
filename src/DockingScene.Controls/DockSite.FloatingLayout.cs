using DockingScene.Core;
using DockingScene.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

public partial class DockSite
{
    private readonly Dictionary<WindowControl, FloatingProjection> _floatingLayouts = new();
    public long FloatingLayoutPlanBuildCount { get; private set; }
    public long FloatingLayoutArrangeCount { get; private set; }
    public int ActiveFloatingLayoutPlanCount => _floatingLayouts.Count;
    public static readonly DependencyProperty IsLiveFloatingWindowLayoutEnabledProperty = DependencyProperty.Register(
        nameof(IsLiveFloatingWindowLayoutEnabled), typeof(bool), typeof(DockSite),
        new PropertyMetadata(true, (d, _) =>
        {
            var site = (DockSite)d;
            site.CancelFloatingGeometry();
            site.QueueRender();
        }));
    /// <summary>Reflow retained contents during pointer/keyboard frame gestures.
    /// False provides a frame-only preview for expensive application content.</summary>
    public bool IsLiveFloatingWindowLayoutEnabled
    {
        get => (bool)GetValue(IsLiveFloatingWindowLayoutEnabledProperty);
        set => SetValue(IsLiveFloatingWindowLayoutEnabledProperty, value);
    }
    private bool BeginFloatingLayout(WindowControl frame, string hostId)
    {
        EnsureFloatingInputGuards();
        if (!IsLiveFloatingWindowLayoutEnabled) return true;
        try
        {
            var projection = new FloatingProjection(this, hostId);
            _floatingLayouts[frame] = projection;
            FloatingLayoutPlanBuildCount++;
            return true;
        }
        catch (ArgumentException error)
        {
            frame.CancelMovement();
            OperationRejected?.Invoke(error.Message);
            return false;
        }
    }
    private void UpdateFloatingLayout(WindowControl frame)
    {
        if (!_floatingLayouts.TryGetValue(frame, out var projection)) return;
        if (!projection.IsCurrent(this))
        {
            frame.CancelMovement();
            _floatingLayouts.Remove(frame);
            QueueRender();
            return;
        }
        projection.Apply(frame.Bounds);
        FloatingLayoutArrangeCount++;
    }
    private void EndFloatingLayout(WindowControl frame, string hostId, long completedVersion)
    {
        // A BoundsChanged subscriber may start another keyboard gesture before
        // the preceding completion's finally runs. Only release our own epoch.
        if (frame.MovementVersion != completedVersion) return;
        _floatingLayouts.Remove(frame);
        _floatingGestureRevisions.Remove(hostId);
        QueueRender();
    }
    private void CancelInvalidFloatingLayouts()
    {
        // Iterate retained frames, not the plan dictionary: cancellation removes plans.
        foreach (var frame in _floats.Values)
            if (_floatingLayouts.TryGetValue(frame, out var projection) && !projection.IsCurrent(this))
                frame.CancelMovement();
    }
    private void RefreshFloatingLayouts()
    {
        // A color-only refresh must not reset live content to committed geometry.
        foreach (var frame in _floats.Values)
            if (_floatingLayouts.ContainsKey(frame)) UpdateFloatingLayout(frame);
    }
    private sealed class FloatingProjection
    {
        private readonly DockLayout _snapshot;
        private readonly DockManager _manager;
        private readonly FloatingLayoutPlan _plan;
        private readonly DockMetrics _metrics;
        private readonly DockRect[] _nodes, _splitters;
        private readonly FrameworkElement?[] _views, _splitterViews;
        internal FloatingProjection(DockSite site, string hostId)
        {
            _manager = site.Manager; _snapshot = _manager.Layout;
            _metrics = new() { TabHeight = site.Theme.TabHeight, TitleHeight = site.Theme.TitleHeight,
                Splitter = site.Theme.SplitterSize, DpiScale = site.XamlRoot?.RasterizationScale ?? 1,
                ToolWindowsHaveTitleBars = site.ToolWindowsHaveTitleBars };
            _plan = new(_snapshot, hostId, _metrics);
            _nodes = new DockRect[_plan.NodeCount]; _splitters = new DockRect[_plan.SplitterCount];
            _views = new FrameworkElement?[_plan.NodeCount]; _splitterViews = new FrameworkElement?[_plan.SplitterCount];
            for (int i = 0; i < _plan.NodeCount; i++)
            {
                string id = _plan.GetNodeId(i);
                if (site._groups.TryGetValue(id, out var group)) _views[i] = group;
                int splitter = _plan.GetSplitterIndex(i);
                if (splitter >= 0 && site._auxiliaryViews.TryGetValue("split:" + id, out var view))
                    _splitterViews[splitter] = view;
            }
        }
        internal bool IsCurrent(DockSite site) => ReferenceEquals(_manager, site.Manager) &&
            ReferenceEquals(_snapshot, _manager.Layout) && site.IsLiveFloatingWindowLayoutEnabled &&
            _metrics.ToolWindowsHaveTitleBars == site.ToolWindowsHaveTitleBars &&
            _metrics.TabHeight == site.Theme.TabHeight && _metrics.TitleHeight == site.Theme.TitleHeight &&
            _metrics.Splitter == site.Theme.SplitterSize && _metrics.DpiScale == (site.XamlRoot?.RasterizationScale ?? 1);
        internal void Apply(DockRect frame)
        {
            _plan.Arrange(frame, _nodes, _splitters);
            for (int i = 0; i < _views.Length; i++)
                if (_views[i] is { IsLoaded: true } view)
                    DockChrome.Position(view, _nodes[i], Canvas.GetZIndex(view));
            for (int i = 0; i < _splitterViews.Length; i++)
                if (_splitterViews[i] is { IsLoaded: true } view)
                    DockChrome.PositionComposed(view, _splitters[i], Canvas.GetZIndex(view));
        }
    }
}
