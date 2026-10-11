using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); } }
string[] Ids(int count) => Enumerable.Range(0, count).Select(i => "d" + i).ToArray();
Test("Minimized captions wrap above a complete bottom row", () => {
    var plan = MinimizedWindowLayout.Arrange(Ids(5), new(10, 20, 508, 200));
    Check(plan.Windows["d0"] == new DockRect(10, 194, 168, 26));
    Check(plan.Windows["d2"] == new DockRect(350, 194, 168, 26));
    Check(plan.Windows["d3"] == new DockRect(10, 166, 168, 26));
    Check(plan.RequiredHeight == 54 && !plan.IsOverflowing);
});
Test("Partial columns never overlap the preceding row", () => {
    var plan = MinimizedWindowLayout.Arrange(Ids(6), new(0, 0, 500, 300));
    Check(plan.Windows["d2"].X == 0 && plan.Windows["d2"].Y < plan.Windows["d0"].Y);
    Check(plan.RequiredHeight == 82);
});
Test("A narrow viewport clips caption width but not its height", () => {
    var plan = MinimizedWindowLayout.Arrange(Ids(2), new(0, 0, 70, 120));
    Check(plan.Windows.Values.All(r => r.Width == 70 && r.Height == 26));
    Check(plan.Windows["d1"].Y == 66);
});
Test("Insufficient height is reported without duplicate or overlapping slots", () => {
    var plan = MinimizedWindowLayout.Arrange(Ids(8), new(0, 0, 168, 30));
    Check(plan.IsOverflowing && plan.RequiredHeight == 222);
    Check(plan.Windows.Values.Select(r => r.Y).Distinct().Count() == 8 && plan.Windows["d7"].Y < 0);
});
Test("Empty layout has no required height", () => {
    var plan = MinimizedWindowLayout.Arrange([], new(0, 0, 100, 100));
    Check(plan.Windows.Count == 0 && plan.RequiredHeight == 0 && !plan.IsOverflowing);
});
Test("Duplicate blank and invalid numerical inputs reject", () => {
    Action[] cases = [() => MinimizedWindowLayout.Arrange(["a", "a"], new(0,0,100,100)),
        () => MinimizedWindowLayout.Arrange([" "], new(0,0,100,100)),
        () => MinimizedWindowLayout.Arrange(["a"], new(0,0,0,100)),
        () => MinimizedWindowLayout.Arrange(["a"], new(0,0,100,100), new(0,26)),
        () => MinimizedWindowLayout.Arrange(["a"], new(0,0,100,100), gap: double.NaN),
        () => MinimizedWindowLayout.Arrange(["a"], new(0,0,100,100), gap: -1),
        () => MinimizedWindowLayout.Arrange(["a"], new(0,0,100,100), new(168, double.MaxValue), double.MaxValue)];
    foreach (var action in cases) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
});
Test("Extreme finite host widths do not overflow integer column counts", () => {
    var plan = MinimizedWindowLayout.Arrange(Ids(5), new(0,0,double.MaxValue,300));
    Check(!plan.IsOverflowing && plan.Windows["d4"].X == 680);
});
Test("Resize arrangement preserves caller order and has no overlap in 500 seeded layouts", () => {
    var random = new Random(87223);
    for (int i = 0; i < 500; i++) {
        var ids = Ids(random.Next(1, 30)); var area = new DockRect(-10, 5, random.Next(30,900), random.Next(20,800));
        var plan = MinimizedWindowLayout.Arrange(ids, area); var rectangles = ids.Select(id => plan.Windows[id]).ToArray();
        Check(rectangles[0].X == area.X && rectangles[0].Bottom == area.Bottom);
        for (int a = 0; a < rectangles.Length; a++) for (int b = a + 1; b < rectangles.Length; b++) {
            var x = rectangles[a]; var y = rectangles[b];
            Check(x.Right <= y.X || y.Right <= x.X || x.Bottom <= y.Y || y.Bottom <= x.Y);
        }
    }
});
Test("Minimize maximize and restore never overwrite restored window bounds", () => {
    var manager = new DockManager(); manager.Register(new("d","Document",WindowKind.Document)).ThrowIfFailed();
    manager.SetMdiBounds("d", new(67,88,500,370)).ThrowIfFailed(); var bounds = manager.Layout.Windows["d"].MdiBounds;
    foreach (var state in new[] { MdiWindowState.Minimized, MdiWindowState.Maximized, MdiWindowState.Normal }) {
        manager.SetMdiState("d", state).ThrowIfFailed(); Check(manager.Layout.Windows["d"].MdiBounds == bounds);
    }
});
Test("Move-only capability works without allowing resize", () => {
    var manager = new DockManager(); manager.Register(new("d","Document",WindowKind.Document) { Capabilities = DockCapabilities.Drag }).ThrowIfFailed();
    var size = manager.Layout.Windows["d"].MdiBounds;
    manager.MoveMdiWindow("d", 40, 50).ThrowIfFailed();
    Check(manager.Layout.Windows["d"].MdiBounds == (size with { X = 40, Y = 50 }));
    Check(!manager.SetMdiBounds("d", size with { Width = 600 }).Success);
});
Test("Stale or minimized moves preserve restored bounds and revision", () => {
    var manager = new DockManager(); manager.Register(new("d","Document",WindowKind.Document)).ThrowIfFailed();
    var revision = manager.Layout.Revision; manager.SetMdiState("d", MdiWindowState.Minimized).ThrowIfFailed();
    var before = manager.Layout;
    Check(!manager.MoveMdiWindow("d", 200, 200, revision).Success);
    Check(!manager.MoveMdiWindow("d", 200, 200).Success && ReferenceEquals(before, manager.Layout));
});
Console.WriteLine($"{passed}/{passed + failed} MDI review specifications passed.");
return failed == 0 ? 0 : 1;
