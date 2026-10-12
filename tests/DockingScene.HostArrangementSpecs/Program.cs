using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockManager Manager()
{
    var m = new DockManager();
    m.RegisterMany(new[] { "a", "b", "c", "d" }.Select(id => new DockWindow(id,id,WindowKind.Document))).ThrowIfFailed();
    return m;
}
var area = new DockRect(0,0,700,500);
Test("Host arrangement changes only supplied members", () => {
    var m = Manager(); var before = m.Layout;
    WindowGeometry.ArrangeMdi(m, new[]{"b","a"}, area, MdiArrangement.TileVertically).ThrowIfFailed();
    Check(m.Layout.Windows["b"].MdiBounds == new DockRect(0,0,350,500));
    Check(m.Layout.Windows["a"].MdiBounds == new DockRect(350,0,350,500));
    Check(ReferenceEquals(m.Layout.Windows["c"], before.Windows["c"]) && ReferenceEquals(m.Layout.Windows["d"], before.Windows["d"]));
});
Test("Independent host order and areas are respected", () => {
    var m = Manager();
    WindowGeometry.ArrangeMdi(m,new[]{"a","b"},area,MdiArrangement.Cascade).ThrowIfFailed();
    var a = m.Layout.Windows["a"]; var b = m.Layout.Windows["b"];
    WindowGeometry.ArrangeMdi(m,new[]{"d","c"},new(20,30,200,300),MdiArrangement.TileHorizontally).ThrowIfFailed();
    Check(ReferenceEquals(a,m.Layout.Windows["a"]) && ReferenceEquals(b,m.Layout.Windows["b"]));
    Check(m.Layout.Windows["d"].MdiBounds == new DockRect(20,30,200,150));
});
Test("Unrelated nonresizable documents cannot block a valid host", () => {
    var m=Manager(); m.UpdateWindow("c",w=>w with {Capabilities=DockCapabilities.Close}).ThrowIfFailed();
    WindowGeometry.ArrangeMdi(m,new[]{"a","b"},area,MdiArrangement.Cascade).ThrowIfFailed();
});
Test("Hidden unknown and duplicate members reject the complete operation", () => {
    var m=Manager();m.Close("c").ThrowIfFailed();var before=m.Layout;
    foreach(var ids in new[]{new[]{"a","c"},new[]{"a","missing"},new[]{"a","a"}})
        Check(!WindowGeometry.ArrangeMdi(m,ids,area,MdiArrangement.Cascade).Success && ReferenceEquals(before,m.Layout));
});
Test("Tool panes cannot masquerade as document host members", () => {
    var m=Manager();m.Register(new("tool","Tool",WindowKind.Tool),targetId:"workspace",side:DockSide.Left).ThrowIfFailed();var before=m.Layout;
    Check(!WindowGeometry.ArrangeMdi(m,new[]{"a","tool"},area,MdiArrangement.Cascade).Success && ReferenceEquals(before,m.Layout));
});
Test("Hosted floating document groups retain outer host geometry", () => {
    var m=Manager();m.Float("d").ThrowIfFailed();var host=m.Layout.FloatingHosts[0];var outside=m.Layout.Windows["a"];
    WindowGeometry.ArrangeMdi(m,new[]{"d"},area,MdiArrangement.TileGrid).ThrowIfFailed();
    Check(ReferenceEquals(host,m.Layout.FloatingHosts[0]) && ReferenceEquals(outside,m.Layout.Windows["a"]));
    Check(m.Layout.Windows["d"].MdiBounds == area);
});
Test("Arranging twice preserves snapshot and history on the second call", () => {
    var m=Manager();WindowGeometry.ArrangeMdi(m,new[]{"a","b"},area,MdiArrangement.Cascade).ThrowIfFailed();
    var before=m.Layout;int changes=0;m.Changed+=(_,_)=>changes++;
    WindowGeometry.ArrangeMdi(m,new[]{"a","b"},area,MdiArrangement.Cascade).ThrowIfFailed();
    Check(ReferenceEquals(before,m.Layout) && changes==0);m.Undo().ThrowIfFailed();Check(m.Layout.MdiMode==MdiMode.Tabbed);
});
Test("Empty hosts leave presentation mode and redo intact", () => {
    var m=Manager();m.Activate("a").ThrowIfFailed();m.Undo().ThrowIfFailed();var before=m.Layout;
    WindowGeometry.ArrangeMdi(m,Array.Empty<string>(),area,MdiArrangement.Cascade).ThrowIfFailed();
    Check(ReferenceEquals(before,m.Layout) && m.CanRedo && m.Layout.MdiMode==MdiMode.Tabbed);
});
Test("Lock capability and veto all reject atomically", () => {
    var m=Manager();m.SetLayoutLocked(true).ThrowIfFailed();var before=m.Layout;
    Check(!WindowGeometry.ArrangeMdi(m,new[]{"a"},area,MdiArrangement.Cascade).Success && ReferenceEquals(before,m.Layout));
    m.SetLayoutLocked(false).ThrowIfFailed();m.UpdateWindow("a",w=>w with {Capabilities=DockCapabilities.Drag}).ThrowIfFailed();before=m.Layout;
    Check(!WindowGeometry.ArrangeMdi(m,new[]{"a","b"},area,MdiArrangement.Cascade).Success && ReferenceEquals(before,m.Layout));
    m.Changing+=(_,e)=>e.Cancel=true;
    Check(!WindowGeometry.ArrangeMdi(m,new[]{"b"},area,MdiArrangement.Cascade).Success && ReferenceEquals(before,m.Layout));
});
Test("Invalid geometry and stale caller revisions do not change layout", () => {
    var m=Manager();var before=m.Layout;
    Check(!WindowGeometry.ArrangeMdi(m,new[]{"a"},area,(MdiArrangement)99).Success);
    Check(!WindowGeometry.ArrangeMdi(m,new[]{"a"},new(0,0,double.NaN,400),MdiArrangement.Cascade).Success);
    Check(!WindowGeometry.ArrangeMdi(m,new[]{"a"},area,MdiArrangement.Cascade,expectedRevision:before.Revision-1).Success);
    Check(ReferenceEquals(before,m.Layout));
});
Test("Caller indexer reentrancy cannot overwrite newer metadata", () => {
    var m=Manager();
    var ids = new ReentrantIds(()=>m.UpdateWindow("b",w=>w with {Title="newer"}).ThrowIfFailed());
    Check(!WindowGeometry.ArrangeMdi(m,ids,area,MdiArrangement.Cascade).Success);
    Check(m.Layout.Windows["b"].Title=="newer" && m.Layout.MdiMode==MdiMode.Tabbed);
});
Test("One thousand documents arrange in one commit and undo together", () => {
    var m=new DockManager();var ids=Enumerable.Range(0,1000).Select(i=>"doc"+i).ToArray();
    m.RegisterMany(ids.Select(id=>new DockWindow(id,id,WindowKind.Document))).ThrowIfFailed();
    var before=m.Layout;int changes=0;m.Changed+=(_,_)=>changes++;
    WindowGeometry.ArrangeMdi(m,ids,new(0,0,4000,3000),MdiArrangement.TileGrid).ThrowIfFailed();
    Check(changes==1 && m.Layout.Revision==before.Revision+1);
    m.Undo().ThrowIfFailed();Check(m.Layout.Windows.SequenceEqual(before.Windows));
});
Console.WriteLine($"{passed}/{passed+failed} host arrangement specifications passed.");
return failed==0?0:1;

sealed class ReentrantIds(Action onRead) : IReadOnlyList<string>
{
    public int Count=>1;
    public string this[int index] { get { onRead(); return "a"; } }
    public IEnumerator<string> GetEnumerator() { yield return this[0]; }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
}
