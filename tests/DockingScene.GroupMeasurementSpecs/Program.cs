using System.Collections.Immutable;
using System.Diagnostics;
using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool value, string reason = "Assertion failed") { if (!value) throw new Exception(reason); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
void Reject<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
DockLayout Layout(TabPlacement placement = TabPlacement.Top, bool floating = false, int count = 2)
{
    var windows = Enumerable.Range(0, count).Select(i => new DockWindow("a" + i, "Tool " + i, WindowKind.Tool)
        { MinimumSize = new(100 + i, 70 + i), MaximumSize = new(900 + i, 800 + i) }).ToImmutableDictionary(w => w.Id);
    windows = windows.Add("doc", new("doc", "Document", WindowKind.Document));
    var group = new DockGroup("tools", GroupKind.Tools, [.. windows.Keys.Where(id => id != "doc").Order()], "a0") { TabPlacement = placement };
    var split = new DockSplit("split", SplitAxis.Horizontal, .01, group, new DockWorkspace("workspace", new DockGroup("documents", GroupKind.Documents, ["doc"], "doc")));
    return new() { Windows = windows, Root = floating ? null : split,
        FloatingHosts = floating ? [new("host", split, new(20, 30, 1000, 600))] : [] };
}
DockGroup Tools(DockLayout layout) => (DockGroup)DockTree.Find(layout, "tools")!;
DockLayout Placement(DockLayout layout, TabPlacement placement) => DockTree.Map(layout,
    n => n is DockGroup { Id: "tools" } g ? g with { TabPlacement = placement } : n);
DockMeasure Reference(DockLayout layout, DockGroup group, DockMetrics metrics)
{
    // Independent deliberately simple multi-pass oracle, not the production accumulator.
    var windows = group.Windows.Select(id => layout.Windows[id]).ToArray();
    bool vertical = group.TabPlacement is TabPlacement.Left or TabPlacement.Right;
    double width = metrics.Border * 2 + (vertical ? 155 : 0);
    double height = metrics.Border * 2 + (vertical ? 0 : metrics.TabHeight) +
        (group.Kind == GroupKind.Tools && windows.Any(w => w.HasTitleBar ?? metrics.ToolWindowsHaveTitleBars) ? metrics.TitleHeight : 0);
    double minW = windows.Max(w => w.MinimumSize.Width) + width, minH = windows.Max(w => w.MinimumSize.Height) + height;
    return new(new(minW, minH), new(Math.Max(minW, windows.Min(w => w.MaximumSize.Width) + width), Math.Max(minH, windows.Min(w => w.MaximumSize.Height) + height)));
}
var viewport = new DockRect(0, 0, 1280, 800);
Test("Every placement reserves chrome on the physical axis", () => {
    foreach (var placement in Enum.GetValues<TabPlacement>()) {
        var layout = Layout(placement); var actual = DockLayoutEngine.MeasureGroup(layout, Tools(layout));
        bool vertical = placement is TabPlacement.Left or TabPlacement.Right;
        Check(actual.Minimum == new DockSize(vertical ? 258 : 103, vertical ? 97 : 123));
        Check(actual.Maximum == new DockSize(vertical ? 1057 : 902, vertical ? 826 : 852));
        Check(actual == Reference(layout, Tools(layout), DockMetrics.Default));
    }
});
Test("Document groups never reserve a tool title", () => {
    var layout = Layout(); var group = new DockGroup("documents", GroupKind.Documents, ["doc"]);
    foreach (var placement in Enum.GetValues<TabPlacement>()) {
        group = group with { TabPlacement = placement };
        Check(DockLayoutEngine.MeasureGroup(layout, group) == Reference(layout, group, DockMetrics.Default));
    }
});
Test("Explicit and inherited titles retain the common maximum envelope", () => {
    var layout = Layout(TabPlacement.Left);
    foreach (bool defaultTitle in new[] { false, true }) foreach (bool? a in new bool?[] { null, false, true }) foreach (bool? b in new bool?[] { null, false, true }) {
        var current = layout with { Windows = layout.Windows.SetItem("a0", layout.Windows["a0"] with { HasTitleBar = a }).SetItem("a1", layout.Windows["a1"] with { HasTitleBar = b }) };
        var metrics = new DockMetrics { ToolWindowsHaveTitleBars = defaultTitle };
        Check(DockLayoutEngine.MeasureGroup(current, Tools(current), metrics) == Reference(current, Tools(current), metrics));
    }
});
Test("Conflicting member constraints cannot produce an inverted maximum", () => {
    var layout = Layout(); layout = layout with { Windows = layout.Windows.SetItem("a0", layout.Windows["a0"] with { MinimumSize = new(50, 50), MaximumSize = new(60, 60) }) };
    var measured = DockLayoutEngine.MeasureGroup(layout, Tools(layout));
    Check(measured.Minimum == measured.Maximum);
});
Test("Vertical placement fixes the minimum-clamped horizontal splitter", () => {
    var top = Layout(); var left = Placement(top, TabPlacement.Left);
    var a = DockLayoutEngine.Arrange(top, viewport).Nodes["tools"];
    var b = DockLayoutEngine.Arrange(left, viewport).Nodes["tools"];
    Check(a.Width == 103 && b.Width == 258 && b.Width - a.Width == 155);
    Check(b.Width - 155 - 2 >= 101, "The physical content minimum must still fit beside the strip");
});
Test("Vertical placement removes obsolete horizontal-strip height", () => {
    var top = Layout(); var split = (DockSplit)top.Root!;
    top = top with { Root = split with { Axis = SplitAxis.Vertical } };
    var left = Placement(top, TabPlacement.Left);
    Check(DockLayoutEngine.Arrange(top, viewport).Nodes["tools"].Height == 123);
    Check(DockLayoutEngine.Arrange(left, viewport).Nodes["tools"].Height == 97);
});
Test("Main and floating cache keys track axis changes but reuse opposite edges", () => {
    foreach (bool floating in new[] { false, true }) {
        var layout = Layout(floating: floating); var cache = new DockGeometryCache();
        var top = cache.Arrange(layout, viewport);
        layout = Placement(layout, TabPlacement.Bottom); Check(ReferenceEquals(top, cache.Arrange(layout, viewport)));
        layout = Placement(layout, TabPlacement.Left); var left = cache.Arrange(layout, viewport); Check(!ReferenceEquals(top, left));
        layout = Placement(layout, TabPlacement.Right); Check(ReferenceEquals(left, cache.Arrange(layout, viewport)));
        layout = Placement(layout, TabPlacement.Top); Check(!ReferenceEquals(left, cache.Arrange(layout, viewport)));
        Check(cache.BuildCount == 3 && cache.ReuseCount == 2);
    }
});
Test("Selected tabs and button-only metadata reuse both measurement envelopes", () => {
    var layout = Layout(TabPlacement.Left); var cache = new DockGeometryCache(); var before = cache.Arrange(layout, viewport);
    layout = DockTree.Map(layout, n => n is DockGroup { Id: "tools" } g ? g with { SelectedId = "a1" } : n);
    layout = layout with { Windows = layout.Windows.SetItem("a1", layout.Windows["a1"] with { HasCloseButton = false, HasOptionsButton = false }) };
    Check(ReferenceEquals(before, cache.Arrange(layout, viewport)) && cache.BuildCount == 1);
});
Test("Frozen floating plans match full placement-aware geometry", () => {
    foreach (var placement in Enum.GetValues<TabPlacement>()) foreach (bool title in new[] { false, true }) {
        var layout = Layout(placement, true); var metrics = new DockMetrics { DpiScale = 1.25, Border = 2, TitleHeight = 29, TabHeight = 31, ToolWindowsHaveTitleBars = title };
        var geometry = DockLayoutEngine.Arrange(layout, viewport, metrics); var plan = new FloatingLayoutPlan(layout, "host", metrics);
        var nodes = new DockRect[plan.NodeCount]; var splitters = new DockRect[plan.SplitterCount];
        plan.Arrange(geometry.FloatingHosts["host"], nodes, splitters);
        for (int i = 0; i < plan.NodeCount; i++) Check(nodes[i] == geometry.Nodes[plan.GetNodeId(i)]);
        for (int i = 0; i < plan.NodeCount; i++) if (plan.GetSplitterIndex(i) is int slot && slot >= 0)
            Check(splitters[slot] == geometry.Splitters.First(s => s.Id == plan.GetNodeId(i)).Bounds);
    }
});
Test("A retained floating plan remains frozen after placement changes", () => {
    var top = Layout(floating: true); var old = new FloatingLayoutPlan(top, "host");
    var left = Placement(top, TabPlacement.Left); var updated = new FloatingLayoutPlan(left, "host");
    Check(updated.ContentMeasure.Minimum.Width - old.ContentMeasure.Minimum.Width == 155);
    Check(old.ContentMeasure == new FloatingLayoutPlan(top, "host").ContentMeasure);
});
Test("Five hundred seeded envelopes match an independent four-placement oracle", () => {
    var random = new Random(72103);
    for (int i = 0; i < 500; i++) {
        var layout = Layout((TabPlacement)(i % 4), count: 1 + random.Next(40));
        var windows = layout.Windows;
        foreach (string id in Tools(layout).Windows) {
            var min = new DockSize(random.Next(500), random.Next(400));
            windows = windows.SetItem(id, windows[id] with { MinimumSize = min, MaximumSize = new(min.Width + random.Next(1, 700), min.Height + random.Next(1, 700)), HasTitleBar = i % 3 == 0 ? null : i % 2 == 0 });
        }
        layout = layout with { Windows = windows };
        var metrics = new DockMetrics { Border = i % 4, TabHeight = 10 + i % 30, TitleHeight = 10 + i % 20, ToolWindowsHaveTitleBars = i % 2 == 0 };
        Check(DockLayoutEngine.MeasureGroup(layout, Tools(layout), metrics) == Reference(layout, Tools(layout), metrics));
    }
});
Test("Default metrics are immutable and custom values do not leak to other callers", () => {
    var defaults = DockMetrics.Default; var custom = defaults with { TitleHeight = 40, Border = 3 };
    Check(ReferenceEquals(defaults, DockMetrics.Default) && defaults.TitleHeight == 24 && defaults.Border == 1);
    var layout = Layout(); Check(DockLayoutEngine.MeasureGroup(layout, Tools(layout)) != DockLayoutEngine.MeasureGroup(layout, Tools(layout), custom));
    Check(DockLayoutEngine.MeasureGroup(layout, Tools(layout)) == Reference(layout, Tools(layout), defaults));
});
Test("Empty groups and unknown presentation enums reject", () => {
    var layout = Layout(); var group = Tools(layout);
    Reject<ArgumentException>(() => DockLayoutEngine.MeasureGroup(layout, group with { Windows = [] }));
    Reject<ArgumentException>(() => DockLayoutEngine.MeasureGroup(layout, group with { Windows = default }));
    Reject<ArgumentException>(() => DockLayoutEngine.MeasureGroup(layout, group with { Kind = (GroupKind)99 }));
    Reject<ArgumentException>(() => DockLayoutEngine.MeasureGroup(layout, group with { TabPlacement = (TabPlacement)99 }));
    Reject<ArgumentNullException>(() => DockLayoutEngine.MeasureGroup(null!, group));
    Reject<ArgumentNullException>(() => DockLayoutEngine.MeasureGroup(layout, null!));
});
Test("Invalid metrics and member constraints reject without altering cache state", () => {
    var layout = Layout(); var cache = new DockGeometryCache(); var cached = cache.Arrange(layout, viewport);
    foreach (var metrics in new[] { new DockMetrics { Border = -1 }, new() { TabHeight = double.NaN }, new() { TitleHeight = double.PositiveInfinity }, new() { DpiScale = 0 } })
        Reject<ArgumentException>(() => DockLayoutEngine.MeasureGroup(layout, Tools(layout), metrics));
    foreach (var window in new[] { layout.Windows["a0"] with { MinimumSize = new(-1, 60) }, layout.Windows["a0"] with { MaximumSize = new(1, 1) } }) {
        var bad = layout with { Windows = layout.Windows.SetItem("a0", window) };
        Reject<ArgumentException>(() => DockLayoutEngine.MeasureGroup(bad, Tools(bad)));
        Reject<ArgumentException>(() => cache.Arrange(bad, viewport));
    }
    Check(ReferenceEquals(cached, cache.Arrange(layout, viewport)) && cache.BuildCount == 1);
});
Test("Overflow is rejected without replacing the previous valid geometry", () => {
    var layout = Layout(); var cache = new DockGeometryCache(); var cached = cache.Arrange(layout, viewport);
    var metrics = new DockMetrics { Border = double.MaxValue };
    Reject<InvalidOperationException>(() => DockLayoutEngine.MeasureGroup(layout, Tools(layout), metrics));
    Reject<InvalidOperationException>(() => cache.Arrange(layout, viewport, metrics));
    Check(ReferenceEquals(cached, cache.Arrange(layout, viewport)));
});
Test("Omitted-metrics warm geometry queries allocate zero managed bytes", () => {
    var layout = Layout(); var cache = new DockGeometryCache(); var expected = cache.Arrange(layout, viewport);
    AllocationProbe.VerifyControls();
    Func<DockGeometry> query = () => cache.Arrange(layout, viewport);
    _ = AllocationProbe.Measure(query, 1000, out _);
    long bytes = AllocationProbe.Measure(query, 10000, out var result);
    Check(bytes == 0 && ReferenceEquals(result, expected), $"Expected zero default-query bytes; actual {bytes}");
    Console.WriteLine($"ALLOCATION default geometry: 10000 warm queries, {bytes} bytes; construction excluded");
});
Test("Single-pass group measurement allocates zero managed bytes", () => {
    var layout = Layout(TabPlacement.Left, count: 128); var group = Tools(layout); var expected = Reference(layout, group, DockMetrics.Default);
    Func<DockMeasure> query = () => DockLayoutEngine.MeasureGroup(layout, group);
    _ = AllocationProbe.Measure(query, 1000, out _);
    var watch = Stopwatch.StartNew();
    long bytes = AllocationProbe.Measure(query, 10000, out var result);
    watch.Stop();
    Check(bytes == 0 && result == expected, $"Expected zero group-measure bytes; actual {bytes}");
    Console.WriteLine($"GROUP_MEASURE members=128 queries=10000 elapsedMs={watch.Elapsed.TotalMilliseconds:F3} allocated={bytes}");
});
Console.WriteLine($"{passed}/{passed + failed} group measurement specifications passed.");
return failed == 0 ? 0 : 1;
