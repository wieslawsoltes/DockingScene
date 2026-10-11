using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < .0001, $"{actual} != {expected}");
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
TabMeasurement Tab(string id, double size, TabDisplayMode mode = TabDisplayMode.Normal, double min = 24, double max = 300) => new(id, mode, size, min, max);
Test("Three stable lanes retain original item indexes", () => {
    var plan = TabLayoutEngine.Arrange(new[] { Tab("n", 100), Tab("p", 60, TabDisplayMode.Pinned), Tab("v", 80, TabDisplayMode.Preview) }, 500, TabOverflowBehavior.Scroll);
    Check(plan.Lanes[0].Indices.SequenceEqual(new[] { 1 })); Check(plan.Lanes[1].Indices.SequenceEqual(new[] { 0 }));
    Near(plan.Lanes[0].Start, 0); Near(plan.Lanes[2].Start, 420); Near(plan.Lanes[1].Extent, 360);
});
Test("Preview-only content remains trailing aligned", () => {
    var plan = TabLayoutEngine.Arrange(new[] { Tab("v", 80, TabDisplayMode.Preview) }, 500, TabOverflowBehavior.Menu);
    Near(plan.Lanes[2].Start, 420); Near(plan.Lanes[1].Extent, 420);
});
Test("Normal overflow cannot scroll away pinned and preview lanes", () => {
    var tabs = new[] { Tab("p", 60, TabDisplayMode.Pinned), Tab("a", 200), Tab("b", 200), Tab("v", 70, TabDisplayMode.Preview) };
    var plan = TabLayoutEngine.Arrange(tabs, 300, TabOverflowBehavior.ScrollWithMenu);
    Near(plan.Lanes[0].Extent, 60); Near(plan.Lanes[1].Extent, 170); Near(plan.Lanes[2].Start, 230);
    Check(plan.Lanes[1].IsOverflowing && !plan.Lanes[0].IsOverflowing && !plan.Lanes[2].IsOverflowing);
});
Test("Shrink caps long tabs without expanding short ones", () => {
    var plan = TabLayoutEngine.Arrange(new[] { Tab("a", 40), Tab("b", 200), Tab("c", 200) }, 240, TabOverflowBehavior.Shrink);
    var offsets = plan.Lanes[1].Metrics.Offsets; Near(offsets[1], 40); Near(offsets[2] - offsets[1], 100); Near(offsets[3], 240);
});
Test("Fixed extents are never reduced below their minimum", () => {
    var plan = TabLayoutEngine.Arrange(new[] { Tab("a", 80, min: 80, max: 80), Tab("b", 80, min: 80, max: 80) }, 100, TabOverflowBehavior.ShrinkWithMenu);
    Near(plan.Lanes[1].Metrics.Length, 160); Check(plan.IsOverflowing);
});
Test("Special-lane saturation leaves normal content a viewport", () => {
    var plan = TabLayoutEngine.Arrange(new[] { Tab("p", 300, TabDisplayMode.Pinned), Tab("a", 100), Tab("v", 300, TabDisplayMode.Preview) }, 200, TabOverflowBehavior.Scroll);
    Near(plan.Lanes[1].Extent, 24); Near(plan.Lanes[0].Extent, 88); Near(plan.Lanes[2].Extent, 88);
});
Test("Empty and zero-size strips remain finite", () => {
    var empty = TabLayoutEngine.Arrange(Array.Empty<TabMeasurement>(), 0, TabOverflowBehavior.None);
    Check(empty.Lanes.Length == 3 && empty.Lanes.All(l => l.Extent == 0));
    var zero = TabLayoutEngine.Arrange(new[] { Tab("a", 100) }, 0, TabOverflowBehavior.Shrink);
    Check(zero.IsOverflowing); Near(zero.Lanes.Sum(l => l.Extent), 0);
});
Test("All six modes expose only the requested affordances", () => {
    Check(TabLayoutEngine.HasMenu(TabOverflowBehavior.Menu)); Check(TabLayoutEngine.HasMenu(TabOverflowBehavior.ScrollWithMenu));
    Check(!TabLayoutEngine.HasMenu(TabOverflowBehavior.Scroll)); Check(!TabLayoutEngine.CanScroll(TabOverflowBehavior.Menu));
    Check(TabLayoutEngine.CanShrink(TabOverflowBehavior.ShrinkWithMenu)); Check(!TabLayoutEngine.CanShrink(TabOverflowBehavior.None));
});
Test("Duplicate identities, invalid enums and nonfinite extents reject", () => {
    var cases = new[] { new[] { Tab("a", 50), Tab("a", 60) }, new[] { Tab("a", double.NaN) }, new[] { Tab("a", 50, (TabDisplayMode)99) }, new[] { Tab("a", 50, min: 0) } };
    foreach (var tabs in cases) { bool rejected = false; try { TabLayoutEngine.Arrange(tabs, 100, TabOverflowBehavior.Menu); } catch (ArgumentException) { rejected = true; } Check(rejected); }
});
Test("Ten thousand tabs keep a small visible header range", () => {
    var tabs = Enumerable.Range(0, 10000).Select(i => Tab("tab" + i, 60 + i % 100)).ToArray();
    var plan = TabLayoutEngine.Arrange(tabs, 1000, TabOverflowBehavior.ScrollWithMenu);
    Check(plan.Lanes[1].Metrics.VisibleRange(350000, 1000, 100).Count < 30);
});
Test("Seeded allocations preserve extents, ordering and constraints", () => {
    var random = new Random(73921);
    for (int trial = 0; trial < 500; trial++)
    {
        var tabs = Enumerable.Range(0, random.Next(1, 70)).Select(i => Tab("t" + i, random.NextDouble() * 800, (TabDisplayMode)random.Next(3), random.Next(1, 30))).ToArray();
        double available = random.NextDouble() * 1800;
        var plan = TabLayoutEngine.Arrange(tabs, available, (TabOverflowBehavior)random.Next(6));
        Near(plan.Lanes.Sum(l => l.Extent), available);
        Check(plan.Lanes.SelectMany(l => l.Indices).Order().SequenceEqual(Enumerable.Range(0, tabs.Length)));
        foreach (var lane in plan.Lanes)
        {
            Check(lane.Extent >= 0 && double.IsFinite(lane.Start));
            for (int i = 0; i < lane.Indices.Length; i++)
            { var tab = tabs[lane.Indices[i]]; double size = lane.Metrics.Offsets[i + 1] - lane.Metrics.Offsets[i]; Check(size >= tab.Minimum - .0001 && size <= tab.Maximum + .0001); }
        }
    }
});
Test("Digit nine selects the last normal tab, not the ninth", () => {
    var manager = new DockManager();
    for (int i = 0; i < 12; i++) manager.Register(new("d" + i, "Document", WindowKind.Document)).ThrowIfFailed();
    var group = DockTree.GroupOf(manager.Layout, "d0")!;
    manager.ActivateTabByDigit(group.Id, 9).ThrowIfFailed(); Check(manager.Layout.ActiveWindowId == "d11");
    manager.ActivateTabByDigit(group.Id, 8).ThrowIfFailed(); Check(manager.Layout.ActiveWindowId == "d7");
});
Test("Digit selection filters pinned and preview independently", () => {
    var manager = new DockManager(); manager.Register(new("n", "Normal", WindowKind.Document)).ThrowIfFailed();
    manager.Register(new("p", "Pinned", WindowKind.Document) { DisplayMode = TabDisplayMode.Pinned }).ThrowIfFailed();
    manager.Register(new("v", "Preview", WindowKind.Document) { DisplayMode = TabDisplayMode.Preview }).ThrowIfFailed();
    var group = DockTree.GroupOf(manager.Layout, "n")!;
    manager.ActivateTabByDigit(group.Id, 9, TabDisplayMode.Pinned).ThrowIfFailed(); Check(manager.Layout.ActiveWindowId == "p");
    manager.ActivateTabByDigit(group.Id, 1, TabDisplayMode.Preview).ThrowIfFailed(); Check(manager.Layout.ActiveWindowId == "v");
    var before = manager.Layout; Check(!manager.ActivateTabByDigit(group.Id, 8, TabDisplayMode.Pinned).Success); Check(ReferenceEquals(before, manager.Layout));
});
Console.WriteLine($"{passed}/{passed + failed} tab-layout specifications passed (including 500 seeded allocations).");
return failed == 0 ? 0 : 1;
