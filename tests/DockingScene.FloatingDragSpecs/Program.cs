using DockingScene.Core;
using DockingScene.Layout;

int passed=0, failed=0;
void Check(bool value) { if(!value) throw new Exception("Assertion failed"); }
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS "+name); } catch(Exception e) { failed++; Console.Error.WriteLine("FAIL "+name+": "+e); } }
void Reject(Action action) { try { action(); } catch(ArgumentException) { return; } throw new Exception("Invalid drag accepted"); }
var fallback = new DockRect(80,90,240,180);
DockManager Create()
{
    var m=new DockManager();
    m.RegisterMany(new[]{"a","b","c","d"}.Select(id=>new DockWindow(id,id,WindowKind.Document))).ThrowIfFailed();
    m.FloatMany(new[]{"a","b"},new(20,40,260,220)).ThrowIfFailed();
    m.Dock("b","a",DockSide.Bottom).ThrowIfFailed();
    m.Float("c",new(330,40,260,220)).ThrowIfFailed();
    return m;
}
Test("Whole split-host drag keeps original bounds and preserves every node",()=>{
    var m=Create();var host=m.Layout.FloatingHosts.First(h=>DockTree.WindowIds(h.Root).Contains("a"));
    var plan=new FloatingDragPlan(m.Layout,new[]{"b","a"},fallback);
    Check(plan.SourceHostId==host.Id && plan.InitialBounds==host.Bounds);
    plan.Commit(m,host.Bounds with {X=69}).ThrowIfFailed();
    var after=m.Layout.FloatingHosts.First(h=>h.Id==host.Id);
    Check(ReferenceEquals(after.Root,host.Root) && after.Bounds==host.Bounds with {X=69});
    Check(m.Layout.Windows["a"].FloatingBounds==after.Bounds && m.Layout.Windows["b"].FloatingBounds==after.Bounds);
});
Test("Whole-host movement requires Drag but not Float or Resize",()=>{
    var m=Create();foreach(string id in new[]{"a","b"}) m.UpdateWindow(id,w=>w with {Capabilities=DockCapabilities.Drag|DockCapabilities.Close}).ThrowIfFailed();
    var plan=new FloatingDragPlan(m.Layout,new[]{"a","b"},fallback);
    plan.Commit(m,plan.InitialBounds with {X=55}).ThrowIfFailed();
    Check(m.Layout.FloatingHosts.Count(h=>DockTree.WindowIds(h.Root).Contains("a"))==1);
});
Test("Source hosts cannot snap to themselves, including partial tab detach",()=>{
    var m=Create();var plan=new FloatingDragPlan(m.Layout,new[]{"a"},fallback);
    Check(plan.SourceHostId is null && plan.InitialBounds==fallback && plan.Magnetism.NeighborCount==1);
    var raw=new DockRect(22,42,100,100);Check(plan.Move(raw)==raw);
});
Test("Neighbor gap and bypass use the captured full host extent",()=>{
    var m=Create();var plan=new FloatingDragPlan(m.Layout,new[]{"a","b"},fallback);
    var raw=plan.InitialBounds with {X=66};
    Check(plan.Move(raw).X==69 && plan.Move(raw,true)==raw);
    Check(new FloatingDragPlan(m.Layout,new[]{"a","b"},fallback,0).Move(raw)==raw);
    Check(new FloatingDragPlan(m.Layout,new[]{"a","b"},fallback,5,8).Move(raw with {X=65}).X==62);
});
Test("Partial detach creates a host without moving the remaining source",()=>{
    var m=Create();var original=m.Layout.FloatingHosts.First(h=>DockTree.WindowIds(h.Root).Contains("a"));
    new FloatingDragPlan(m.Layout,new[]{"a"},fallback).Commit(m,fallback).ThrowIfFailed();
    var remaining=m.Layout.FloatingHosts.First(h=>h.Id==original.Id);
    Check(DockTree.WindowIds(remaining.Root).SequenceEqual(new[]{"b"}) && remaining.Bounds==original.Bounds);
    Check(DockTree.Locate(m.Layout,"a").HostId!=original.Id);
});
Test("A restricted partial detach cannot bypass Float capability",()=>{
    var m=Create();m.UpdateWindow("a",w=>w with {Capabilities=DockCapabilities.Drag}).ThrowIfFailed();var before=m.Layout;
    Check(!new FloatingDragPlan(before,new[]{"a"},fallback).Commit(m,fallback).Success && ReferenceEquals(before,m.Layout));
});
Test("Maximized hosts are excluded as source-preserving or magnetic neighbors",()=>{
    var m=Create();var c=DockTree.Locate(m.Layout,"c").HostId!;m.SetFloatingMaximized(c,true).ThrowIfFailed();
    Check(new FloatingDragPlan(m.Layout,new[]{"a","b"},fallback).Magnetism.NeighborCount==0);
    Check(new FloatingDragPlan(m.Layout,new[]{"c"},fallback).SourceHostId is null);
});
Test("Snapshot revisions reject changes without overwriting newer metadata",()=>{
    var m=Create();var plan=new FloatingDragPlan(m.Layout,new[]{"a","b"},fallback);
    m.UpdateWindow("d",w=>w with {Title="Newer"}).ThrowIfFailed();var before=m.Layout;
    Check(!plan.Commit(m,plan.InitialBounds with {X=80}).Success && ReferenceEquals(before,m.Layout));
});
Test("Lock and veto preserve all host data atomically",()=>{
    var m=Create();m.SetLayoutLocked(true).ThrowIfFailed();var before=m.Layout;
    var plan=new FloatingDragPlan(before,new[]{"a","b"},fallback);
    Check(!plan.Commit(m,plan.InitialBounds with {X=60}).Success && ReferenceEquals(before,m.Layout));
    m.SetLayoutLocked(false).ThrowIfFailed();before=m.Layout;plan=new(before,new[]{"a","b"},fallback);m.Changing+=(_,e)=>e.Cancel=true;
    Check(!plan.Commit(m,plan.InitialBounds with {X=60}).Success && ReferenceEquals(before,m.Layout));
});
Test("A whole-host no-op preserves snapshot and undo restores a real movement",()=>{
    var m=Create();var before=m.Layout;var plan=new FloatingDragPlan(before,new[]{"a","b"},fallback);
    plan.Commit(m,plan.InitialBounds).ThrowIfFailed();Check(ReferenceEquals(before,m.Layout));
    plan.Commit(m,plan.InitialBounds with {X=60}).ThrowIfFailed();m.Undo().ThrowIfFailed();
    Check(m.Layout.FloatingHosts.SequenceEqual(before.FloatingHosts) && m.Layout.Windows.SequenceEqual(before.Windows));
});
Test("Malformed members and invalid bounds reject",()=>{
    var m=Create();m.Close("d").ThrowIfFailed();
    foreach(var ids in new[]{Array.Empty<string>(),new[]{"a","a"},new[]{"missing"},new[]{"d"}}) Reject(()=>new FloatingDragPlan(m.Layout,ids,fallback));
    Reject(()=>new FloatingDragPlan(m.Layout,new[]{"a"},new(double.MaxValue,0,double.MaxValue,10)));
    var plan=new FloatingDragPlan(m.Layout,new[]{"a","b"},fallback);var before=m.Layout;
    Check(!plan.Commit(m,plan.InitialBounds with {Width=300}).Success && ReferenceEquals(before,m.Layout));
    Check(!plan.Commit(m,plan.InitialBounds with {X=double.NaN}).Success && ReferenceEquals(before,m.Layout));
});
Test("Mutable caller arrays and multi-host scopes cannot corrupt the frozen plan",()=>{
    var m=Create();var ids=new[]{"a","b","c"};var plan=new FloatingDragPlan(m.Layout,ids,fallback);ids[0]="d";
    Check(plan.WindowIds[0]=="a" && plan.SourceHostId is null && plan.Magnetism.NeighborCount==0);
});
Console.WriteLine($"{passed}/{passed+failed} floating drag specifications passed.");return failed==0?0:1;
