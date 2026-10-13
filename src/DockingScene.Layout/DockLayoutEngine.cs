using System.Collections.Immutable;
using DockingScene.Core;

namespace DockingScene.Layout;

public sealed record DockMetrics
{
    /// <summary>Immutable defaults shared by allocation-free omitted-metrics calls.</summary>
    public static DockMetrics Default { get; } = new();
    /// <summary>Cross-axis extent used by the native vertical tab strip.</summary>
    public const double VerticalTabStripWidth = 155;
    public double Splitter { get; init; } = 5;
    public double TabHeight { get; init; } = 26;
    public double TitleHeight { get; init; } = 24;
    public double AutoHideRail { get; init; } = 25;
    public double Border { get; init; } = 1;
    public double DpiScale { get; init; } = 1;
    public bool ToolWindowsHaveTitleBars { get; init; } = true;
}
public readonly record struct DockMeasure(DockSize Minimum, DockSize Maximum);
public sealed record SplitterGeometry(string Id, DockRect Bounds, DockRect ParentBounds, SplitAxis Axis, double Ratio);
public sealed record DockGeometry(
    DockRect Bounds, DockRect WorkspaceBounds,
    ImmutableDictionary<string, DockRect> Nodes,
    ImmutableArray<SplitterGeometry> Splitters,
    ImmutableDictionary<DockSide, DockRect> AutoHideRails,
    ImmutableDictionary<string, DockRect> FloatingHosts,
    ImmutableArray<string> ConstraintWarnings);

