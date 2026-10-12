using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); } }
DockManager Manager()
{
    var m = new DockManager();
    m.RegisterMany([new("a", "A", WindowKind.Document), new("b", "B", WindowKind.Document)]).ThrowIfFailed();
    m.Register(new("tool", "Tool", WindowKind.Tool)).ThrowIfFailed(); return m;
}
var area = new DockRect(0, 0, 500, 300);
Test("Repeated minimized IDs reuse the result even in a new list", () => {
    var cache = new MinimizedWindowLayoutCache(); var first = cache.Arrange(["a", "b"], area);
    Check(ReferenceEquals(first, cache.Arrange(new List<string> { "a", "b" }, area)) && cache.BuildCount == 1 && cache.ReuseCount == 1);
});
Test("Caller mutation cannot silently change the cached ID signature", () => {
    var cache = new MinimizedWindowLayoutCache(); string[] ids = ["a", "b"];
    var first = cache.Arrange(ids, area); ids[1] = "c"; var next = cache.Arrange(ids, area);
    Check(!ReferenceEquals(first, next) && first.Windows.ContainsKey("b") && next.Windows.ContainsKey("c"));
    Array.Reverse(ids); Check(cache.Arrange(ids, area).Windows["c"].X == 0 && cache.BuildCount == 3);
});
Test("Every minimized geometry input invalidates the cache", () => {
    var cache = new MinimizedWindowLayoutCache(); _ = cache.Arrange(["a"], area);
    _ = cache.Arrange(["a"], area with { X = 5 }); _ = cache.Arrange(["a"], area with { Height = 500 });
    _ = cache.Arrange(["a"], area, new(200, 30)); _ = cache.Arrange(["a"], area, gap: 4);
    _ = cache.Arrange(["a", "b"], area); Check(cache.BuildCount == 6);
});
Test("Invalid input cannot replace a successful cache entry", () => {
    var cache = new MinimizedWindowLayoutCache(); var first = cache.Arrange(["a"], area);
    try { cache.Arrange(["a", "a"], area); throw new Exception("Duplicate accepted"); } catch (ArgumentException) { }
    try { cache.Arrange(["a"], area, gap: double.NaN); throw new Exception("NaN accepted"); } catch (ArgumentException) { }
    Check(ReferenceEquals(first, cache.Arrange(["a"], area)) && cache.BuildCount == 1);
});
Test("Clearing drops cached output without resetting lifetime counters", () => {
    var cache = new MinimizedWindowLayoutCache(); var first = cache.Arrange([], area); cache.Clear();
    Check(!ReferenceEquals(first, cache.Arrange([], area)) && cache.BuildCount == 2);
});
Test("Concurrent immutable requests share one minimized result", () => {
    var cache = new MinimizedWindowLayoutCache(); var results = new MinimizedWindowLayout[100];
    Parallel.For(0, results.Length, i => results[i] = cache.Arrange(["a", "b"], area));
    Check(cache.BuildCount == 1 && results.All(x => ReferenceEquals(x, results[0])));
});
Test("Active document wins and tool activation retains document MRU", () => {
    var m = Manager(); m.Activate("a").ThrowIfFailed(); Check(MdiPrimaryState.Create(m.Layout, ["b", "a"]).Window?.Id == "a");
    m.Activate("tool").ThrowIfFailed(); Check(MdiPrimaryState.Create(m.Layout, ["b", "a"]).Window?.Id == "a");
});
Test("Primary selection remains local to the supplied host", () => {
    var m = Manager(); m.Activate("b").ThrowIfFailed(); Check(MdiPrimaryState.Create(m.Layout, ["a"]).Window?.Id == "a");
    Check(MdiPrimaryState.Create(m.Layout, ["tool", "missing"]) == default);
});
Test("Closing the primary selects the remaining visible document", () => {
    var m = Manager(); m.Activate("a").ThrowIfFailed(); m.Close("a").ThrowIfFailed();
    Check(MdiPrimaryState.Create(m.Layout, ["a", "b"]).Window?.Id == "b");
    m.Close("b").ThrowIfFailed(); Check(MdiPrimaryState.Create(m.Layout, ["a", "b"]) == default);
});
Test("External chrome is needed only for a maximized frameless primary", () => {
    var m = Manager(); m.Activate("a").ThrowIfFailed();
    Check(!MdiPrimaryState.Create(m.Layout, ["a", "b"], maximizedFramesVisible: false).RequiresExternalChrome);
    m.SetMdiState("a", MdiWindowState.Maximized).ThrowIfFailed();
    Check(MdiPrimaryState.Create(m.Layout, ["a", "b"], maximizedFramesVisible: false).RequiresExternalChrome);
    Check(!MdiPrimaryState.Create(m.Layout, ["a", "b"]).RequiresExternalChrome);
});
Test("Presentation restrictions keep restore and explicit model commands available", () => {
    var m = Manager(); m.Activate("a").ThrowIfFailed(); m.SetMdiState("a", MdiWindowState.Maximized).ThrowIfFailed();
    var state = MdiPrimaryState.Create(m.Layout, ["a"], canMinimize: false, canMaximize: false);
    Check(!state.CanMinimize && !state.CanMaximize && state.CanRestore && state.CanClose);
});
Test("Layout locking disables state changes but retains permitted closing", () => {
    var m = Manager(); m.SetMdiState("a", MdiWindowState.Maximized).ThrowIfFailed(); m.SetLayoutLocked(true).ThrowIfFailed();
    var state = MdiPrimaryState.Create(m.Layout, ["a"], maximizedFramesVisible: false);
    Check(!state.CanMinimize && !state.CanMaximize && !state.CanRestore && state.CanClose && state.RequiresExternalChrome);
});
Test("Per-window capabilities and global policy both restrict commands", () => {
    var m = Manager(); m.UpdateWindow("a", w => w with { Capabilities = DockCapabilities.Drag | DockCapabilities.Close }).ThrowIfFailed();
    var state = MdiPrimaryState.Create(m.Layout, ["a"]);
    Check(!state.CanMinimize && !state.CanMaximize && state.CanClose);
    Check(!MdiPrimaryState.Create(m.Layout, ["a"], allowed: DockCapabilities.Drag).CanClose);
});
Test("Pure primary-state queries never change model revision or restore bounds", () => {
    var m = Manager(); var before = m.Layout;
    for (int i = 0; i < 1000; i++) _ = MdiPrimaryState.Create(before, ["a", "b"], maximizedFramesVisible: i % 2 == 0);
    Check(ReferenceEquals(before, m.Layout));
});
Console.WriteLine($"{passed}/{passed + failed} MDI presentation specifications passed.");
return failed == 0 ? 0 : 1;
