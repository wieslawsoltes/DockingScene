using System.Collections.Immutable;
using DockingScene.Core;

namespace DockingScene.Layout;

public enum TabOverflowBehavior { None, Menu, Scroll, ScrollWithMenu, Shrink, ShrinkWithMenu }
public enum SingleTabLayoutBehavior { Show, Hide }
public readonly record struct TabMeasurement(string Id, TabDisplayMode Mode, double Desired, double Minimum = 24, double Maximum = 300);
public sealed record TabLaneLayout(TabDisplayMode Mode, double Start, double Extent, ImmutableArray<int> Indices, TabStripMetrics Metrics)
{
    public bool IsOverflowing => Metrics.Length > Extent + .01;
}
public sealed record TabStripLayout(ImmutableArray<TabLaneLayout> Lanes, double Extent)
{
    public bool IsOverflowing => Lanes.Any(lane => lane.IsOverflowing);
}

/// <summary>Renderer-independent allocation of pinned, normal and preview lanes.
/// Pinned tabs stay at the leading edge; previews stay at the trailing edge. Each lane
/// has independent scrolling geometry. Shrinking never violates a tab's minimum.</summary>
public static class TabLayoutEngine
{
    public static bool HasMenu(TabOverflowBehavior behavior) => behavior is TabOverflowBehavior.Menu or TabOverflowBehavior.ScrollWithMenu or TabOverflowBehavior.ShrinkWithMenu;
    public static bool CanScroll(TabOverflowBehavior behavior) => behavior is TabOverflowBehavior.Scroll or TabOverflowBehavior.ScrollWithMenu;
    public static bool CanShrink(TabOverflowBehavior behavior) => behavior is TabOverflowBehavior.Shrink or TabOverflowBehavior.ShrinkWithMenu;

    public static TabStripLayout Arrange(IReadOnlyList<TabMeasurement> tabs, double extent, TabOverflowBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        if (!double.IsFinite(extent) || extent < 0 || !Enum.IsDefined(behavior)) throw new ArgumentOutOfRangeException(nameof(extent));
        if (tabs.Count > 100000) throw new ArgumentException("Tab count exceeds the layout budget.", nameof(tabs));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var indices = new[] { new List<int>(), new List<int>(), new List<int>() };
        var totals = new double[3];
        var sizes = new double[tabs.Count];
        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (string.IsNullOrWhiteSpace(tab.Id) || !ids.Add(tab.Id) || !Enum.IsDefined(tab.Mode) ||
                !double.IsFinite(tab.Desired) || !double.IsFinite(tab.Minimum) || !double.IsFinite(tab.Maximum) ||
                tab.Desired < 0 || tab.Minimum <= 0 || tab.Maximum < tab.Minimum)
                throw new ArgumentException("Invalid or duplicate tab measurement.", nameof(tabs));
            int lane = LaneIndex(tab.Mode);
            sizes[i] = Math.Clamp(tab.Desired, tab.Minimum, tab.Maximum);
            indices[lane].Add(i); totals[lane] += sizes[i];
            if (!double.IsFinite(totals[lane])) throw new ArgumentException("Tab extents overflow.", nameof(tabs));
        }
        double special = totals[0] + totals[2];
        if (!double.IsFinite(special) || !double.IsFinite(special + totals[1])) throw new ArgumentException("Tab extents overflow.", nameof(tabs));
        // Shrink globally before reserving lanes: several normal minima must not be
        // starved by a large pinned/preview lane when a feasible all-tab fit exists.
        if (CanShrink(behavior) && special + totals[1] > extent)
        {
            sizes = Shrink(tabs.ToArray(), sizes, extent);
            for (int lane = 0; lane < 3; lane++) totals[lane] = indices[lane].Sum(i => sizes[i]);
            special = totals[0] + totals[2];
        }
        double normalReserve = indices[1].Count == 0 ? 0 : Math.Min(extent / 3, indices[1].Min(i => tabs[i].Minimum));
        double specialBudget = Math.Max(0, extent - normalReserve);
        double scale = special == 0 ? 0 : Math.Min(1, specialBudget / special);
        var budgets = new[] { totals[0] * scale, 0d, totals[2] * scale };
        budgets[1] = Math.Max(0, extent - budgets[0] - budgets[2]);
        var lanes = ImmutableArray.CreateBuilder<TabLaneLayout>(3);
        double start = 0;
        for (int lane = 0; lane < 3; lane++)
        {
            var widths = indices[lane].Select(i => sizes[i]).ToArray();
            var mode = lane == 0 ? TabDisplayMode.Pinned : lane == 1 ? TabDisplayMode.Normal : TabDisplayMode.Preview;
            lanes.Add(new(mode, start, budgets[lane], indices[lane].ToImmutableArray(), new(widths)));
            start += budgets[lane];
        }
        return new(lanes.ToImmutable(), extent);
    }
    private static double[] Shrink(TabMeasurement[] tabs, double[] desired, double budget)
    {
        if (tabs.Length == 0) return desired;
        var minimum = tabs.Select(t => t.Minimum).ToArray();
        if (minimum.Sum() >= budget) return minimum;
        double low = 0, high = desired.Max();
        for (int iteration = 0; iteration < 64; iteration++)
        {
            double cap = low + (high - low) / 2;
            double total = 0;
            for (int i = 0; i < tabs.Length; i++) total += Math.Max(minimum[i], Math.Min(desired[i], cap));
            if (total <= budget) low = cap; else high = cap;
        }
        for (int i = 0; i < tabs.Length; i++) desired[i] = Math.Max(minimum[i], Math.Min(desired[i], low));
        return desired;
    }
    public static int LaneIndex(TabDisplayMode mode) => mode switch
    {
        TabDisplayMode.Pinned => 0, TabDisplayMode.Normal => 1, TabDisplayMode.Preview => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
