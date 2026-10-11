using DockingScene.Core;

int passed = 0, failed = 0;
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockManager New(DockPolicy? policy = null)
{
    var manager = new DockManager(policy: policy);
    manager.Register(new("a", "A", WindowKind.Document)).ThrowIfFailed();
    manager.Register(new("b", "B", WindowKind.Document)).ThrowIfFailed();
    manager.Register(new("tool", "Tool", WindowKind.Tool)).ThrowIfFailed();
    return manager;
}
Test("Batch commits one revision and one notification", () => {
    var manager = New(); var before = manager.Layout; int events = 0;
    manager.Changed += (_, _) => events++;
    manager.Transaction("Batch", [m => m.Float("a"), m => m.Close("b")]).ThrowIfFailed();
    Check(manager.Layout.Revision == before.Revision + 1); Check(events == 1);
    manager.Undo().ThrowIfFailed();
    Check(DockTree.Locate(manager.Layout, "a").Placement == WindowPlacement.Document);
    Check(!manager.Layout.HiddenWindows.Contains("b"));
});
Test("Rejected intermediate operation rolls back entire batch", () => {
    var manager = New(); var before = manager.Layout;
    Check(!manager.Transaction("Batch", [m => m.Float("a"), m => m.Dock("b", "tool")]).Success);
    Check(ReferenceEquals(before, manager.Layout));
});
Test("Stale batch cannot overwrite another revision", () => {
    var manager = New(); var before = manager.Layout;
    Check(!manager.Transaction("Batch", [m => m.Close("a")], before.Revision - 1).Success);
    Check(ReferenceEquals(before, manager.Layout));
});
Test("Batch close veto leaves all windows unchanged", () => {
    var manager = New(); var before = manager.Layout;
    manager.Changing += (_, e) => { if (e.ClosingWindowIds.Contains("b")) e.Cancel = true; };
    Check(!manager.Transaction("Batch", [m => m.Float("a"), m => m.Close("b")]).Success);
    Check(ReferenceEquals(before, manager.Layout));
});
Test("History journal is visible before Changed is raised", () => {
    var manager = New(); manager.Float("a").ThrowIfFailed(); bool observed = false;
    manager.Changed += (_, e) => { if (e.Command == "Undo") { Check(manager.CanRedo); observed = true; } };
    manager.Undo().ThrowIfFailed(); Check(observed);
});
Test("Nested observer commands deliver revisions in order", () => {
    var manager = New(); var revisions = new List<long>();
    manager.Changed += (_, e) => { if (e.Command == "Float") manager.Close("b").ThrowIfFailed(); };
    manager.Changed += (_, e) => revisions.Add(e.After.Revision);
    manager.Float("a").ThrowIfFailed();
    Check(revisions.Count == 2); Check(revisions[1] == revisions[0] + 1);
});
Test("Observer failure cannot change successful commit result", () => {
    var manager = New(); int faults = 0, notified = 0;
    manager.ObserverFaulted += _ => faults++;
    manager.Changed += (_, _) => throw new Exception("Consumer error");
    manager.Changed += (_, _) => notified++;
    Check(manager.Float("a").Success); Check(faults == 1 && notified == 1);
    Check(DockTree.Locate(manager.Layout, "a").Placement == WindowPlacement.Floating);
});
Test("Redo cleared by a command dispatched from Undo observer", () => {
    var manager = New(); manager.Float("a").ThrowIfFailed();
    manager.Changed += (_, e) => { if (e.Command == "Undo") manager.Close("b").ThrowIfFailed(); };
    manager.Undo().ThrowIfFailed(); Check(!manager.CanRedo); Check(!manager.Redo().Success);
});
Test("Restore close participates in cancellation", () => {
    var manager = New(); var original = manager.Layout;
    var trial = new DockManager(original); trial.Close("a").ThrowIfFailed();
    manager.Changing += (_, e) => { if (e.ClosingWindowIds.Contains("a")) e.Cancel = true; };
    Check(!manager.Restore(trial.Layout).Success); Check(ReferenceEquals(original, manager.Layout));
});
Test("Undo registration can be cancelled before content destruction", () => {
    var manager = New(); manager.Register(new("new", "New", WindowKind.Document)).ThrowIfFailed();
    manager.Changing += (_, e) => { if (e.ClosingWindowIds.Contains("new")) e.Cancel = true; };
    Check(!manager.Undo().Success); Check(manager.Layout.Windows.ContainsKey("new"));
});
Test("Invalid cyclic Restore rejects before activation repair", () => {
    var manager = New(); var original = manager.Layout; var node = new DockWorkspace("cycle");
    typeof(DockWorkspace).GetProperty(nameof(DockWorkspace.Content))!.SetValue(node, node);
    Check(!manager.Restore(new() { Root = node }).Success); Check(ReferenceEquals(original, manager.Layout));
});
Test("Preview replacement hides previous preview atomically", () => {
    var manager = New(); manager.OpenPreview(new("p1", "Preview 1", WindowKind.Document)).ThrowIfFailed();
    manager.OpenPreview(new("p2", "Preview 2", WindowKind.Document)).ThrowIfFailed();
    Check(manager.Layout.HiddenWindows.Contains("p1")); Check(manager.Layout.ActiveWindowId == "p2");
    Check(manager.Layout.Windows["p2"].DisplayMode == TabDisplayMode.Preview);
});
Test("Preview replacement veto also cancels new registration", () => {
    var manager = New(); manager.OpenPreview(new("p1", "Preview 1", WindowKind.Document)).ThrowIfFailed();
    var before = manager.Layout;
    manager.Changing += (_, e) => { if (e.ClosingWindowIds.Contains("p1")) e.Cancel = true; };
    Check(!manager.OpenPreview(new("p2", "Preview 2", WindowKind.Document)).Success);
    Check(ReferenceEquals(before, manager.Layout)); Check(!manager.Layout.Windows.ContainsKey("p2"));
});
Test("Modified preview is retained as a normal document", () => {
    var manager = New(); manager.OpenPreview(new("p", "Preview", WindowKind.Document) { IsModified = true }).ThrowIfFailed();
    Check(manager.Layout.Windows["p"].DisplayMode == TabDisplayMode.Normal);
});
Test("Keep open promotes preview", () => {
    var manager = New(); manager.OpenPreview(new("p", "Preview", WindowKind.Document)).ThrowIfFailed(); manager.KeepOpen("p").ThrowIfFailed();
    Check(manager.Layout.Windows["p"].DisplayMode == TabDisplayMode.Normal);
});
Test("Close other tabs preserves pinned documents by default", () => {
    var manager = New(); manager.SetPinned("a", true).ThrowIfFailed();
    manager.Register(new("c", "C", WindowKind.Document)).ThrowIfFailed(); manager.CloseOtherTabs("b").ThrowIfFailed();
    Check(!manager.Layout.HiddenWindows.Contains("a")); Check(manager.Layout.HiddenWindows.Contains("c"));
});
Test("Directional close only closes tabs on requested side", () => {
    var manager = New(); manager.Register(new("c", "C", WindowKind.Document)).ThrowIfFailed();
    manager.CloseTabsToRight("b").ThrowIfFailed();
    Check(manager.Layout.HiddenWindows.Contains("c")); Check(!manager.Layout.HiddenWindows.Contains("a"));
});
Test("Filtered activation indexes pinned tabs independently", () => {
    var manager = New(); manager.SetPinned("a", true).ThrowIfFailed();
    var group = DockTree.GroupOf(manager.Layout, "a")!;
    manager.ActivateTab(group.Id, 0, TabDisplayMode.Pinned).ThrowIfFailed(); Check(manager.Layout.ActiveWindowId == "a");
    manager.ActivateTab(group.Id, 0).ThrowIfFailed(); Check(manager.Layout.ActiveWindowId == "b");
});
Test("MDI resize observes per-window capability", () => {
    var manager = New(); manager.UpdateWindow("a", window => window with { Capabilities = DockCapabilities.All & ~DockCapabilities.Resize }).ThrowIfFailed();
    Check(!manager.SetMdiBounds("a", new(0, 0, 300, 200)).Success); Check(!manager.SetMdiState("a", MdiWindowState.Maximized).Success);
});
Test("Floating maximize observes layout lock", () => {
    var manager = New(); manager.Float("a").ThrowIfFailed(); manager.SetLayoutLocked(true).ThrowIfFailed();
    Check(!manager.SetFloatingMaximized(manager.Layout.FloatingHosts[0].Id, true).Success);
});
Test("Auto-hide resize rejects invalid input without partial metadata", () => {
    var manager = New(); var before = manager.Layout; Check(!manager.SetAutoHideSize("tool", double.NaN).Success);
    Check(ReferenceEquals(before, manager.Layout)); manager.SetAutoHideSize("tool", 360).ThrowIfFailed();
    Check(manager.Layout.Windows["tool"].AutoHideSize == 360);
});
Test("Mixed floating group respects workspace policy", () => {
    var manager = New(new() { AllowMixedDocumentGroups = false }); var before = manager.Layout;
    Check(!manager.FloatMany(["a", "tool"]).Success); Check(ReferenceEquals(before, manager.Layout));
});
Test("Reentrant Changing remains rejected", () => {
    var manager = New(); DockResult nested = default;
    manager.Changing += (_, _) => nested = manager.Close("b");
    manager.Float("a").ThrowIfFailed(); Check(!nested.Success); Check(!manager.Layout.HiddenWindows.Contains("b"));
});
Console.WriteLine($"{passed}/{passed + failed} transaction and document specifications passed.");
return failed == 0 ? 0 : 1;
