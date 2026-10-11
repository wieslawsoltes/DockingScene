using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace DockingScene.Controls;

public partial class DockSite
{
    private string? _autoResizeId;
    private DockSide _autoResizeSide;
    private double _autoResizeSize;
    private long _autoResizeRevision;
    private Border? _autoGrip;
    private void RenderAutoHideResizeGrip()
    {
        _autoGrip = null;
        if (_autoOpen is not { } id || _autoPopup is null || Manager.Layout.IsLayoutLocked || !Manager.Allows(id, DockCapabilities.Resize)) return;
        var group = Manager.Layout.AutoHideGroups.FirstOrDefault(group => group.Windows.Contains(id));
        if (group is null) return;
        var grip = UseAuxiliary("auto-resize:" + id, () =>
        {
            var element = DockChrome.Identify(new Border(), "autohide-resize:" + id, "Resize auto-hide popup");
            element.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed || Manager.Layout.IsLayoutLocked || !Manager.Allows(id, DockCapabilities.Resize)) return;
                var current = Manager.Layout.AutoHideGroups.FirstOrDefault(g => g.Windows.Contains(id));
                if (current is null || _autoOpen != id || !_canvas.CapturePointer(e.Pointer)) return;
                _hoverClose.Stop(); _hoverOpen.Stop(); _autoResizeId = id; _autoResizeSide = current.Side;
                _autoResizeSize = Manager.Layout.Windows[id].AutoHideSize; _autoResizeRevision = Manager.Layout.Revision;
                e.Handled = true;
            };
            return element;
        });
        _autoGrip = grip; grip.Background = DockChrome.Brush(Theme.Border);
        DockChrome.Position(grip, AutoHideGripBounds(_autoBounds, group.Side), 6000);
    }

    private static DockRect AutoHideGripBounds(DockRect bounds, DockSide side) => side switch
    {
        DockSide.Left => new(bounds.Right - 5, bounds.Y, 5, bounds.Height),
        DockSide.Right => new(bounds.X, bounds.Y, 5, bounds.Height),
        DockSide.Top => new(bounds.X, bounds.Bottom - 5, bounds.Width, 5),
        _ => new(bounds.X, bounds.Y, bounds.Width, 5)
    };
    private bool MoveAutoHideResize(Point point)
    {
        if (_autoResizeId is not { } id || _autoPopup is null || _geometry is null) return false;
        _hoverClose.Stop();
        var area = _geometry.WorkspaceBounds; var window = Manager.Layout.Windows[id];
        bool horizontal = _autoResizeSide is DockSide.Left or DockSide.Right;
        double maximum = Math.Max(40, Math.Min(horizontal ? area.Width : area.Height, horizontal ? window.MaximumSize.Width : window.MaximumSize.Height));
        double minimum = Math.Min(maximum, Math.Max(40, horizontal ? window.MinimumSize.Width : window.MinimumSize.Height));
        double requested = _autoResizeSide switch { DockSide.Left => point.X - area.X, DockSide.Right => area.Right - point.X, DockSide.Top => point.Y - area.Y, _ => area.Bottom - point.Y };
        _autoResizeSize = Math.Clamp(requested, minimum, maximum);
        _autoBounds = _autoResizeSide switch
        {
            DockSide.Left => area with { Width = _autoResizeSize },
            DockSide.Right => new(area.Right - _autoResizeSize, area.Y, _autoResizeSize, area.Height),
            DockSide.Top => area with { Height = _autoResizeSize },
            _ => new(area.X, area.Bottom - _autoResizeSize, area.Width, _autoResizeSize)
        };
        DockChrome.Position(_autoPopup, _autoBounds, 5000);
        if (_autoGrip is not null) DockChrome.Position(_autoGrip, AutoHideGripBounds(_autoBounds, _autoResizeSide), 6000);
        return true;
    }
    private bool EndAutoHideResize(PointerRoutedEventArgs e)
    {
        if (_autoResizeId is not { } id) return false;
        _autoResizeId = null; _canvas.ReleasePointerCapture(e.Pointer);
        Submit(Manager.Layout.Revision == _autoResizeRevision ? Manager.SetAutoHideSize(id, _autoResizeSize) : new(false, Manager.Layout.Revision, "The layout changed while resizing."));
        QueueRender(); e.Handled = true; return true;
    }
}
