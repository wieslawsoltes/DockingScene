using System.Runtime.CompilerServices;
using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

public partial class DockSite
{
    private readonly ConditionalWeakTable<WindowControl, FloatingResizeGrips> _floatingResizeGrips = new();
    private void RenderFloatingResizeGrips(IReadOnlyDictionary<string, int> z)
    {
        foreach (var (id, frame) in _floats)
        {
            if (!frame.CanResize || frame.IsMaximized || frame.IsMinimized || !z.TryGetValue(id, out int order)) continue;
            if (!_floatingResizeGrips.TryGetValue(frame, out var grips))
            {
                grips = new FloatingResizeGrips(this, frame, id);
                _floatingResizeGrips.Add(frame, grips);
            }
            grips.Render(order + 4);
        }
    }
    // Floating groups are siblings above their frame. Retained border-only hit
    // targets sit above those groups, not a full-window input-blocking overlay.
    private sealed class FloatingResizeGrips
    {
        private static readonly (DockSide X, DockSide Y)[] Edges =
        [
            (DockSide.Left, DockSide.Top), (DockSide.Center, DockSide.Top), (DockSide.Right, DockSide.Top),
            (DockSide.Left, DockSide.Center), (DockSide.Right, DockSide.Center),
            (DockSide.Left, DockSide.Bottom), (DockSide.Center, DockSide.Bottom), (DockSide.Right, DockSide.Bottom)
        ];
        private readonly DockSite _site;
        private readonly DockManager _manager;
        private readonly WindowControl _frame;
        private readonly string _id;
        private readonly Border[] _grips = new Border[8];
        private readonly string[] _keys = new string[8];
        private int _z;
        internal FloatingResizeGrips(DockSite site, WindowControl frame, string id)
        {
            _site = site; _manager = site.Manager; _frame = frame; _id = id;
            for (int i = 0; i < Edges.Length; i++)
            {
                var (x, y) = Edges[i];
                _keys[i] = "floating-grip:" + id + ":" + i;
                var grip = DockChrome.Identify(new Border { Background = DockChrome.Brush("#00000000") },
                    "floating-resize:" + id + ":" + x + ":" + y, "Resize floating window");
                grip.PointerPressed += (_, e) =>
                {
                    if (Current() && grip.IsLoaded && _frame.BeginPointerResize(e, x, y)) e.Handled = true;
                };
                _grips[i] = grip;
            }
            frame.VisualBoundsChanged += UpdateGeometry;
        }
        private bool Current() => ReferenceEquals(_site.Manager, _manager) &&
            _site._floats.TryGetValue(_id, out var current) && ReferenceEquals(current, _frame);
        internal void Render(int z)
        {
            _z = z;
            for (int i = 0; i < _grips.Length; i++)
            {
                var grip = _grips[i];
                _site.UseAuxiliary(_keys[i], () => grip);
            }
            UpdateGeometry();
        }
        private void UpdateGeometry()
        {
            if (!Current()) return;
            var r = _frame.Bounds;
            // Four inner units plus a two-unit outer tolerance remain usable
            // after device-pixel rounding. The DockSite clips at its own edges.
            double left = r.X - 2, top = r.Y - 2, width = r.Width + 4, height = r.Height + 4;
            double cw = Math.Min(10, width / 2), ch = Math.Min(10, height / 2);
            for (int i = 0; i < Edges.Length; i++)
            {
                var (x, y) = Edges[i];
                bool corner = x != DockSide.Center && y != DockSide.Center;
                double w = x == DockSide.Center ? Math.Max(0, width - 2 * cw) : corner ? cw : Math.Min(6, width);
                double h = y == DockSide.Center ? Math.Max(0, height - 2 * ch) : corner ? ch : Math.Min(6, height);
                double px = x == DockSide.Left ? left : x == DockSide.Right ? left + width - w : left + cw;
                double py = y == DockSide.Top ? top : y == DockSide.Bottom ? top + height - h : top + ch;
                DockChrome.Position(_grips[i], new(px, py, w, h), _z);
            }
        }
    }
}
