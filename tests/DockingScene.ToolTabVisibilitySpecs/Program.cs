using System.Collections.Immutable;
using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Test(string name, Action run)
{
    try { run(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
void Reject(Action run)
{
    try { run(); } catch (ArgumentException) { return; }
    throw new Exception("Invalid input accepted");
}
var hidden = new DockMetrics { ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Hide };
var shown = DockMetrics.Default;
var viewport = new DockRect(0, 0, 1200, 800);
DockLayout Fixture(TabPlacement placement = TabPlacement.Top, int count = 1, bool floating = false)
{
    var windows = Enumerable.Range(0, count).Select(i => new DockWindow("t" + i, "Tool", WindowKind.Tool)
        { MinimumSize = new(120, 80), MaximumSize = new(600, 500) }).ToImmutableDictionary(w => w.Id);
    var group = new DockGroup("tools", GroupKind.Tools, [.. windows.Keys.Order()], "t0") { TabPlacement = placement };
    var root = new DockSplit("split", SplitAxis.Horizontal, .01, group,
        new DockWorkspace("workspace", new DockGroup("docs", GroupKind.Documents, ["d"], "d")));
    return new() { Root = floating ? null : root, Windows = windows.Add("d", new("d", "Document", WindowKind.Document)),
        FloatingHosts = floating ? [new("host", root, new(20, 30, 900, 650))] : [] };
}
DockGroup Group(DockLayout layout) => (DockGroup)DockTree.Find(layout, "tools")!;
DockLayout Place(DockLayout layout, TabPlacement placement) => DockTree.Map(layout,
    n => n is DockGroup { Id: "tools" } g ? g with { TabPlacement = placement } : n);

Test("Standalone metrics preserve shown-strip behavior", () => {
    Check(shown.ToolWindowsSingleTabLayoutBehavior == SingleTabLayoutBehavior.Show);
    var l = Fixture(); Check(DockLayoutEngine.MeasureGroup(l, Group(l)).Minimum == new DockSize(122, 132));
});
Test("Hidden single-tool strips reclaim the correct width or height on all four edges", () => {
    foreach (var edge in Enum.GetValues<TabPlacement>()) {
        var l = Fixture(edge); var a = DockLayoutEngine.MeasureGroup(l, Group(l), shown); var b = DockLayoutEngine.MeasureGroup(l, Group(l), hidden);
        bool vertical = edge is TabPlacement.Left or TabPlacement.Right;
        Check(b.Minimum == new DockSize(122, 106) && b.Maximum == new DockSize(602, 526));
        Check(a.Minimum.Width - b.Minimum.Width == (vertical ? 155 : 0));
        Check(a.Minimum.Height - b.Minimum.Height == (vertical ? 0 : 26));
    }
});
Test("Multi-tool groups always reserve their visible strip", () => {
    foreach (var edge in Enum.GetValues<TabPlacement>()) for (int count = 2; count <= 3; count++) {
        var l = Fixture(edge, count); Check(DockLayoutEngine.MeasureGroup(l, Group(l), hidden) == DockLayoutEngine.MeasureGroup(l, Group(l), shown));
    }
});
Test("Document groups ignore the single-tool strip policy", () => {
    var l = Fixture(); var g = (DockGroup)DockTree.Find(l, "docs")!;
    foreach (var edge in Enum.GetValues<TabPlacement>()) {
        var current = g with { TabPlacement = edge };
        Check(DockLayoutEngine.MeasureGroup(l, current, hidden) == DockLayoutEngine.MeasureGroup(l, current, shown));
    }
});
Test("Title inheritance remains independent of strip visibility", () => {
    foreach (bool defaultTitle in new[] { false, true }) foreach (bool? title in new bool?[] { null, false, true }) {
        var l = Fixture(); l = l with { Windows = l.Windows.SetItem("t0", l.Windows["t0"] with { HasTitleBar = title }) };
        var m = hidden with { ToolWindowsHaveTitleBars = defaultTitle };
        Check(DockLayoutEngine.MeasureGroup(l, Group(l), m).Minimum == new DockSize(122, 82 + ((title ?? defaultTitle) ? 24 : 0)));
    }
});
Test("Hidden strips no longer distort minimum-clamped split ratios", () => {
    var l = Fixture(TabPlacement.Left);
    Check(DockLayoutEngine.Arrange(l, viewport, shown).Nodes["tools"].Width == 277);
    Check(DockLayoutEngine.Arrange(l, viewport, hidden).Nodes["tools"].Width == 122);
    l = l with { Root = ((DockSplit)l.Root!) with { Axis = SplitAxis.Vertical } };
    l = Place(l, TabPlacement.Top);
    Check(DockLayoutEngine.Arrange(l, viewport, shown).Nodes["tools"].Height == 132);
    Check(DockLayoutEngine.Arrange(l, viewport, hidden).Nodes["tools"].Height == 106);
});
Test("All invisible strip edges share geometry in main and floating hosts", () => {
    foreach (bool floating in new[] { false, true }) {
        var l = Fixture(floating: floating); var cache = new DockGeometryCache(); var first = cache.Arrange(l, viewport, hidden);
        foreach (var edge in Enum.GetValues<TabPlacement>()) {
            l = Place(l, edge); Check(ReferenceEquals(first, cache.Arrange(l, viewport, hidden)));
        }
        Check(cache.BuildCount == 1);
        Check(!ReferenceEquals(first, cache.Arrange(l, viewport, shown)) && cache.BuildCount == 2);
    }
});
Test("Membership changes still invalidate a hidden-strip cache", () => {
    var l = Fixture(TabPlacement.Left, 2); var cache = new DockGeometryCache(); var multi = cache.Arrange(l, viewport, hidden);
    l = DockTree.Map(l, n => n is DockGroup { Id: "tools" } g ? g with { Windows = ["t0"] } : n);
    l = l with { HiddenWindows = ["t1"] };
    var single = cache.Arrange(l, viewport, hidden);
    Check(!ReferenceEquals(single, multi) && multi.Nodes["tools"].Width - single.Nodes["tools"].Width == 155);
});
Test("Floating plans agree with full layout and preserve frozen visibility", () => {
    foreach (var edge in Enum.GetValues<TabPlacement>()) foreach (var metrics in new[] { hidden, shown }) {
        var l = Fixture(edge, floating: true); var m = metrics with { DpiScale = 1.25 };
        var plan = new FloatingLayoutPlan(l, "host", m); var direct = DockLayoutEngine.Arrange(l, viewport, m);
        var nodes = new DockRect[plan.NodeCount]; var splitters = new DockRect[plan.SplitterCount];
        plan.Arrange(direct.FloatingHosts["host"], nodes, splitters);
        for (int i = 0; i < nodes.Length; i++) Check(nodes[i] == direct.Nodes[plan.GetNodeId(i)]);
        var original = plan.ContentMeasure;
        _ = new FloatingLayoutPlan(l, "host", m with { ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Show });
        Check(plan.ContentMeasure == original);
    }
});
Test("Invalid visibility rejects in every public measurement path without poisoning the cache", () => {
    var l = Fixture(floating: true); var invalid = hidden with { ToolWindowsSingleTabLayoutBehavior = (SingleTabLayoutBehavior)99 };
    var cache = new DockGeometryCache(); var first = cache.Arrange(l, viewport, hidden);
    Reject(() => DockLayoutEngine.MeasureGroup(l, Group(l), invalid));
    Reject(() => DockLayoutEngine.Arrange(l, viewport, invalid));
    Reject(() => new FloatingLayoutPlan(l, "host", invalid));
    Reject(() => cache.Arrange(l, viewport, invalid));
    Check(ReferenceEquals(first, cache.Arrange(l, viewport, hidden)) && cache.BuildCount == 1);
});
Test("Five hundred seeded content envelopes match independently calculated visible chrome", () => {
    var random = new Random(84102);
    for (int i = 0; i < 500; i++) {
        int count = 1 + random.Next(3); var edge = (TabPlacement)(i % 4); var l = Fixture(edge, count);
        var min = new DockSize(random.Next(1, 400), random.Next(1, 300)); var max = new DockSize(min.Width + 50, min.Height + 80);
        var windows = l.Windows;
        foreach (var id in Group(l).Windows) windows = windows.SetItem(id, windows[id] with { MinimumSize = min, MaximumSize = max });
        l = l with { Windows = windows }; var metrics = i % 2 == 0 ? hidden : shown;
        bool tabs = count > 1 || i % 2 != 0, vertical = edge is TabPlacement.Left or TabPlacement.Right;
        double width = 2 + (tabs && vertical ? 155 : 0), height = 26 + (tabs && !vertical ? 26 : 0);
        var actual = DockLayoutEngine.MeasureGroup(l, Group(l), metrics);
        Check(actual.Minimum == new DockSize(min.Width + width, min.Height + height));
        Check(actual.Maximum == new DockSize(max.Width + width, max.Height + height));
    }
});
Test("Hidden-strip measurements and cache hits remain allocation-free after warm-up", () => {
    var l = Fixture(TabPlacement.Left); var group = Group(l); var cache = new DockGeometryCache();
    AllocationProbe.VerifyControls();
    Func<DockMeasure> measure = () => DockLayoutEngine.MeasureGroup(l, group, hidden);
    Func<DockGeometry> arrange = () => cache.Arrange(l, viewport, hidden);
    _ = AllocationProbe.Measure(measure, 1000, out _); _ = AllocationProbe.Measure(arrange, 1000, out _);
    long measureBytes = AllocationProbe.Measure(measure, 10000, out var measured);
    long cacheBytes = AllocationProbe.Measure(arrange, 10000, out var geometry);
    Check(measureBytes == 0 && cacheBytes == 0 && measured.Minimum.Width == 122 && geometry.Nodes["tools"].Width == 122,
        $"Hidden-strip allocation: measure={measureBytes}, cache={cacheBytes}");
});
Console.WriteLine($"{passed}/{passed + failed} tool tab visibility specifications passed.");
return failed == 0 ? 0 : 1;
