using DockingScene.Core;

int passed = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
DockManager New()
{
    var m = new DockManager();
    m.Register(new("a", "A.cs", WindowKind.Document)).ThrowIfFailed();
    m.Register(new("b", "B.cs", WindowKind.Document)).ThrowIfFailed();
    m.Register(new("tool", "Explorer", WindowKind.Tool)).ThrowIfFailed();
    return m;
}
var m = New();
Check(m.Layout.Windows.Count == 3, "register");
Check(DockTree.Locate(m.Layout, "tool").Placement == WindowPlacement.Docked, "tool location");
Check(DockTree.Locate(m.Layout, "a").Placement == WindowPlacement.Document, "document location");
Check(!m.Register(new("a", "duplicate", WindowKind.Document)).Success, "duplicate rejected");
var before = m.Layout;
Check(!m.Dock("a", "tool").Success && ReferenceEquals(before, m.Layout), "invalid docking atomic");
m.Dock("b", "a", DockSide.Right).ThrowIfFailed();
Check(DockTree.GroupOf(m.Layout, "a")!.Id != DockTree.GroupOf(m.Layout, "b")!.Id, "split documents");
m.Dock("b", "a").ThrowIfFailed();
Check(DockTree.GroupOf(m.Layout, "a")!.Windows.Length == 2, "attach documents");
m.Float("tool").ThrowIfFailed();
Check(m.Layout.FloatingHosts.Length == 1, "float");
m.AutoHide("tool", DockSide.Right).ThrowIfFailed();
Check(m.Layout.FloatingHosts.IsEmpty && m.Layout.AutoHideGroups.Length == 1, "auto-hide from float");
m.PinAutoHide("tool").ThrowIfFailed();
Check(m.Layout.AutoHideGroups.IsEmpty && DockTree.Locate(m.Layout, "tool").Placement == WindowPlacement.Docked, "pin");
m.UpdateWindow("a", w => w with { DisplayMode = TabDisplayMode.Pinned }).ThrowIfFailed();
m.Reorder("a", 99).ThrowIfFailed();
Check(DockTree.GroupOf(m.Layout, "a")!.Windows[0] == "a", "pinned ordering");
m.UpdateWindow("b", w => w with { DisplayMode = TabDisplayMode.Preview }).ThrowIfFailed();
m.UpdateWindow("b", w => w with { IsModified = true }).ThrowIfFailed();
Check(m.Layout.Windows["b"].DisplayMode == TabDisplayMode.Normal, "modified preview promotes");
EventHandler<DockChangingEventArgs> cancel = (_, e) => { if (!e.ClosingWindowIds.IsEmpty) e.Cancel = true; };
m.Changing += cancel;
before = m.Layout;
Check(!m.Close("a").Success && ReferenceEquals(before, m.Layout), "close cancellation");
m.Changing -= cancel;
m.Close("a").ThrowIfFailed();
Check(m.Layout.HiddenWindows.Contains("a"), "close hides registered window");
m.Open("a").ThrowIfFailed();
Check(!m.Layout.HiddenWindows.Contains("a") && m.Layout.ActiveWindowId == "a", "reopen");
long revision = m.Layout.Revision;
Check(!m.Dock("a", "b", expectedRevision: revision - 1).Success && m.Layout.Revision == revision, "stale revision");
m.SetLayoutLocked(true).ThrowIfFailed();
Check(!m.Float("a").Success, "layout lock");
m.SetLayoutLocked(false).ThrowIfFailed();
m.Float("a").ThrowIfFailed();
m.Undo().ThrowIfFailed();
Check(DockTree.Locate(m.Layout, "a").Placement == WindowPlacement.Document, "undo");
m.Redo().ThrowIfFailed();
Check(DockTree.Locate(m.Layout, "a").Placement == WindowPlacement.Floating, "redo");
m.CloseMany(["a", "b", "tool"]).ThrowIfFailed();
Check(m.Layout.ActiveWindowId is null && m.Layout.FloatingHosts.IsEmpty, "close all normalizes");
foreach (var id in new[] { "a", "b", "tool" }) m.Open(id).ThrowIfFailed();
Check(m.Layout.Windows.Count == 3, "reopen all");
m.UpdateWindow("tool", w => w with { Capabilities = DockCapabilities.All & ~DockCapabilities.Float }).ThrowIfFailed();
Check(!m.FloatMany(["a", "tool"]).Success, "group capability atomic");
// Seeded state-machine fuzz: every successful transition must preserve all structural invariants.
var random = new Random(19384);
for (int i = 0; i < 2500; i++)
{
    var id = new[] { "a", "b", "tool" }[random.Next(3)];
    switch (random.Next(9))
    {
        case 0: m.Open(id); break;
        case 1: m.Close(id); break;
        case 2: m.Float(id); break;
        case 3: m.Dock(id, null, (DockSide)random.Next(5)); break;
        case 4: m.Activate(id); break;
        case 5: m.Undo(); break;
        case 6: m.Redo(); break;
        case 7: m.AutoHide(id, DockSide.Left); break;
        case 8: m.PinAutoHide(id); break;
    }
    DockLayoutValidator.Validate(m.Layout);
    Check(m.Layout.Windows.Count == 3, "fuzz registry invariant");
}
Console.WriteLine($"PASS: {passed} assertions, including 2500 seeded state-machine transitions.");
