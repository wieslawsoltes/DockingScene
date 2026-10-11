using System.Collections.Immutable;
using DockingScene.Core;

namespace DockingScene.Layout;

public enum MdiArrangement { Cascade, TileHorizontally, TileVertically, TileGrid }

public static partial class WindowGeometry
{
    public static DockRect KeepVisible(DockRect rect, DockRect viewport, double visibleTitle = 24)
    {
        ValidateRect(rect);
        ValidateViewport(viewport);
        if (!double.IsFinite(visibleTitle) || visibleTitle < 0) throw new ArgumentOutOfRangeException(nameof(visibleTitle));
        double keep = Math.Min(visibleTitle, Math.Min(viewport.Width, viewport.Height));
        return Checked(rect with
        {
            X = Math.Clamp(rect.X, viewport.X - rect.Width + keep, Math.Max(viewport.X - rect.Width + keep, viewport.Right - keep)),
            Y = Math.Clamp(rect.Y, viewport.Y, Math.Max(viewport.Y, viewport.Bottom - keep))
        });
    }

    public static DockRect Resize(DockRect start, double dx, double dy, DockSide horizontal, DockSide vertical, DockSize minimum, DockSize maximum)
    {
        ValidateRect(start);
        if (!double.IsFinite(dx) || !double.IsFinite(dy) ||
            horizontal is not (DockSide.Center or DockSide.Left or DockSide.Right) ||
            vertical is not (DockSide.Center or DockSide.Top or DockSide.Bottom))
            throw new ArgumentException("Invalid resize input.");
        if (!minimum.IsFinite || !maximum.IsFinite || minimum.Width < 0 || minimum.Height < 0 ||
            maximum.Width <= 0 || maximum.Height <= 0 || maximum.Width < minimum.Width || maximum.Height < minimum.Height)
            throw new ArgumentException("Invalid resize constraints.");
        double width = Math.Clamp(start.Width + (horizontal == DockSide.Left ? -dx : horizontal == DockSide.Right ? dx : 0), minimum.Width, maximum.Width);
        double height = Math.Clamp(start.Height + (vertical == DockSide.Top ? -dy : vertical == DockSide.Bottom ? dy : 0), minimum.Height, maximum.Height);
        return Checked(new(horizontal == DockSide.Left ? start.Right - width : start.X, vertical == DockSide.Top ? start.Bottom - height : start.Y, width, height));
    }

    public static DockRect Snap(DockRect rect, IEnumerable<DockRect> neighbors, double threshold = 8)
    {
        ValidateRect(rect);
        ArgumentNullException.ThrowIfNull(neighbors);
        if (threshold < 0 || !double.IsFinite(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
        double dx = threshold + 1, dy = threshold + 1;
        void CandidateX(double delta) { if (Math.Abs(delta) <= threshold && Math.Abs(delta) < Math.Abs(dx)) dx = delta; }
        void CandidateY(double delta) { if (Math.Abs(delta) <= threshold && Math.Abs(delta) < Math.Abs(dy)) dy = delta; }
        foreach (var other in neighbors)
        {
            ValidateRect(other);
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
        return Checked(rect with { X = rect.X + (Math.Abs(dx) <= threshold ? dx : 0), Y = rect.Y + (Math.Abs(dy) <= threshold ? dy : 0) });
    }

    public static ImmutableDictionary<string, DockRect> ArrangeMdi(IReadOnlyList<string> ids, DockRect area, MdiArrangement arrangement, double titleHeight = 26)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ValidateViewport(area);
        if (!Enum.IsDefined(arrangement) || !double.IsFinite(titleHeight) || titleHeight <= 0)
            throw new ArgumentException("Invalid MDI arrangement options.");
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
            if (string.IsNullOrWhiteSpace(id) || !unique.Add(id)) throw new ArgumentException("MDI ids must be nonempty and unique.");
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
                double x = area.X + area.Width * ((double)col / inRow), right = area.X + area.Width * ((double)(col + 1) / inRow);
                double y = area.Y + area.Height * ((double)row / rows), bottom = area.Y + area.Height * ((double)(row + 1) / rows);
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
