using System.Diagnostics;
using DockingScene.Core;

int passed = 0, failed = 0;
void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockWindow[] Documents(int count) => Enumerable.Range(0, count).Select(i => new DockWindow("d" + i, "Document " + i, WindowKind.Document)).ToArray();
Test("Ten thousand registrations are one revision and one notification", () => {
    var manager = new DockManager(); int changing = 0, changed = 0;
    manager.Changing += (_, _) => changing++; manager.Changed += (_, _) => changed++;
    manager.RegisterMany(Documents(10000)).ThrowIfFailed();
    Check(manager.Layout.Windows.Count == 10000 && manager.Layout.Revision == 1 && changing == 1 && changed == 1);
    Check(manager.Layout.ActiveWindowId == "d0" && DockLayoutIndex.For(manager.Layout).WindowCount == 10000);
    DockLayoutValidator.Validate(manager.Layout);
    manager.Undo().ThrowIfFailed(); Check(manager.Layout.Windows.Count == 0);
    manager.Redo().ThrowIfFailed(); Check(manager.Layout.Windows.Count == 10000);
});
Test("Duplicate and existing ids roll back the entire batch", () => {
    var manager = new DockManager(); manager.Register(new("existing", "Existing", WindowKind.Document)).ThrowIfFailed();
    var before = manager.Layout;
    Check(!manager.RegisterMany([new("new", "New", WindowKind.Document), new("existing", "Duplicate", WindowKind.Document)]).Success);
    Check(ReferenceEquals(before, manager.Layout));
    Check(!manager.RegisterMany([new("x", "X", WindowKind.Document), new("x", "X", WindowKind.Document)]).Success);
    Check(ReferenceEquals(before, manager.Layout));
});
Test("Invalid metadata and null items reject all registrations", () => {
    var manager = new DockManager(); var before = manager.Layout;
    Check(!manager.RegisterMany([new("good", "Good", WindowKind.Document), new("bad", "Bad", WindowKind.Document) { MinimumSize = new(double.NaN, 10) }]).Success);
    Check(!manager.RegisterMany([null!]).Success && ReferenceEquals(before, manager.Layout));
});
Test("Cancellation leaves history and contents untouched", () => {
    var manager = new DockManager(); var before = manager.Layout;
    manager.Changing += (_, e) => e.Cancel = true;
    Check(!manager.RegisterMany(Documents(100)).Success && ReferenceEquals(before, manager.Layout) && !manager.CanUndo);
});
Test("Budget enforcement stops unbounded enumeration", () => {
    var manager = new DockManager(policy: new() { MaximumWindows = 100 }); int enumerated = 0;
    IEnumerable<DockWindow> Source() { while (true) { enumerated++; yield return new("d" + enumerated, "Document", WindowKind.Document); } }
    Check(!manager.RegisterMany(Source()).Success && enumerated == 101 && manager.Layout.Windows.Count == 0);
});
Test("Stale revisions reject before enumerating caller data", () => {
    var manager = new DockManager(); manager.RegisterMany(Documents(2)).ThrowIfFailed(); int enumerated = 0;
    IEnumerable<DockWindow> Source() { enumerated++; yield return new("later", "Later", WindowKind.Document); }
    Check(!manager.RegisterMany(Source(), expectedRevision: 0).Success && enumerated == 0);
});
Test("Empty batches retain identity and redo", () => {
    var manager = new DockManager(); manager.RegisterMany(Documents(2)).ThrowIfFailed(); manager.Undo().ThrowIfFailed();
    var before = manager.Layout; manager.RegisterMany([]).ThrowIfFailed();
    Check(ReferenceEquals(before, manager.Layout) && manager.CanRedo);
});
Test("Mixed closed batches work while mixed open batches reject atomically", () => {
    var windows = new DockWindow[] { new("document", "Document", WindowKind.Document), new("tool", "Tool", WindowKind.Tool) };
    var manager = new DockManager(); Check(!manager.RegisterMany(windows).Success && manager.Layout.Windows.Count == 0);
    manager.RegisterMany(windows, open: false).ThrowIfFailed();
    Check(manager.Layout.HiddenWindows.Length == 2 && manager.Layout.ActiveWindowId is null);
});
Test("Explicit target attachment preserves existing tabs and stable pinned order", () => {
    var manager = new DockManager(); manager.Register(new("first", "First", WindowKind.Document)).ThrowIfFailed();
    string target = DockTree.GroupOf(manager.Layout, "first")!.Id;
    manager.RegisterMany([new("normal", "Normal", WindowKind.Document), new("pin", "Pinned", WindowKind.Document) { DisplayMode = TabDisplayMode.Pinned }], targetId: target).ThrowIfFailed();
    Check(DockTree.GroupOf(manager.Layout, "normal")!.Windows.SequenceEqual(new[] { "pin", "first", "normal" }));
    var before = manager.Layout; Check(!manager.RegisterMany([new("bad", "Bad", WindowKind.Document)], targetId: "missing").Success);
    Check(ReferenceEquals(before, manager.Layout));
});
Test("Bulk registration allocation benchmark preserves all models", () => {
    var input = Documents(500);
    (long Bytes, double Ms, DockManager Manager) Measure(bool bulk)
    {
        var manager = new DockManager(policy: new() { HistoryLimit = 0 });
        long initial = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        if (bulk) manager.RegisterMany(input).ThrowIfFailed();
        else foreach (var window in input) manager.Register(window).ThrowIfFailed();
        watch.Stop(); return (GC.GetAllocatedBytesForCurrentThread() - initial, watch.Elapsed.TotalMilliseconds, manager);
    }
    _ = Measure(true); var single = Measure(false); var batched = Measure(true);
    Check(single.Manager.Layout.Windows.Count == batched.Manager.Layout.Windows.Count && batched.Manager.Layout.Revision == 1);
    Check(batched.Bytes < single.Bytes / 4, "Bulk path should not validate/copy every intermediate snapshot.");
    Console.WriteLine($"BENCHMARK register 500 windows: individual={single.Ms:F3}ms {single.Bytes}B; bulk={batched.Ms:F3}ms {batched.Bytes}B");
});
Console.WriteLine($"{passed}/{passed + failed} bulk registration specifications passed.");
return failed == 0 ? 0 : 1;
