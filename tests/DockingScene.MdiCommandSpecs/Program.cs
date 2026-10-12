using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); } }
DockManager Manager()
{
    var manager = new DockManager();
    manager.RegisterMany(new[] { new DockWindow("a", "A", WindowKind.Document), new DockWindow("b", "B", WindowKind.Document) }).ThrowIfFailed();
    manager.Register(new("tool", "Tool", WindowKind.Tool)).ThrowIfFailed(); return manager;
}
Test("Primary commands target an active member, not an active outside tool", () => {
    var manager = Manager(); manager.Activate("b").ThrowIfFailed(); manager.Activate("tool").ThrowIfFailed();
    var state = MdiPrimaryWindow.Resolve(manager.Layout, new[] { "a", "b" });
    Check(state.Id == "b" && state.CanMinimize && state.CanMaximize && !state.CanRestore && state.CanClose);
});
Test("Hidden and unknown members cannot receive primary commands", () => {
    var manager = Manager(); manager.Activate("b").ThrowIfFailed(); manager.Close("b").ThrowIfFailed();
    Check(MdiPrimaryWindow.Resolve(manager.Layout, new[] { "missing", "b", "a" }).Id == "a");
    Check(MdiPrimaryWindow.Resolve(manager.Layout, new[] { "missing", "b" }) == default);
});
Test("Host ordering breaks ties without activation history", () => {
    var manager = Manager(); var layout = manager.Layout with { ActiveWindowId = null, ActivationHistory = [] };
    Check(MdiPrimaryWindow.Resolve(layout, new[] { "b", "a" }).Id == "b");
});
Test("Minimized and maximized state command availability is independent", () => {
    var manager = Manager(); manager.SetMdiState("a", MdiWindowState.Minimized).ThrowIfFailed();
    var min = MdiPrimaryWindow.Resolve(manager.Layout, new[] { "a" });
    Check(!min.CanMinimize && min.CanRestore && min.CanMaximize);
    manager.SetMdiState("a", MdiWindowState.Maximized).ThrowIfFailed();
    var max = MdiPrimaryWindow.Resolve(manager.Layout, new[] { "a" });
    Check(max.CanMinimize && max.CanRestore && !max.CanMaximize);
});
Test("Presentation policies never prevent restoring a maximized window", () => {
    var manager = Manager(); manager.SetMdiState("a", MdiWindowState.Maximized).ThrowIfFailed();
    var state = MdiPrimaryWindow.Resolve(manager.Layout, new[] { "a" }, canMinimize: false, canMaximize: false);
    Check(!state.CanMinimize && !state.CanMaximize && state.CanRestore);
});
Test("Lock and effective capabilities remain separate from close policy", () => {
    var manager = Manager(); manager.SetMdiState("a", MdiWindowState.Maximized).ThrowIfFailed(); manager.SetLayoutLocked(true).ThrowIfFailed();
    var locked = MdiPrimaryWindow.Resolve(manager.Layout, new[] { "a" });
    Check(!locked.CanMinimize && !locked.CanMaximize && !locked.CanRestore && locked.CanClose);
    Check(!MdiPrimaryWindow.Resolve(manager.Layout, new[] { "a" }, DockCapabilities.None).CanClose);
});
Test("Separate hosts never select one another's windows", () => {
    var manager = Manager(); manager.Activate("b").ThrowIfFailed();
    Check(MdiPrimaryWindow.Resolve(manager.Layout, new[] { "a" }).Id == "a");
    Check(MdiPrimaryWindow.Resolve(manager.Layout, new[] { "b" }).Id == "b");
});
Test("Minimized cache retains its exact plan on equivalent ordered input", () => {
    var cache = new MinimizedWindowLayoutCache(); var area = new DockRect(0,0,800,500);
    var first = cache.Arrange(new[] { "a", "b" }, area);
    Check(ReferenceEquals(first, cache.Arrange(new[] { "a", "b" }, area)) && cache.BuildCount == 1 && cache.ReuseCount == 1);
});
Test("Minimized cache does not retain the caller's mutable id array", () => {
    var cache = new MinimizedWindowLayoutCache(); var ids = new[] { "a", "b" }; var area = new DockRect(0,0,800,500);
    var first = cache.Arrange(ids, area); ids[0] = "c";
    var next = cache.Arrange(ids, area); Check(next.Windows.ContainsKey("c") && first.Windows.ContainsKey("a") && cache.BuildCount == 2);
});
Test("Order viewport caption and spacing changes invalidate minimized plans", () => {
    var cache = new MinimizedWindowLayoutCache(); var area = new DockRect(0,0,800,500);
    cache.Arrange(new[] { "a", "b" }, area); cache.Arrange(new[] { "b", "a" }, area);
    cache.Arrange(new[] { "b", "a" }, area with { Width = 200 });
    cache.Arrange(new[] { "b", "a" }, area, new(170,30));
    cache.Arrange(new[] { "b", "a" }, area, new(170,30), 4); Check(cache.BuildCount == 5);
});
Test("Invalid minimized input cannot poison a previous valid cache entry", () => {
    var cache = new MinimizedWindowLayoutCache(); var area = new DockRect(0,0,800,500); var first = cache.Arrange(new[] { "a" }, area);
    try { cache.Arrange(new[] { "a", "a" }, area); throw new Exception("Duplicate accepted"); } catch (ArgumentException) { }
    try { cache.Arrange(new[] { "a" }, area, gap: double.NaN); throw new Exception("NaN accepted"); } catch (ArgumentException) { }
    Check(ReferenceEquals(first, cache.Arrange(new[] { "a" }, area)) && cache.BuildCount == 1);
    cache.Clear(); Check(!ReferenceEquals(first, cache.Arrange(new[] { "a" }, area)) && cache.BuildCount == 2);
});
Test("Five hundred cached minimized layouts match independent arrangements", () => {
    var random = new Random(91284); var cache = new MinimizedWindowLayoutCache();
    for (int i = 0; i < 500; i++) {
        var ids = Enumerable.Range(0, random.Next(0,50)).Select(n => "d" + n).ToArray();
        var area = new DockRect(-10,5,random.Next(50,1500),random.Next(40,900));
        var actual = cache.Arrange(ids, area); var expected = MinimizedWindowLayout.Arrange(ids, area);
        Check(actual.RequiredHeight == expected.RequiredHeight && actual.IsOverflowing == expected.IsOverflowing && actual.Windows.Count == expected.Windows.Count);
        Check(expected.Windows.All(p => actual.Windows[p.Key] == p.Value));
    }
});
Console.WriteLine($"{passed}/{passed + failed} primary MDI command and minimized cache specifications passed.");
return failed == 0 ? 0 : 1;