/// <summary>Linear-time two-pass layout. When the viewport cannot satisfy minimums it shrinks proportionally, never producing negative sizes.</summary>
public static partial class DockLayoutEngine
{
    public static DockGeometry Arrange(DockLayout layout, DockRect bounds, DockMetrics? metrics = null)
    {
        metrics ??= DockMetrics.Default;
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || bounds.Width < 0 || bounds.Height < 0 || !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom))
            throw new ArgumentException("Invalid viewport.", nameof(bounds));
        ValidateMetrics(metrics);
        var nodes = ImmutableDictionary.CreateBuilder<string, DockRect>();
        var splitters = ImmutableArray.CreateBuilder<SplitterGeometry>();
        var floats = ImmutableDictionary.CreateBuilder<string, DockRect>();
        var rails = ImmutableDictionary.CreateBuilder<DockSide, DockRect>();
        var warnings = ImmutableArray.CreateBuilder<string>();
        var measures = new Dictionary<string, DockMeasure>(StringComparer.Ordinal);
        double Round(double v) => double.IsFinite(v * metrics.DpiScale) ? Math.Round(v * metrics.DpiScale) / metrics.DpiScale : v;
        DockMeasure Measure(DockNode node)
        {
            DockMeasure result;
            switch (node)
            {
                case DockGroup g:
                    result = MeasureGroupCore(layout, g, metrics);
                    break;
                case DockWorkspace w:
                    result = w.Content is null ? new(new(80, 60), DockSize.Maximum) : Measure(w.Content);
                    break;
                case DockSplit s:
                    var a = Measure(s.First); var b = Measure(s.Second);
                    result = s.Axis == SplitAxis.Horizontal
                        ? new(new(a.Minimum.Width + b.Minimum.Width + metrics.Splitter, Math.Max(a.Minimum.Height, b.Minimum.Height)), new(a.Maximum.Width + b.Maximum.Width + metrics.Splitter, Math.Min(a.Maximum.Height, b.Maximum.Height)))
                        : new(new(Math.Max(a.Minimum.Width, b.Minimum.Width), a.Minimum.Height + b.Minimum.Height + metrics.Splitter), new(Math.Min(a.Maximum.Width, b.Maximum.Width), a.Maximum.Height + b.Maximum.Height + metrics.Splitter));
                    break;
                default: throw new InvalidOperationException("Unknown layout node.");
            }
            if (!result.Minimum.IsFinite || !result.Maximum.IsFinite) throw new InvalidOperationException("Layout size arithmetic overflowed.");
            measures[node.Id] = result;
            return result;
        }
        void Place(DockNode? node, DockRect rect)
        {
            if (node is null) return;
            nodes[node.Id] = rect;
            if (node is DockWorkspace workspace) { Place(workspace.Content, rect); return; }
            if (node is not DockSplit split) return;
            bool horizontal = split.Axis == SplitAxis.Horizontal;
            double extent = horizontal ? rect.Width : rect.Height;
            double gap = Math.Min(metrics.Splitter, extent);
            double available = Math.Max(0, extent - gap);
            var first = measures[split.First.Id]; var second = measures[split.Second.Id];
            double minA = horizontal ? first.Minimum.Width : first.Minimum.Height;
            double minB = horizontal ? second.Minimum.Width : second.Minimum.Height;
            double maxA = horizontal ? first.Maximum.Width : first.Maximum.Height;
            double maxB = horizontal ? second.Maximum.Width : second.Maximum.Height;
            double size;
            if (minA + minB > available)
            {
                size = minA + minB == 0 ? 0 : available * (minA / (minA + minB));
                warnings.Add(split.Id + ": viewport is smaller than combined minimum sizes");
            }
            else
            {
                double low = Math.Max(minA, available - maxB), high = Math.Min(maxA, available - minB);
                if (low > high)
                {
                    low = minA; high = Math.Max(low, available - minB);
                    warnings.Add(split.Id + ": maximum sizes cannot fill the viewport");
                }
                size = Math.Clamp(available * split.Ratio, low, high);
            }
            size = Math.Clamp(Round(size), 0, available);
            var r1 = horizontal ? new DockRect(rect.X, rect.Y, size, rect.Height) : new DockRect(rect.X, rect.Y, rect.Width, size);
            var bar = horizontal ? new DockRect(rect.X + size, rect.Y, gap, rect.Height) : new DockRect(rect.X, rect.Y + size, rect.Width, gap);
            var r2 = horizontal ? new DockRect(bar.Right, rect.Y, available - size, rect.Height) : new DockRect(rect.X, bar.Bottom, rect.Width, available - size);
            splitters.Add(new(split.Id, bar, rect, split.Axis, available == 0 ? .5 : size / available));
            Place(split.First, r1); Place(split.Second, r2);
        }
        var work = bounds;
        foreach (var side in new[] { DockSide.Left, DockSide.Right, DockSide.Top, DockSide.Bottom })
        {
            if (!layout.AutoHideGroups.Any(g => g.Side == side)) continue;
            double n = Math.Min(metrics.AutoHideRail, side is DockSide.Left or DockSide.Right ? work.Width : work.Height);
            var rail = side switch
            {
                DockSide.Left => new DockRect(work.X, work.Y, n, work.Height),
                DockSide.Right => new DockRect(work.Right - n, work.Y, n, work.Height),
                DockSide.Top => new DockRect(work.X, work.Y, work.Width, n),
                _ => new DockRect(work.X, work.Bottom - n, work.Width, n)
            };
            rails[side] = rail;
            work = side switch
            {
                DockSide.Left => new(work.X + n, work.Y, work.Width - n, work.Height),
                DockSide.Right => new(work.X, work.Y, work.Width - n, work.Height),
                DockSide.Top => new(work.X, work.Y + n, work.Width, work.Height - n),
                _ => new(work.X, work.Y, work.Width, work.Height - n)
            };
        }
        if (layout.Root is not null) { Measure(layout.Root); Place(layout.Root, work); }
        foreach (var host in layout.FloatingHosts)
        {
            var measure = Measure(host.Root);
            var rect = host.IsMaximized ? work : WindowGeometry.KeepVisible(host.Bounds, bounds, metrics.TitleHeight);
            floats[host.Id] = rect;
            var content = new DockRect(rect.X + metrics.Border, rect.Y + metrics.TitleHeight, Math.Max(0, rect.Width - 2 * metrics.Border), Math.Max(0, rect.Height - metrics.TitleHeight - metrics.Border));
            if (content.Width < measure.Minimum.Width || content.Height < measure.Minimum.Height) warnings.Add(host.Id + ": floating host is below minimum size");
            Place(host.Root, content);
        }
        return new(bounds, work, nodes.ToImmutable(), splitters.ToImmutable(), rails.ToImmutable(), floats.ToImmutable(), warnings.ToImmutable());
    }

    public static double RatioAt(SplitterGeometry splitter, double x, double y, double thickness = 5)
    {
        double size = splitter.Axis == SplitAxis.Horizontal ? splitter.ParentBounds.Width : splitter.ParentBounds.Height;
        double position = splitter.Axis == SplitAxis.Horizontal ? x - splitter.ParentBounds.X : y - splitter.ParentBounds.Y;
        return Math.Clamp(position / Math.Max(1, size - thickness), .01, .99);
    }
}
