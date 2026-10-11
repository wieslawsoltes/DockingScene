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
        if (Manager.Layout.IsLayoutLocked || ids.IsEmpty || ids.Any(id => !Manager.Allows(id, DockCapabilities.Drag))) return;
        if (!e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed) return;
        var selected = DockTree.GroupOf(Manager.Layout, ids[0])?.SelectedId;
        selected = selected is not null && ids.Contains(selected) ? selected : ids[0];
        if (Manager.Layout.ActiveWindowId != selected)
        {
            var result = Manager.Activate(selected); Submit(result); if (!result.Success) return;
        }
        if (!CaptureInteractionPointer(e)) return;
        _dragStart = e.GetCurrentPoint(_canvas).Position; _dragIds = ids; _dragRevision = Manager.Layout.Revision; _dragging = false; _tabDropIndex = -1;
        var group = DockTree.GroupOf(Manager.Layout, ids[0]);
        var rect = group is not null && _geometry?.Nodes.TryGetValue(group.Id, out var r) == true ? r : Manager.Layout.Windows[ids[0]].FloatingBounds;
        _dragBounds = new(_dragStart.X - Math.Min(100, rect.Width / 2), _dragStart.Y - 12, Math.Max(240, Math.Min(600, rect.Width)), Math.Max(180, Math.Min(460, rect.Height)));
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
        // Uno/WinUI rejects a competing explicit capture: release only the same
        // pointer from descendants in this event's route, then acquire it here.
        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, _canvas); node = VisualTreeHelper.GetParent(node))
            if (node is UIElement element && element.PointerCaptures?.Any(p => p.PointerId == e.Pointer.PointerId) == true)
                element.ReleasePointerCapture(e.Pointer);
        if (_canvas.CapturePointer(e.Pointer)) return true;
        TraceInput("Capture rejected", e);
        return false;
    }
}
