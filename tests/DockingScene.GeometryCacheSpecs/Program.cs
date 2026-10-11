using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockManager Manager()
{
    var manager = new DockManager();
    manager.RegisterMany(Enumerable.Range(0, 20).Select(i => new DockWindow("d" + i, "Document " + i, WindowKind.Document))).ThrowIfFailed();
    manager.Register(new("tool", "Tool", WindowKind.Tool), targetId: "workspace", side: DockSide.Right).ThrowIfFailed();
    return manager;
}
var viewport = new DockRect(0, 0, 1280, 800);
Test("Repeated layout returns the same result without rebuilding", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var first = cache.Arrange(m.Layout, viewport);
    Check(ReferenceEquals(first, cache.Arrange(m.Layout, viewport))); Check(cache.BuildCount == 1 && cache.ReuseCount == 1);
});
Test("Twenty document activations reuse geometry", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var first = cache.Arrange(m.Layout, viewport);
    for (int i = 0; i < 20; i++) { m.Activate("d" + i).ThrowIfFailed(); Check(ReferenceEquals(first, cache.Arrange(m.Layout, viewport))); }
    Check(cache.BuildCount == 1);
});
Test("Title dirty read-only capability and tab placement changes reuse rectangles", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var first = cache.Arrange(m.Layout, viewport);
    m.UpdateWindow("d0", w => w with { Title = "Changed", IsModified = true, IsReadOnly = true, Capabilities = DockCapabilities.Close }).ThrowIfFailed();
    m.SetTabPlacement(DockTree.GroupOf(m.Layout, "d0")!.Id, TabPlacement.Bottom).ThrowIfFailed();
    Check(ReferenceEquals(first, cache.Arrange(m.Layout, viewport)));
});
Test("Viewport origin size and DPI invalidate", () => {
    var m = Manager(); var cache = new DockGeometryCache(); _ = cache.Arrange(m.Layout, viewport);
    _ = cache.Arrange(m.Layout, viewport with { X = 10 }); _ = cache.Arrange(m.Layout, viewport with { Width = 1000 });
    _ = cache.Arrange(m.Layout, viewport, new() { DpiScale = 2 }); Check(cache.BuildCount == 4);
});
Test("All chrome metrics invalidate", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var metrics = new DockMetrics(); _ = cache.Arrange(m.Layout, viewport, metrics);
    foreach (var changed in new[] { metrics with { Splitter = 8 }, metrics with { TabHeight = 30 }, metrics with { TitleHeight = 30 }, metrics with { Border = 2 }, metrics with { AutoHideRail = 30 } })
        _ = cache.Arrange(m.Layout, viewport, changed);
    Check(cache.BuildCount == 6);
});
Test("Minimum maximum and titlebar constraints invalidate", () => {
    var m = Manager(); var cache = new DockGeometryCache(); _ = cache.Arrange(m.Layout, viewport);
    m.UpdateWindow("tool", w => w with { MinimumSize = new(250, 100) }).ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport);
    m.UpdateWindow("tool", w => w with { MaximumSize = new(600, 600) }).ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport);
    m.UpdateWindow("tool", w => w with { HasTitleBar = false }).ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport);
    Check(cache.BuildCount == 4);
});
Test("Split ratio and structure invalidate", () => {
    var m = Manager(); var cache = new DockGeometryCache(); _ = cache.Arrange(m.Layout, viewport);
    var split = DockTree.Nodes(m.Layout).OfType<DockSplit>().First(); m.ResizeSplit(split.Id, .6).ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport);
    m.Dock("d1", "d0", DockSide.Bottom).ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport); Check(cache.BuildCount == 3);
});
Test("Floating position maximize and redocking invalidate", () => {
    var m = Manager(); var cache = new DockGeometryCache(); _ = cache.Arrange(m.Layout, viewport);
    m.Float("d0").ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport); var host = m.Layout.FloatingHosts[0];
    m.SetFloatingBounds(host.Id, host.Bounds with { X = 222 }).ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport);
    m.SetFloatingMaximized(host.Id, true).ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport);
    m.Dock("d0").ThrowIfFailed(); _ = cache.Arrange(m.Layout, viewport); Check(cache.BuildCount == 5);
});
Test("Auto-hide presence invalidates but popup size does not", () => {
    var m = Manager(); var cache = new DockGeometryCache(); _ = cache.Arrange(m.Layout, viewport);
    m.AutoHide("tool", DockSide.Left).ThrowIfFailed(); var first = cache.Arrange(m.Layout, viewport);
    m.SetAutoHideSize("tool", 400).ThrowIfFailed(); Check(ReferenceEquals(first, cache.Arrange(m.Layout, viewport)));
    m.AutoHide("tool", DockSide.Right).ThrowIfFailed(); Check(!ReferenceEquals(first, cache.Arrange(m.Layout, viewport)));
});
Test("Failed arrangement preserves the previous valid cache entry", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var first = cache.Arrange(m.Layout, viewport);
    try { cache.Arrange(m.Layout, viewport, new() { TabHeight = double.NaN }); throw new Exception("Accepted NaN"); } catch (ArgumentException) { }
    Check(ReferenceEquals(first, cache.Arrange(m.Layout, viewport)) && cache.BuildCount == 1);
});
Test("Clear releases cached output and forces rebuilding", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var first = cache.Arrange(m.Layout, viewport); cache.Clear();
    Check(!ReferenceEquals(first, cache.Arrange(m.Layout, viewport)) && cache.BuildCount == 2);
});
Test("Seeded transitions match uncached rectangles and warnings", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var random = new Random(81603);
    for (int i = 0; i < 300; i++)
    {
        var id = "d" + random.Next(20);
        _ = (i % 6) switch { 0 => m.Activate(id), 1 => m.Float(id), 2 => m.Dock(id), 3 => m.Close(id), 4 => m.Open(id), _ => m.Undo() };
        var actual = cache.Arrange(m.Layout, viewport); var expected = DockLayoutEngine.Arrange(m.Layout, viewport);
        Check(actual.Nodes.Count == expected.Nodes.Count && expected.Nodes.All(p => actual.Nodes[p.Key] == p.Value));
        Check(actual.Splitters.SequenceEqual(expected.Splitters) && actual.ConstraintWarnings.SequenceEqual(expected.ConstraintWarnings));
        Check(expected.AutoHideRails.Count == actual.AutoHideRails.Count && expected.AutoHideRails.All(p => actual.AutoHideRails[p.Key] == p.Value));
    }
});
Test("Extreme finite minimums shrink proportionally without intermediate overflow", () => {
    var m = new DockManager();
    m.Register(new("a", "A", WindowKind.Tool) { MinimumSize = new(3e307, 50), MaximumSize = new(4e307, 1000) }, targetId: "workspace", side: DockSide.Left).ThrowIfFailed();
    m.Register(new("b", "B", WindowKind.Tool) { MinimumSize = new(3e307, 50), MaximumSize = new(4e307, 1000) }, targetId: "a", side: DockSide.Right).ThrowIfFailed();
    var rect = new DockRect(0, 0, 5e307, 800);
    var actual = new DockGeometryCache().Arrange(m.Layout, rect);
    var a = actual.Nodes[DockTree.GroupOf(m.Layout, "a")!.Id];
    var b = actual.Nodes[DockTree.GroupOf(m.Layout, "b")!.Id];
    Check(double.IsFinite(a.Width) && double.IsFinite(b.Width) && a.Width > 0 && b.Width > 0);
    Check(Math.Abs(a.Width / b.Width - 1) < .00001);
});
Console.WriteLine($"{passed}/{passed + failed} geometry cache specifications passed.");
return failed == 0 ? 0 : 1;
