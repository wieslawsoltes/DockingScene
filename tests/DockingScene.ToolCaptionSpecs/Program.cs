using DockingScene.Core;

int passed=0,failed=0;
void Check(bool value) { if(!value) throw new Exception("Assertion failed"); }
void Test(string name,Action action) { try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.Error.WriteLine("FAIL "+name+": "+e);} }
DockManager Create()
{
    var m=new DockManager();
    m.Register(new("a","Explorer",WindowKind.Tool)).ThrowIfFailed();
    m.Register(new("b","Properties",WindowKind.Tool)).ThrowIfFailed();
    m.Register(new("doc","Document.cs",WindowKind.Document)).ThrowIfFailed();
    m.FloatMany(new[]{"a","b"},new(20,30,400,300)).ThrowIfFailed();
    m.Dock("b","a",DockSide.Bottom).ThrowIfFailed();return m;
}
string Group(DockManager m,string id)=>DockTree.GroupOf(m.Layout,id)!.Id;
string Host(DockManager m,string id)=>DockTree.Locate(m.Layout,id).HostId!;
Test("Default double-click maximizes and restores the complete split host",()=>{
    var m=Create();var host=m.Layout.FloatingHosts[0];var before=m.Layout;
    m.InvokeToolCaptionDoubleClick(Group(m,"a")).ThrowIfFailed();
    Check(m.Layout.FloatingHosts[0].IsMaximized && ReferenceEquals(m.Layout.FloatingHosts[0].Root,host.Root));
    Check(m.Layout.FloatingHosts[0].Bounds==host.Bounds && m.Layout.Windows["a"].FloatingBounds==before.Windows["a"].FloatingBounds);
    m.InvokeToolCaptionDoubleClick(Group(m,"b")).ThrowIfFailed();
    Check(!m.Layout.FloatingHosts[0].IsMaximized && m.Layout.FloatingHosts[0].Bounds==host.Bounds);
});
Test("Dock mode moves only the clicked group, leaving the sibling host intact",()=>{
    var m=Create();var host=m.Layout.FloatingHosts[0];
    m.InvokeToolCaptionDoubleClick(Group(m,"a"),FloatingWindowTitleBarDoubleClickMode.Dock).ThrowIfFailed();
    Check(DockTree.Locate(m.Layout,"a").Placement!=WindowPlacement.Floating);
    Check(Host(m,"b")==host.Id && m.Layout.FloatingHosts[0].Bounds==host.Bounds);
});
Test("Main-site activation cannot redirect the title command",()=>{
    var m=Create();m.Activate("doc").ThrowIfFailed();
    m.InvokeToolCaptionDoubleClick(Group(m,"b")).ThrowIfFailed();
    Check(m.Layout.FloatingHosts[0].IsMaximized && m.Layout.ActiveWindowId=="doc");
});
Test("Resize permission covers every member of the enclosing floating host",()=>{
    var m=Create();m.UpdateWindow("b",w=>w with {Capabilities=w.Capabilities & ~DockCapabilities.Resize}).ThrowIfFailed();var before=m.Layout;
    Check(!m.InvokeToolCaptionDoubleClick(Group(m,"a")).Success && ReferenceEquals(before,m.Layout));
});
Test("Dock mode does not incorrectly require the sibling resize capability",()=>{
    var m=Create();m.UpdateWindow("b",w=>w with {Capabilities=DockCapabilities.None}).ThrowIfFailed();
    m.InvokeToolCaptionDoubleClick(Group(m,"a"),FloatingWindowTitleBarDoubleClickMode.Dock).ThrowIfFailed();
    Check(DockTree.Locate(m.Layout,"b").Placement==WindowPlacement.Floating);
});
Test("Docked and auto-hidden tool groups float through the same gesture",()=>{
    var m=new DockManager();m.Register(new("a","A",WindowKind.Tool)).ThrowIfFailed();
    m.InvokeToolCaptionDoubleClick(Group(m,"a")).ThrowIfFailed();Check(m.Layout.FloatingHosts.Length==1);
    m.AutoHide("a",DockSide.Left).ThrowIfFailed();
    m.InvokeToolCaptionDoubleClick(m.Layout.AutoHideGroups[0].Id).ThrowIfFailed();Check(m.Layout.AutoHideGroups.IsEmpty && m.Layout.FloatingHosts.Length==1);
});
Test("Float and Dock capabilities remain enforced",()=>{
    var m=Create();m.UpdateWindow("a",w=>w with {Capabilities=DockCapabilities.Resize}).ThrowIfFailed();var before=m.Layout;
    Check(!m.InvokeToolCaptionDoubleClick(Group(m,"a"),FloatingWindowTitleBarDoubleClickMode.Dock).Success && ReferenceEquals(before,m.Layout));
    m.InvokeToolCaptionDoubleClick(Group(m,"a")).ThrowIfFailed();Check(m.Layout.FloatingHosts[0].IsMaximized);
});
Test("Lock, stale revisions, vetoes and invalid container modes reject atomically",()=>{
    var m=Create();var g=Group(m,"a");var before=m.Layout;
    Check(!m.InvokeToolCaptionDoubleClick(g,expectedRevision:before.Revision-1).Success);
    Check(!m.InvokeToolCaptionDoubleClick(g,(FloatingWindowTitleBarDoubleClickMode)99).Success);
    Check(!m.InvokeToolCaptionDoubleClick("missing").Success);
    Check(!m.InvokeToolCaptionDoubleClick(Group(m,"doc")).Success && ReferenceEquals(before,m.Layout));
    m.SetLayoutLocked(true).ThrowIfFailed();before=m.Layout;
    Check(!m.InvokeToolCaptionDoubleClick(g).Success && ReferenceEquals(before,m.Layout));
    m.SetLayoutLocked(false).ThrowIfFailed();before=m.Layout;m.Changing+=(_,e)=>e.Cancel=true;
    Check(!m.InvokeToolCaptionDoubleClick(g).Success && ReferenceEquals(before,m.Layout));
});
Test("One title action generates one notification and one undo step",()=>{
    var m=Create();var before=m.Layout;int notifications=0;m.Changed+=(_,_)=>notifications++;
    m.InvokeToolCaptionDoubleClick(Group(m,"a")).ThrowIfFailed();Check(notifications==1 && m.Layout.Revision==before.Revision+1);
    m.Undo().ThrowIfFailed();Check(m.Layout.FloatingHosts.SequenceEqual(before.FloatingHosts));
});
Test("Tool-only captions ignore the application title and follow host MRU",()=>{
    var m=Create();string host=Host(m,"a");m.Activate("a").ThrowIfFailed();m.Activate("doc").ThrowIfFailed();
    Check(FloatingHostCaption.GetTitle(m.Layout,host,"App")=="Explorer");
    m.Activate("b").ThrowIfFailed();Check(FloatingHostCaption.GetTitle(m.Layout,host,"App")=="Properties");
});
Test("Document captions retain the primary document while a tool is active",()=>{
    var m=Create();m.Float("doc").ThrowIfFailed();string host=Host(m,"doc");m.Dock("a","doc",DockSide.Left).ThrowIfFailed();
    m.Activate("a").ThrowIfFailed();Check(FloatingHostCaption.GetTitle(m.Layout,host,"App")=="Document.cs - App");
    Check(FloatingHostCaption.GetTitle(m.Layout,host,"App"," | ")=="Document.cs | App");
    Check(FloatingHostCaption.GetTitle(m.Layout,host,"App",null)=="Document.csApp");
});
Test("Caption selection reads changed metadata without modifying its cached tree",()=>{
    var m=Create();m.Float("doc").ThrowIfFailed();string host=Host(m,"doc");var root=m.Layout.FloatingHosts.First(h=>h.Id==host).Root;
    Check(FloatingHostCaption.GetTitle(m.Layout,host)=="Document.cs");
    m.UpdateWindow("doc",w=>w with {Title="Renamed.cs"}).ThrowIfFailed();
    Check(FloatingHostCaption.GetTitle(m.Layout,host,"App")=="Renamed.cs - App");
    Check(ReferenceEquals(root,m.Layout.FloatingHosts.First(h=>h.Id==host).Root));
});
Test("Equal-revision metadata and activation snapshots cannot alias caption results",()=>{
    var m=Create();m.Activate("a").ThrowIfFailed();var before=m.Layout;string host=Host(m,"a");
    Check(FloatingHostCaption.GetPrimaryWindow(before,host).Id=="a");
    var changed=before with {Windows=before.Windows.SetItem("a",before.Windows["a"] with {Title="Changed title"})};
    Check(FloatingHostCaption.GetTitle(changed,host)=="Changed title" && FloatingHostCaption.GetTitle(before,host)=="Explorer");
    var switched=before with {ActiveWindowId="b",ActivationHistory=["b","a","doc"]};
    Check(FloatingHostCaption.GetPrimaryWindow(switched,host).Id=="b" && FloatingHostCaption.GetPrimaryWindow(before,host).Id=="a");
});
Test("Concurrent readers see complete independent host projections",()=>{
    var m=Create();m.Float("doc").ThrowIfFailed();m.Activate("a").ThrowIfFailed();
    var layout=m.Layout;string tools=Host(m,"a"),document=Host(m,"doc");
    Parallel.For(0,128,i=>{
        Check(FloatingHostCaption.GetPrimaryWindow(layout,tools).Id=="a");
        Check(FloatingHostCaption.GetPrimaryWindow(layout,document).Id=="doc");
    });
});
Test("An unknown host cannot reuse the previously queried host's caption",()=>{
    var m=Create();string host=Host(m,"a");var primary=FloatingHostCaption.GetPrimaryWindow(m.Layout,host);
    try{FloatingHostCaption.GetPrimaryWindow(m.Layout,"missing");throw new Exception("Unknown host was accepted");}
    catch(ArgumentException){}
    Check(ReferenceEquals(primary,FloatingHostCaption.GetPrimaryWindow(m.Layout,host)));
});
Test("Warm primary-window resolution allocates no managed memory",()=>{
    var m=Create();string host=Host(m,"a");m.Activate("a").ThrowIfFailed();
    AllocationProbe.VerifyControls();
    Func<DockWindow> query=()=>FloatingHostCaption.GetPrimaryWindow(m.Layout,host);
    _=AllocationProbe.Measure(query,1000,out _);
    long bytes=AllocationProbe.Measure(query,10000,out var result);
    if(bytes!=0 || result?.Id!="a") throw new Exception($"Expected 0 bytes and a; actual {bytes} bytes and {result?.Id}");
    Console.WriteLine($"ALLOCATION primary captions: 10000 warm resolutions, {bytes} bytes; membership/index construction excluded");
});
Console.WriteLine($"{passed}/{passed+failed} tool caption specifications passed.");return failed==0?0:1;
