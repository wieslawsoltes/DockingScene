using System.Collections.Immutable;
using DockingScene.Core;
using DockingScene.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

public partial class DockSite
{
    private void BeginDockDrag(ImmutableArray<string> ids, PointerRoutedEventArgs e)
    {
        var manager = Manager;
        if (manager.Layout.IsLayoutLocked || ids.IsEmpty || ids.Any(id => !manager.Layout.Windows.ContainsKey(id) || !manager.Allows(id, DockCapabilities.Drag))) return;
        if (!e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed) return;
        var selected = DockTree.GroupOf(manager.Layout, ids[0])?.SelectedId;
        selected = selected is not null && ids.Contains(selected) ? selected : ids[0];
        if (manager.Layout.ActiveWindowId != selected)
        {
            var result = manager.Activate(selected); Submit(result); if (!result.Success) return;
        }
        // Activation and capture can run application code. Never use a preceding
        // manager's identities after it replaces or unregisters the source.
        if (!ReferenceEquals(manager, Manager)) return;
        var layout = manager.Layout;
        if (layout.IsLayoutLocked || ids.Any(id => !layout.Windows.ContainsKey(id) || !manager.Allows(id,DockCapabilities.Drag))) return;
        var start = e.GetCurrentPoint(_canvas).Position;
        var group = DockTree.GroupOf(layout, ids[0]);
        var rect = group is not null && _geometry?.Nodes.TryGetValue(group.Id, out var r) == true ? r : layout.Windows[ids[0]].FloatingBounds;
        FloatingDragPlan plan;
        try
        {
            plan = CreateFloatingDragPlan(layout, ids, new(start.X - Math.Min(100, rect.Width / 2), start.Y - 12,
                Math.Max(240, Math.Min(600, rect.Width)), Math.Max(180, Math.Min(460, rect.Height))));
        }
        catch (ArgumentException error) { OperationRejected?.Invoke(error.Message); return; }
        if (!CaptureInteractionPointer(e)) return;
        if (!ReferenceEquals(manager, Manager) || !ReferenceEquals(layout, manager.Layout)) { _canvas.ReleasePointerCapture(e.Pointer); return; }
        _dragStart = start; _dragIds = ids; _dragRevision = layout.Revision; _dragging = false; _tabDropIndex = -1;
        _floatingDragPlan = plan; _dragBounds = plan.InitialBounds;
        TraceInput("Begin", e); e.Handled = true;
    }
    private void BeginSplitDrag(SplitterGeometry split, PointerRoutedEventArgs e)
    {
        var node = DockTree.Find(Manager.Layout, split.Id);
        if (Manager.Layout.IsLayoutLocked || node is null || DockTree.WindowIds(node).Any(id => !Manager.Allows(id, DockCapabilities.Resize)) || !e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed) return;
        if (!CaptureInteractionPointer(e)) return;
        _splitDrag = split; _splitRatio = split.Ratio; _splitRevision = Manager.Layout.Revision;
        TraceInput("Begin", e); e.Handled = true;
    }
    private bool CaptureInteractionPointer(PointerRoutedEventArgs e)
    {
        if (IsInteractionActive) return false;
        // ButtonBase captures in its class handler before our routed handler runs.
        // Release only the same pointer within this event's descendant route.
        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, _canvas); node = VisualTreeHelper.GetParent(node))
            if (node is UIElement element && element.PointerCaptures?.Any(p => p.PointerId == e.Pointer.PointerId) == true)
                element.ReleasePointerCapture(e.Pointer);
        if (_canvas.CapturePointer(e.Pointer)) return true;
        TraceInput("Capture rejected", e);
        return false;
    }
}
