using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;

// This initializer adds a deterministic invariant check to this executable suite.
// Throwing aborts the suite with a nonzero exit code before any PASS summary.
internal static class TabFitChecks
{
    [ModuleInitializer]
    internal static void ValidateFeasibleShrinking()
    {
        var random = new Random(91408);
        for (int trial = 0; trial < 500; trial++)
        {
            var tabs = Enumerable.Range(0, random.Next(1, 70)).Select(i => new TabMeasurement("t" + i, (TabDisplayMode)random.Next(3), random.Next(30, 400), random.Next(1, 30))).ToArray();
            double extent = tabs.Sum(t => t.Minimum) + random.Next(1, 800);
            var plan = TabLayoutEngine.Arrange(tabs, extent, TabOverflowBehavior.ShrinkWithMenu);
            if (plan.Lanes.Any(lane => lane.Metrics.Length > lane.Extent + .001)) throw new Exception("A feasible shrink layout overflowed its viewport.");
        }
        Console.WriteLine("PASS 500 additional seeded feasible-fit allocations");
    }
}
