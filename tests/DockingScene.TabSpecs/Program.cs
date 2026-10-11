using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
Test("Measured widths accumulate without character-count heuristics", () => { var tabs = new TabStripMetrics([40, 120, 70]); Check(tabs.Offsets.SequenceEqual(new[] { 0d, 40, 160, 230 })); });
Test("Empty strips have no realized items", () => { var tabs = new TabStripMetrics([]); Check(tabs.Length == 0 && tabs.InsertionIndex(100) == 0 && tabs.VisibleRange(0, 100).Count == 0); });
Test("Visible range excludes an item ending on the left boundary", () => { var tabs = new TabStripMetrics([40, 120, 70]); Check(tabs.VisibleRange(40, 120) == new TabRange(1, 1)); });
Test("Partially visible items are realized", () => { var tabs = new TabStripMetrics([40, 120, 70]); Check(tabs.VisibleRange(39, 122) == new TabRange(0, 3)); });
Test("Overscan realizes only adjacent intersecting items", () => { var tabs = new TabStripMetrics([40, 120, 70]); Check(tabs.VisibleRange(40, 120, 1) == new TabRange(0, 3)); });
Test("Viewport beyond content returns an empty range", () => Check(new TabStripMetrics([40, 120]).VisibleRange(200, 100).Count == 0));
Test("Insertion index switches at measured midpoints", () => { var tabs = new TabStripMetrics([40, 120, 70]); Check(tabs.InsertionIndex(-50) == 0 && tabs.InsertionIndex(19) == 0 && tabs.InsertionIndex(20) == 1 && tabs.InsertionIndex(99) == 1 && tabs.InsertionIndex(100) == 2 && tabs.InsertionIndex(300) == 3); });
Test("Bring selected tab into view from either edge", () => { var tabs = new TabStripMetrics([60, 60, 60]); Check(tabs.BringIntoView(0, 80, 100) == 0 && tabs.BringIntoView(2, 0, 100) == 80); });
Test("Oversized tabs expose their title", () => { var tabs = new TabStripMetrics([40, 200, 50]); Check(tabs.BringIntoView(1, 0, 100) == 40); });
Test("Ten thousand variable tabs use a bounded visible interval", () => { var tabs = new TabStripMetrics(Enumerable.Range(0, 10000).Select(i => 50d + i % 91)); var range = tabs.VisibleRange(350000, 800, 200); Check(range.Count <= 25 && range.First > 3000); });
Test("Invalid tab lengths are rejected", () => { foreach (double length in new[] { 0d, -1, double.NaN, double.PositiveInfinity }) { try { _ = new TabStripMetrics([length]); } catch (ArgumentOutOfRangeException) { continue; } throw new Exception("Invalid length accepted"); } });
Test("Cumulative overflow is rejected", () => { try { _ = new TabStripMetrics([double.MaxValue, double.MaxValue]); } catch (ArgumentOutOfRangeException) { return; } throw new Exception("Overflow accepted"); });
Test("Same-group drag insertion adjusts for removed source tabs", () => {
    var manager = new DockManager(); foreach (var id in new[] { "a", "b", "c", "d" }) manager.Register(new(id, id, WindowKind.Document)).ThrowIfFailed();
    var group = DockTree.GroupOf(manager.Layout, "a")!; int insertion = 3 - group.Windows.Take(3).Count(id => id == "a");
    manager.Dock("a", group.Id, DockSide.Center, insertion).ThrowIfFailed(); Check(DockTree.GroupOf(manager.Layout, "a")!.Windows.SequenceEqual(new[] { "b", "c", "a", "d" }));
});
Console.WriteLine($"{passed}/{passed + failed} tab geometry specifications passed.");
return failed == 0 ? 0 : 1;
