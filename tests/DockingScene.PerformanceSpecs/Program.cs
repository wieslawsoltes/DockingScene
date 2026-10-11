using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Mvvm;

int passed = 0, failed = 0;
void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e); }
}
DockLayout Large(int count, bool hidden = false)
{
    var ids = Enumerable.Range(0, count).Select(i => "d" + i).ToImmutableArray();
    var windows = ids.ToImmutableDictionary(id => id, id => new DockWindow(id, id, WindowKind.Document));
    DockNode Balance(DockNode[] nodes, int start, int length) => length == 1 ? nodes[start] : new DockSplit("s" + start + "-" + length, SplitAxis.Horizontal, .5,
        Balance(nodes, start, length / 2), Balance(nodes, start + length / 2, length - length / 2));
    var groups = ids.Chunk(25).Select((part, i) => (DockNode)new DockGroup("g" + i, GroupKind.Documents, part.ToImmutableArray(), part[0])).ToArray();
    return new() { Windows = windows, Root = new DockWorkspace("workspace", hidden ? null : Balance(groups, 0, groups.Length)),
        HiddenWindows = hidden ? ids : [], ActiveWindowId = hidden ? null : ids[0], ActivationHistory = hidden ? [] : [ids[0]] };
}
WindowLocation Scan(DockLayout layout, string id)
{
    foreach (var group in DockTree.Descendants(layout.Root).OfType<DockGroup>())
    {
        int index = group.Windows.IndexOf(id);
        if (index >= 0) return new(group.Kind == GroupKind.Documents ? WindowPlacement.Document : WindowPlacement.Docked, group.Id, null, DockSide.Center, index);
    }
    foreach (var host in layout.FloatingHosts)
        foreach (var group in DockTree.Descendants(host.Root).OfType<DockGroup>())
        { int i = group.Windows.IndexOf(id); if (i >= 0) return new(WindowPlacement.Floating, group.Id, host.Id, DockSide.Center, i); }
    foreach (var group in layout.AutoHideGroups)
    { int i = group.Windows.IndexOf(id); if (i >= 0) return new(WindowPlacement.AutoHidden, group.Id, null, group.Side, i); }
    return new(WindowPlacement.Hidden, null, null, DockSide.Center, -1);
}
Test("Index agrees with independent scans for 10,000 windows", () => {
    var layout = Large(10000); DockLayoutValidator.Validate(layout);
    var index = DockLayoutIndex.For(layout); Check(index.WindowCount == 10000);
    for (int i = 0; i < 10000; i++) Check(index.Locate("d" + i) == Scan(layout, "d" + i));
    Check(index.Nodes.SequenceEqual(DockTree.Descendants(layout.Root)));
    Check(ReferenceEquals(index, DockLayoutIndex.For(layout)));
});
Test("Equal revision snapshots never alias their lookup cache", () => {
    var a = Large(50); var b = a with { Root = new DockWorkspace("workspace"), HiddenWindows = a.Windows.Keys.ToImmutableArray(), ActiveWindowId = null, ActivationHistory = [] };
    Check(a.Revision == b.Revision); Check(!ReferenceEquals(DockLayoutIndex.For(a), DockLayoutIndex.For(b)));
    Check(DockTree.Locate(a, "d0").Placement == WindowPlacement.Document && DockTree.Locate(b, "d0").Placement == WindowPlacement.Hidden);
});
Test("Concurrent cache readers receive the same complete index", () => {
    var layout = Large(1000); var indexes = new DockLayoutIndex[64];
    Parallel.For(0, indexes.Length, i => indexes[i] = DockLayoutIndex.For(layout));
    Check(indexes.All(i => ReferenceEquals(i, indexes[0]) && i.WindowCount == 1000));
});
Test("Index holds no strong reference to its cache key", () => {
    var (weak, index) = MakeWeakIndex();
    for (int i = 0; i < 3 && weak.IsAlive; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
    Check(!weak.IsAlive, "Retained index kept layout key alive"); GC.KeepAlive(index);
});
Test("No-op maps retain every node and snapshot identity", () => {
    var layout = Large(1000); Check(ReferenceEquals(layout, DockTree.Map(layout, node => node)));
});
Test("Local edits retain unrelated split branches", () => {
    var layout = Large(100); var root = (DockSplit)((DockWorkspace)layout.Root!).Content!;
    var leaf = DockTree.Descendants(root.First).OfType<DockGroup>().First();
    var next = DockTree.Map(layout, node => node.Id == leaf.Id ? leaf with { SelectedId = leaf.Windows[1] } : node);
    var changed = (DockSplit)((DockWorkspace)next.Root!).Content!;
    Check(ReferenceEquals(root.Second, changed.Second)); Check(!ReferenceEquals(root.First, changed.First));
});
Test("Repeated activation is a no-op without events or new revision", () => {
    var manager = new DockManager(Large(50)); var before = manager.Layout; int events = 0;
    manager.Changed += (_, _) => events++;
    for (int i = 0; i < 1000; i++) manager.Activate("d0").ThrowIfFailed();
    Check(ReferenceEquals(before, manager.Layout) && events == 0);
});
Test("No-op metadata does not clear redo history", () => {
    var manager = new DockManager(Large(50)); manager.Float("d1").ThrowIfFailed(); manager.Undo().ThrowIfFailed();
    var before = manager.Layout; manager.UpdateWindow("d0", w => w with { Title = w.Title }).ThrowIfFailed();
    Check(ReferenceEquals(before, manager.Layout) && manager.CanRedo);
});
Test("Metadata without display-mode changes retains container identity", () => {
    var manager = new DockManager(Large(100)); var root = manager.Layout.Root;
    manager.UpdateWindow("d0", w => w with { Title = "Changed" }).ThrowIfFailed(); Check(ReferenceEquals(root, manager.Layout.Root));
});
Test("MRU fallback selects the visible tab after closing active document", () => {
    var manager = new DockManager(Large(25)); manager.Activate("d12").ThrowIfFailed(); manager.Activate("d5").ThrowIfFailed();
    manager.Close("d5").ThrowIfFailed(); Check(manager.Layout.ActiveWindowId == "d12");
    Check(DockTree.GroupOf(manager.Layout, "d12")!.SelectedId == "d12");
});
Test("MVVM activation notifies only old and new active proxies", () => {
    var manager = new DockManager(Large(1000)); using var model = new DockWorkspaceViewModel(manager);
    int notifications = 0; foreach (var window in model.Windows) window.PropertyChanged += (_, _) => notifications++;
    manager.Activate("d4").ThrowIfFailed(); Check(notifications == 2, "Expected two proxy changes, got " + notifications);
});
Test("MVVM metadata does not invalidate unrelated proxies", () => {
    var manager = new DockManager(Large(1000)); using var model = new DockWorkspaceViewModel(manager);
    int notifications = 0; foreach (var window in model.Windows) window.PropertyChanged += (_, _) => notifications++;
    manager.UpdateWindow("d4", w => w with { Description = "Changed" }).ThrowIfFailed(); Check(notifications == 1);
});
Test("MVVM lock updates command capabilities for all windows", () => {
    var manager = new DockManager(Large(100)); using var model = new DockWorkspaceViewModel(manager);
    int notifications = 0; foreach (var window in model.Windows) window.PropertyChanged += (_, _) => notifications++;
    manager.SetLayoutLocked(true).ThrowIfFailed(); Check(notifications == 100 && !model.Find("d0")!.FloatCommand.CanExecute(null));
});
Test("Cached locations follow undo, redo, float and auto-hide transitions", () => {
    var manager = new DockManager(Large(50)); manager.Register(new("tool", "Tool", WindowKind.Tool)).ThrowIfFailed();
    var random = new Random(10913);
    for (int i = 0; i < 500; i++)
    {
        string id = "d" + random.Next(50);
        _ = (i % 6) switch { 0 => manager.Float(id), 1 => manager.Dock(id), 2 => manager.Close(id), 3 => manager.Open(id), 4 => manager.Undo(), _ => manager.Redo() };
        if (i % 25 == 0) { manager.AutoHide("tool", DockSide.Left); }
        foreach (string key in manager.Layout.Windows.Keys) Check(DockTree.Locate(manager.Layout, key) == Scan(manager.Layout, key));
    }
});
Test("Hidden-window validation scales without changing cancellation", () => {
    var manager = new DockManager(Large(10000, true)); manager.Open("d0").ThrowIfFailed();
    var before = manager.Layout; manager.Changing += (_, e) => e.Cancel = e.ClosingWindowIds.Contains("d0");
    Check(!manager.Close("d0").Success && ReferenceEquals(before, manager.Layout));
});
Test("Layout rejects nonfinite chrome metrics and viewport overflow", () => {
    var layout = Large(25);
    foreach (var metrics in new[] { new DockMetrics { TabHeight = double.NaN }, new DockMetrics { Border = double.PositiveInfinity }, new DockMetrics { Splitter = double.NaN } })
    { bool rejected = false; try { DockLayoutEngine.Arrange(layout, new(0, 0, 800, 500), metrics); } catch (ArgumentException) { rejected = true; } Check(rejected); }
    try { DockLayoutEngine.Arrange(layout, new(double.MaxValue, 0, double.MaxValue, 500)); throw new Exception("Overflow accepted"); } catch (ArgumentException) { }
});
Test("Zero-area tab viewports realize no headers", () => Check(new TabStripMetrics([100, 200]).VisibleRange(50, 0).Count == 0));
Test("Warm lookup benchmark does not allocate per query", () => {
    var layout = Large(10000); var queries = Enumerable.Range(0, 4000).Select(i => "d" + (i * 7919 % 10000)).ToArray();
    var index = DockLayoutIndex.For(layout); foreach (var id in queries) _ = index.Locate(id);
    (double Ms, long Bytes, long Sum) Measure(Func<string, WindowLocation> locate)
    {
        long before = GC.GetAllocatedBytesForCurrentThread(), sum = 0; var watch = Stopwatch.StartNew();
        foreach (var id in queries) sum += locate(id).Index;
        watch.Stop(); return (watch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - before, sum);
    }
    var scan = Measure(id => Scan(layout, id)); var cached = Measure(id => DockTree.Locate(layout, id));
    Check(scan.Sum == cached.Sum); Check(cached.Bytes < 1024, "Warm lookup allocated " + cached.Bytes);
    Console.WriteLine($"BENCHMARK lookup 10000 windows/4000 queries: scan={scan.Ms:F3}ms {scan.Bytes}B; cached={cached.Ms:F3}ms {cached.Bytes}B; speedup={scan.Ms / Math.Max(.000001, cached.Ms):F2}x");
});
Console.WriteLine($"{passed}/{passed + failed} performance and review specifications passed.");
return failed == 0 ? 0 : 1;

[MethodImpl(MethodImplOptions.NoInlining)]
static (WeakReference, DockLayoutIndex) MakeWeakIndex()
{
    var layout = new DockLayout(); return (new WeakReference(layout), DockLayoutIndex.For(layout));
}
