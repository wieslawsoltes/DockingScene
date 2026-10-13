using System.Collections.Immutable;
using System.Diagnostics;
using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool condition, string reason = "Assertion failed") { if (!condition) throw new Exception(reason); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
void Reject(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Invalid input was accepted");
}
DockLayout Layout(int seed = 1, int leaves = 16)
{
    var random = new Random(seed);
    var windows = ImmutableDictionary.CreateBuilder<string, DockWindow>();
    int serial = 0;
    DockNode Tree(int count)
    {
        int id = serial++;
        if (count == 1)
        {
            bool tool = id % 3 == 0;
            var min = new DockSize(random.Next(20, 90), random.Next(15, 60));
            windows["w" + id] = new("w" + id, "Window " + id, tool ? WindowKind.Tool : WindowKind.Document)
            { MinimumSize = min, MaximumSize = new(min.Width + random.Next(10, 400), min.Height + random.Next(10, 400)), HasTitleBar = id % 2 == 0 };
            return new DockGroup("g" + id, tool ? GroupKind.Tools : GroupKind.Documents, ["w" + id]);
        }
        return new DockSplit("s" + id, id % 2 == 0 ? SplitAxis.Horizontal : SplitAxis.Vertical,
            random.NextDouble(), Tree(count / 2), Tree(count - count / 2));
    }
    var tree = new DockWorkspace("floating-workspace", Tree(leaves));
    return new() { Windows = windows.ToImmutable(), FloatingHosts = [new("host", tree, new(10, 30, 800, 600))], Revision = 17 };
}
void Compare(DockLayout layout, DockRect viewport, DockMetrics metrics)
{
    var expected = DockLayoutEngine.Arrange(layout, viewport, metrics);
    var plan = new FloatingLayoutPlan(layout, "host", metrics);
    var nodes = new DockRect[plan.NodeCount]; var splitters = new DockRect[plan.SplitterCount];
    plan.Arrange(expected.FloatingHosts["host"], nodes, splitters);
    for (int i = 0; i < nodes.Length; i++)
    {
        string id = plan.GetNodeId(i);
        Check(nodes[i] == expected.Nodes[id], $"Node {id}: {nodes[i]} != {expected.Nodes[id]}");
        if (plan.GetSplitterIndex(i) is int slot && slot >= 0)
            Check(splitters[slot] == expected.Splitters.First(s => s.Id == id).Bounds, "Splitter " + id);
    }
}
Test("Measured plan matches full layout for mixed groups and nested workspaces", () =>
{
    foreach (double dpi in new[] { 1d, 1.25, 1.5, 2, 3 })
        Compare(Layout(), new(0, 0, 1500, 1100), new() { DpiScale = dpi });
});
Test("One thousand seeded resizes match constrained full layout at fractional DPI", () =>
{
    var random = new Random(947);
    for (int i = 0; i < 1000; i++)
    {
        var layout = Layout(i, 2 + random.Next(30));
        var host = layout.FloatingHosts[0] with { Bounds = new(random.Next(-90, 600), random.Next(-20, 400), random.Next(1, 1400), random.Next(1, 1000)) };
        layout = layout with { FloatingHosts = [host] };
        Compare(layout, new(0, 0, 1200, 900), new() { DpiScale = i % 2 == 0 ? 1.25 : 2, Splitter = i % 7, Border = i % 3 });
    }
});
Test("Normal and maximized displayed bounds use the identical subtree algorithm", () =>
{
    var layout = Layout();
    Compare(layout with { FloatingHosts = [layout.FloatingHosts[0] with { IsMaximized = true }] },
        new(0, 0, 480, 320), new());
});
Test("Empty workspace and zero frame extents produce bounded nonnegative output", () =>
{
    var layout = new DockLayout { FloatingHosts = [new("host", new DockWorkspace("empty"), new(0, 0, 100, 100))] };
    var plan = new FloatingLayoutPlan(layout, "host");
    var nodes = new DockRect[1]; plan.Arrange(new(0, 0, 0, 0), nodes, []);
    Check(nodes[0] == new DockRect(1, 24, 0, 0) && plan.SplitterCount == 0);
});
Test("Snapshot changes cannot mutate a retained plan", () =>
{
    var original = Layout();
    var plan = new FloatingLayoutPlan(original, "host");
    var nodes = new DockRect[plan.NodeCount]; var splitters = new DockRect[plan.SplitterCount];
    plan.Arrange(new(20, 40, 700, 450), nodes, splitters); var before = nodes.ToArray();
    var changed = original with { Windows = original.Windows.Clear() };
    plan.Arrange(new(20, 40, 700, 450), nodes, splitters);
    Check(nodes.SequenceEqual(before) && plan.Revision == 17 && original.Windows.Count > 0 && changed.Windows.Count == 0);
});
Test("Only the requested host is measured", () =>
{
    var layout = Layout();
    layout = layout with { FloatingHosts = layout.FloatingHosts.Add(new("unrelated", new DockGroup("invalid", GroupKind.Documents, ["absent"]), new(0, 0, 40, 40))) };
    Check(new FloatingLayoutPlan(layout, "host").NodeCount > 1);
    Reject(() => new FloatingLayoutPlan(layout, "unrelated"));
});
Test("Invalid metrics and missing hosts reject at construction", () =>
{
    var layout = Layout();
    Reject(() => new FloatingLayoutPlan(layout, "missing"));
    foreach (var metrics in new[] { new DockMetrics { Border = -1 }, new() { TitleHeight = double.NaN }, new() { DpiScale = 0 }, new() { Splitter = double.PositiveInfinity } })
        Reject(() => new FloatingLayoutPlan(layout, "host", metrics));
});
Test("Repeated nodes, members, invalid ratios and deep trees reject before arrangement", () =>
{
    var layout = Layout(1, 1); var leaf = ((DockWorkspace)layout.FloatingHosts[0].Root).Content!;
    foreach (DockNode node in new DockNode[] { new DockSplit("duplicate", SplitAxis.Horizontal, .5, leaf, leaf),
        new DockSplit("bad-ratio", SplitAxis.Horizontal, double.NaN, leaf, leaf) })
        Reject(() => new FloatingLayoutPlan(layout with { FloatingHosts = [layout.FloatingHosts[0] with { Root = node }] }, "host"));
    DockNode deep = leaf;
    for (int i = 0; i < 66; i++) deep = new DockWorkspace("deep" + i, deep);
    Reject(() => new FloatingLayoutPlan(layout with { FloatingHosts = [layout.FloatingHosts[0] with { Root = deep }] }, "host"));
});
Test("Bad buffers and geometry reject without changing previous output", () =>
{
    var plan = new FloatingLayoutPlan(Layout(), "host");
    var sentinel = new DockRect(123, 456, 7, 8);
    var nodes = Enumerable.Repeat(sentinel, plan.NodeCount).ToArray();
    var splitters = Enumerable.Repeat(sentinel, plan.SplitterCount).ToArray();
    Reject(() => plan.Arrange(new(double.MaxValue, 0, double.MaxValue, 20), nodes, splitters));
    Reject(() => plan.Arrange(new(0, 0, 20, -1), nodes, splitters));
    Reject(() => plan.Arrange(new(0, 0, 20, 20), nodes.AsSpan(1), splitters));
    Reject(() => plan.Arrange(new(0, 0, 20, 20), nodes, nodes.AsSpan(0, plan.SplitterCount)));
    Check(nodes.All(n => n == sentinel) && splitters.All(n => n == sentinel));
});
Test("Caller buffer tails are untouched", () =>
{
    var plan = new FloatingLayoutPlan(Layout(), "host");
    var sentinel = new DockRect(123, 456, 7, 8);
    var nodes = Enumerable.Repeat(sentinel, plan.NodeCount + 2).ToArray();
    var splitters = Enumerable.Repeat(sentinel, plan.SplitterCount + 2).ToArray();
    plan.Arrange(new(30, 40, 500, 300), nodes, splitters);
    Check(nodes[^1] == sentinel && nodes[^2] == sentinel && splitters[^1] == sentinel && splitters[^2] == sentinel);
});
Test("Separate output buffers allow simultaneous use of an immutable plan", () =>
{
    var plan = new FloatingLayoutPlan(Layout(), "host");
    Parallel.For(0, 64, i =>
    {
        var nodes = new DockRect[plan.NodeCount]; var bars = new DockRect[plan.SplitterCount];
        plan.Arrange(new(i, 40, 700, 450), nodes, bars);
        Check(nodes[0].X == i + 1);
    });
});
Test("A minimum-clamped pane grows only when the split ratio clears its constraint", () =>
{
    var windows = new[] { "a", "b" }.ToImmutableDictionary(id => id,
        id => new DockWindow(id, id, WindowKind.Document));
    var split = new DockSplit("split", SplitAxis.Vertical, .75,
        new DockGroup("a-group", GroupKind.Documents, ["a"]),
        new DockGroup("b-group", GroupKind.Documents, ["b"]));
    var layout = new DockLayout { Windows = windows, FloatingHosts = [new("host", split, new(20, 40, 260, 220))] };
    var plan = new FloatingLayoutPlan(layout, "host");
    var nodes = new DockRect[plan.NodeCount]; var bars = new DockRect[plan.SplitterCount];
    foreach (var (height, expectedA, expectedB) in new[] { (220d, 102d, 88d), (280d, 162d, 88d), (400d, 278d, 92d), (220d, 102d, 88d) })
    {
        var frame = new DockRect(20, 40, 260, height);
        plan.Arrange(frame, nodes, bars);
        Check(nodes[1].Height == expectedA && nodes[2].Height == expectedB,
            $"Height {height}: first={nodes[1].Height}, second={nodes[2].Height}");
        var reference = DockLayoutEngine.Arrange(layout with { FloatingHosts = [layout.FloatingHosts[0] with { Bounds = frame }] },
            new(0, 0, 1200, 900));
        Check(nodes[1] == reference.Nodes["a-group"] && nodes[2] == reference.Nodes["b-group"]);
    }
});
Test("Warm subtree arrangement allocates no managed bytes", () =>
{
    var plan = new FloatingLayoutPlan(Layout(947, 64), "host");
    var nodes = new DockRect[plan.NodeCount]; var bars = new DockRect[plan.SplitterCount];
    for (int i = 0; i < 2000; i++) plan.Arrange(new(20, 40, 800 + i % 30, 600), nodes, bars);
    var watch = Stopwatch.StartNew();
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 20000; i++) plan.Arrange(new(20, 40, 800 + i % 30, 600), nodes, bars);
    long bytes = GC.GetAllocatedBytesForCurrentThread() - before; watch.Stop();
    Console.WriteLine($"FLOATING_LAYOUT nodes={plan.NodeCount} queries=20000 elapsedMs={watch.Elapsed.TotalMilliseconds:F3} allocated={bytes}");
    Check(bytes == 0, "Warm arrangement allocated " + bytes);
});
Console.WriteLine($"{passed}/{passed + failed} floating layout specifications passed.");
return failed == 0 ? 0 : 1;
