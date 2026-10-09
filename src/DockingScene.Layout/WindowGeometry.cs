using System.Collections.Immutable;
using DockingScene.Core;

namespace DockingScene.Layout;

public enum MdiArrangement { Cascade, TileHorizontally, TileVertically, TileGrid }

public static class WindowGeometry
{
    public static DockRect KeepVisible(DockRect rect, DockRect viewport, double visibleTitle = 24)
    {
        if (!rect.IsValid) throw new ArgumentException("Invalid window bounds.");
        double keep = Math.Min(visibleTitle, Math.Min(viewport.Width, viewport.Height));
        return rect with
        {
            X = Math.Clamp(rect.X, viewport.X - rect.Width + keep, Math.Max(viewport.X - rect.Width + keep, viewport.Right - keep)),
            Y = Math.Clamp(rect.Y, viewport.Y, Math.Max(viewport.Y, viewport.Bottom - keep))
        };
    }

    public static DockRect Resize(DockRect start, double dx, double dy, DockSide horizontal, DockSide vertical, DockSize minimum, DockSize maximum)
    {
        double width = Math.Clamp(start.Width + (horizontal == DockSide.Left ? -dx : horizontal == DockSide.Right ? dx : 0), minimum.Width, maximum.Width);
        double height = Math.Clamp(start.Height + (vertical == DockSide.Top ? -dy : vertical == DockSide.Bottom ? dy : 0), minimum.Height, maximum.Height);
        return new(horizontal == DockSide.Left ? start.Right - width : start.X, vertical == DockSide.Top ? start.Bottom - height : start.Y, width, height);
    }

    public static DockRect Snap(DockRect rect, IEnumerable<DockRect> neighbors, double threshold = 8)
    {
        if (threshold < 0 || !double.IsFinite(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
        double dx = threshold + 1, dy = threshold + 1;
        void CandidateX(double delta) { if (Math.Abs(delta) <= threshold && Math.Abs(delta) < Math.Abs(dx)) dx = delta; }
        void CandidateY(double delta) { if (Math.Abs(delta) <= threshold && Math.Abs(delta) < Math.Abs(dy)) dy = delta; }
        foreach (var other in neighbors)
        {
            if (rect.Bottom + threshold >= other.Y && rect.Y - threshold <= other.Bottom)
            {
                CandidateX(other.X - rect.X); CandidateX(other.Right - rect.X);
                CandidateX(other.X - rect.Right); CandidateX(other.Right - rect.Right);
            }
            if (rect.Right + threshold >= other.X && rect.X - threshold <= other.Right)
            {
                CandidateY(other.Y - rect.Y); CandidateY(other.Bottom - rect.Y);
                CandidateY(other.Y - rect.Bottom); CandidateY(other.Bottom - rect.Bottom);
            }
        }
        return rect with { X = rect.X + (Math.Abs(dx) <= threshold ? dx : 0), Y = rect.Y + (Math.Abs(dy) <= threshold ? dy : 0) };
    }

    public static ImmutableDictionary<string, DockRect> ArrangeMdi(IReadOnlyList<string> ids, DockRect area, MdiArrangement arrangement, double titleHeight = 26)
    {
        var result = ImmutableDictionary.CreateBuilder<string, DockRect>();
        if (ids.Count == 0) return result.ToImmutable();
        if (!area.IsValid) throw new ArgumentException("MDI area must be positive.");
        if (arrangement == MdiArrangement.Cascade)
        {
            double step = Math.Max(1, titleHeight);
            int stagger = Math.Max(1, Math.Min(ids.Count, (int)(Math.Min(area.Width, area.Height) / (step * 3))));
            double width = Math.Max(1, area.Width - step * (stagger - 1));
            double height = Math.Max(1, area.Height - step * (stagger - 1));
            for (int i = 0; i < ids.Count; i++) result[ids[i]] = new(area.X + i % stagger * step, area.Y + i % stagger * step, width, height);
        }
        else
        {
            int columns = arrangement == MdiArrangement.TileHorizontally ? 1 : arrangement == MdiArrangement.TileVertically ? ids.Count : (int)Math.Ceiling(Math.Sqrt(ids.Count));
            int rows = (int)Math.Ceiling((double)ids.Count / columns);
            for (int i = 0; i < ids.Count; i++)
            {
                int row = i / columns, col = i % columns;
                int inRow = Math.Min(columns, ids.Count - row * columns);
                double x = area.X + area.Width * col / inRow, right = area.X + area.Width * (col + 1) / inRow;
                double y = area.Y + area.Height * row / rows, bottom = area.Y + area.Height * (row + 1) / rows;
                result[ids[i]] = new(x, y, right - x, bottom - y);
            }
        }
        return result.ToImmutable();
    }

    public static DockResult ArrangeMdi(DockManager manager, DockRect area, MdiArrangement arrangement)
    {
        var snapshot = manager.Layout;
        var ids = DockTree.Descendants(snapshot.Root).OfType<DockWorkspace>().SelectMany(w => DockTree.WindowIds(w.Content)).ToArray();
        if (snapshot.IsLayoutLocked || ids.Any(id => !manager.Allows(id, DockCapabilities.Resize))) return new(false, snapshot.Revision, "MDI arrangement is not allowed.");
        var placements = ArrangeMdi(ids, area, arrangement);
        var windows = snapshot.Windows;
        foreach (var (id, rect) in placements) windows = windows.SetItem(id, windows[id] with { MdiBounds = rect, MdiState = MdiWindowState.Normal });
        return manager.Restore(snapshot with { Windows = windows, MdiMode = MdiMode.Standard }, snapshot.Revision);
    }
}
