using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid geometry accepted"); }
void Test(string name, Action action) {
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
var rect = new DockRect(10, 20, 300, 200);
var area = new DockRect(0, 0, 800, 600);
Test("KeepVisible rejects nonfinite and negative viewports", () => {
    foreach (var viewport in new[] { area with { Width = double.NaN }, area with { Height = -1 }, area with { X = double.MaxValue, Width = double.MaxValue } })
        Reject(() => WindowGeometry.KeepVisible(rect, viewport));
    Reject(() => WindowGeometry.KeepVisible(rect, area, double.NaN));
    Check(WindowGeometry.KeepVisible(rect, new(0, 0, 0, 0)).IsValid);
});
Test("Resize rejects nonfinite deltas, invalid sides and inverted constraints", () => {
    Reject(() => WindowGeometry.Resize(rect, double.NaN, 0, DockSide.Right, DockSide.Bottom, new(10, 10), new(1000, 1000)));
    Reject(() => WindowGeometry.Resize(rect, 0, 0, DockSide.Top, DockSide.Bottom, new(10, 10), new(1000, 1000)));
    Reject(() => WindowGeometry.Resize(rect, 0, 0, DockSide.Right, (DockSide)99, new(10, 10), new(1000, 1000)));
    Reject(() => WindowGeometry.Resize(rect, 0, 0, DockSide.Right, DockSide.Bottom, new(100, 10), new(10, 1000)));
});
Test("Snap rejects invalid source and neighbor rectangles", () => {
    Reject(() => WindowGeometry.Snap(rect with { Width = 0 }, []));
    Reject(() => WindowGeometry.Snap(rect, [area with { X = double.NaN }]));
    Check(WindowGeometry.Snap(rect, []).Equals(rect));
});
Test("MDI arrangement rejects unknown modes and duplicate identities", () => {
    Reject(() => WindowGeometry.ArrangeMdi(["a"], area, (MdiArrangement)99));
    Reject(() => WindowGeometry.ArrangeMdi(["a", "a"], area, MdiArrangement.TileGrid));
    Reject(() => WindowGeometry.ArrangeMdi([""], area, MdiArrangement.Cascade));
    Reject(() => WindowGeometry.ArrangeMdi(["a"], area, MdiArrangement.Cascade, double.NaN));
});
Test("Large finite MDI extents avoid multiplication overflow", () => {
    var layout = WindowGeometry.ArrangeMdi(["a", "b", "c"], new(0, 0, double.MaxValue / 2, 800), MdiArrangement.TileVertically);
    Check(layout.Values.All(r => r.IsValid && double.IsFinite(r.Right)));
});
Test("Seeded valid resize results obey constraints and preserve opposite edges", () => {
    var random = new Random(3911);
    for (int i = 0; i < 1000; i++) {
        var result = WindowGeometry.Resize(rect, random.Next(-1000, 1000), random.Next(-1000, 1000), DockSide.Left, DockSide.Top, new(80, 60), new(600, 500));
        Check(result.Width >= 80 && result.Width <= 600 && result.Height >= 60 && result.Height <= 500);
        Check(Math.Abs(result.Right - rect.Right) < .001 && Math.Abs(result.Bottom - rect.Bottom) < .001);
    }
});
Test("One-axis pointer resizing preserves an untouched overconstrained extent", () => {
    var small = new DockRect(10,20,30,25);
    var taller = WindowGeometry.Resize(small,0,40,DockSide.Center,DockSide.Bottom,new(80,60),new(600,500));
    Check(taller == new DockRect(10,20,30,65));
    var wider = WindowGeometry.Resize(small,60,0,DockSide.Right,DockSide.Center,new(80,60),new(600,500));
    Check(wider == new DockRect(10,20,90,25));
    Check(WindowGeometry.Resize(small,0,0,DockSide.Center,DockSide.Center,new(80,60),new(600,500)) == small);
});
Console.WriteLine($"{passed}/{passed + failed} geometry review specifications passed (including 1,000 seeded resizes).");
return failed == 0 ? 0 : 1;
