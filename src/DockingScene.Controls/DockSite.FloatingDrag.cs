using DockingScene.Core;
using DockingScene.Layout;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockSite
{
    private FloatingDragPlan? _floatingDragPlan;
    private readonly DockDragOverlay _dragFeedback = new();
    private bool _floatingInputGuards;
    private System.Collections.ObjectModel.ReadOnlyDictionary<string, WindowControl>? _floatingWindowView;
    public IReadOnlyDictionary<string, WindowControl> FloatingWindows => _floatingWindowView ??= new(_floats);
    public long FloatingDragPlanBuildCount { get; private set; }
    public long FloatingMagnetismQueryCount { get; private set; }
    public DockRect? RawFloatingDragBounds { get; private set; }
    public DockDragOverlay DragFeedback => _dragFeedback;
    private void EnsureFloatingInputGuards()
    {
        if (_floatingInputGuards) return; _floatingInputGuards = true;
        SizeChanged += (_, _) => CancelFloatingGeometry();
    }
    private void CancelFloatingGeometry()
    {
        if (!_dragIds.IsEmpty) CancelInteraction();
        foreach (var frame in _floats.Values) frame.CancelMovement();
        _floatingGestureRevisions.Clear();
    }
    private FloatingDragPlan CreateFloatingDragPlan(DockLayout layout, IReadOnlyList<string> ids, DockRect fallback)
    {
        EnsureFloatingInputGuards();
        var plan = new FloatingDragPlan(layout, ids, fallback, MagnetismSnapDistance, MagnetismGapDistance, _geometry?.FloatingHosts);
        FloatingDragPlanBuildCount++; return plan;
    }
    private DockRect ApplyFloatingDrag(DockRect raw, bool bypass)
    {
        if (_floatingDragPlan is null)
        {
            var initial = _dragBounds;
            _floatingDragPlan = CreateFloatingDragPlan(Manager.Layout,_dragIds,initial);
            _dragBounds = _floatingDragPlan.InitialBounds;
            raw = _dragBounds with { X = _dragBounds.X + raw.X - initial.X, Y = _dragBounds.Y + raw.Y - initial.Y };
        }
        RawFloatingDragBounds = raw;
        if (!bypass && _floatingDragPlan.Magnetism.SnapDistance > 0) FloatingMagnetismQueryCount++;
        return _floatingDragPlan.Move(raw,bypass);
    }
    private void ReleaseFloatingDragPlan()
    {
        _floatingDragPlan = null; RawFloatingDragBounds = null; _dragFeedback.ClearPreview();
    }
    private void UpdateDragFeedback(DockDragPreview preview)
    {
        if (_dragFeedback.Parent is null) { _overlay.Children.Clear(); _overlay.Children.Add(_dragFeedback); }
        _dragFeedback.Width = ActualWidth; _dragFeedback.Height = ActualHeight;
        _dragFeedback.Update(preview,Theme);
    }
}
