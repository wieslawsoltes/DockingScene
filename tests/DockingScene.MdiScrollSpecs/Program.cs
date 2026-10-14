using DockingScene.Core;
using DockingScene.Layout;

int passed=0, failed=0;
void Check(bool value) { if(!value) throw new Exception("Assertion failed"); }
void Reject(Action action) { try { action(); } catch(ArgumentException) { return; } throw new Exception("Invalid geometry accepted"); }
void Test(string name,Action action) { try { action(); passed++; Console.WriteLine("PASS "+name); } catch(Exception e) {failed++;Console.Error.WriteLine("FAIL "+name+": "+e);} }
Test("Empty and fully contained workspaces need no scrollbars",()=>{
    var a=MdiScrollLayout.Measure([],new(600,400));
    Check(a.ContentBounds==new DockRect(0,0,600,400) && a.Viewport==a.ContentBounds);
    Check(!a.HasHorizontalScrollBar && !a.HasVerticalScrollBar);
    Check(MdiScrollLayout.Measure([new(20,30,200,100)],new(600,400))==a);
});
Test("Horizontal-only and vertical-only extents reserve native scrollbar space",()=>{
    var h=MdiScrollLayout.Measure([new(500,20,200,100)],new(600,400));
    Check(h.HasHorizontalScrollBar && !h.HasVerticalScrollBar && h.Viewport==new DockRect(0,0,600,384));
    var v=MdiScrollLayout.Measure([new(20,350,200,100)],new(600,400));
    Check(v.HasVerticalScrollBar && !v.HasHorizontalScrollBar && v.Viewport.Width==584);
});
Test("Either scrollbar can induce the other and the fixed point is stable",()=>{
    var a=MdiScrollLayout.Measure([new(0,0,595,410)],new(600,400));
    Check(a.HasHorizontalScrollBar && a.HasVerticalScrollBar && a.Viewport.Size()==new DockSize(584,384));
    var b=MdiScrollLayout.Measure([new(0,0,610,395)],new(600,400));
    Check(b.HasHorizontalScrollBar && b.HasVerticalScrollBar);
});
Test("Negative windows preserve world origin while exposing both leading ranges",()=>{
    var a=MdiScrollLayout.Measure([new(-300,-200,200,100)],new(600,400));
    Check(a.ContentBounds.X==-300 && a.ContentBounds.Y==-200 && a.Viewport.X==0 && a.Viewport.Y==0);
    Check(a.HorizontalOffset==300 && a.VerticalOffset==200);
    a=a.WithOffsets(0,0);Check(a.ToDisplay(new(-300,-200,200,100))==new DockRect(0,0,200,100));
});
Test("Display and world coordinates round-trip without changing sizes",()=>{
    var a=MdiScrollLayout.Measure([new(-100,-150,1300,950)],new(600,400),300,200);
    var world=new DockRect(450,330,100,120);Check(a.ToWorld(a.ToDisplay(world))==world);
    Check(a.ToDisplay(world)==new DockRect(150,130,100,120));
});
Test("Changing extents preserves the visible world anchor and clamps removed ranges",()=>{
    var before=MdiScrollLayout.Measure([new(1000,700,200,100)],new(600,400),500,300);
    var after=MdiScrollLayout.Measure([new(-300,-200,100,100),new(1000,700,200,100)],new(600,400),before.Viewport.X,before.Viewport.Y);
    Check(after.Viewport==before.Viewport && after.HorizontalOffset==800 && after.VerticalOffset==500);
    var empty=MdiScrollLayout.Measure([],new(600,400),500,300);Check(empty.Viewport.X==0 && empty.Viewport.Y==0);
});
Test("Reveal moves minimally and leaves already visible frames unchanged",()=>{
    var a=MdiScrollLayout.Measure([new(1000,700,200,100)],new(600,400));
    Check(a.Reveal(new(20,20,100,100))==a);
    var b=a.Reveal(new(1000,700,200,100));
    Check(b.Viewport.X==616 && b.Viewport.Y==416); // Clamped at the trailing extent.
    Check(b.Reveal(new(1000,700,200,100))==b);
});
Test("Oversized frames align leading edges consistently",()=>{
    var target=new DockRect(100,120,900,700);
    var a=MdiScrollLayout.Measure([target],new(600,400)).Reveal(target);
    Check(a.Viewport.X==92 && a.Viewport.Y==112 && a.Reveal(target)==a);
});
Test("Minimized caption overflow remains reachable above the world origin",()=>{
    var captions=MinimizedWindowLayout.Arrange(Enumerable.Range(0,20).Select(i=>i.ToString()).ToArray(),new(0,0,200,100));
    var a=MdiScrollLayout.Measure(captions.Windows.Values.ToArray(),new(200,100));
    Check(a.HasVerticalScrollBar && a.ContentBounds.Y<0);
    var target=captions.Windows["19"];var b=a.Reveal(target);
    Check(b.ToDisplay(target).Y>=0 && b.ToDisplay(target).Bottom<=b.Viewport.Height);
    var six=Enumerable.Range(0,6).Select(i=>i.ToString()).ToArray();
    var full=MinimizedWindowLayout.Arrange(six,new(0,0,510,54));
    var reduced=MinimizedWindowLayout.Arrange(six,new(0,0,494,54));
    Check(!MdiScrollLayout.Measure(full.Windows.Values.ToArray(),new(510,54)).HasVerticalScrollBar);
    Check(MdiScrollLayout.Measure(reduced.Windows.Values.ToArray(),new(510,54)).HasVerticalScrollBar);
});
Test("Offsets are clamped and nonfinite inputs are rejected",()=>{
    var a=MdiScrollLayout.Measure([new(1000,700,200,100)],new(600,400));
    Check(a.WithOffsets(-10,-20).HorizontalOffset==0);
    Check(a.WithOffsets(1e30,1e30).HorizontalOffset==a.ScrollableWidth);
    Reject(()=>MdiScrollLayout.Measure([],new(0,1)));
    Reject(()=>MdiScrollLayout.Measure([new(double.MaxValue,0,double.MaxValue,10)],new(600,400)));
    Reject(()=>MdiScrollLayout.Measure([],new(600,400),double.NaN));
    Reject(()=>MdiScrollLayout.Measure([],new(600,400),scrollBarSize:-1));
    Reject(()=>MdiScrollLayout.Measure(new DockRect[10001],new(600,400)));
    Reject(()=>a.WithOffsets(double.PositiveInfinity,0));Reject(()=>a.Reveal(new(0,0,100,100),double.NaN));
});
Test("Small clients never produce negative viewport sizes",()=>{
    var a=MdiScrollLayout.Measure([new(-100,-100,1000,1000)],new(1,1));
    Check(a.Viewport.Width==1 && a.Viewport.Height==1 && a.ContentBounds.IsValid);
});
Test("One thousand seeded layouts enclose all frames and preserve coordinate inversion",()=>{
    var random=new Random(2414);
    for(int i=0;i<1000;i++) {
        var frames=Enumerable.Range(0,12).Select(_=>new DockRect(random.Next(-500,1800),random.Next(-500,1400),random.Next(20,400),random.Next(20,300))).ToArray();
        var a=MdiScrollLayout.Measure(frames,new(600,400),random.Next(-1000,2000),random.Next(-1000,2000));
        foreach(var r in frames) {Check(a.ContentBounds.X<=r.X && a.ContentBounds.Y<=r.Y && a.ContentBounds.Right>=r.Right && a.ContentBounds.Bottom>=r.Bottom);Check(a.ToWorld(a.ToDisplay(r))==r);}
        Check(a.HorizontalOffset>=0 && a.HorizontalOffset<=a.ScrollableWidth && a.VerticalOffset>=0 && a.VerticalOffset<=a.ScrollableHeight);
    }
});
Test("Warm measurement and reveal queries allocate no managed memory",()=>{
    var frames=Enumerable.Range(0,128).Select(i=>new DockRect(i*20-100,i*12-50,100,100)).ToArray();
    AllocationProbe.VerifyControls();
    Func<MdiScrollLayout> query=()=>MdiScrollLayout.Measure(frames,new(600,400)).Reveal(frames[80]);
    // Warm exactly the same measurement-plus-reveal path that is measured below.
    _=AllocationProbe.Measure(query,1000,out _);
    long bytes=AllocationProbe.Measure(query,10000,out var a);
    if(bytes!=0 || !a.Viewport.IsValid) throw new Exception($"Expected zero scroll-query bytes and a valid viewport; actual bytes={bytes}, viewport={a.Viewport}");
    Console.WriteLine($"ALLOCATION scroll geometry: 10000 measurements/reveals, 128 frames, {bytes} bytes");
});
Console.WriteLine($"{passed}/{passed+failed} MDI scroll specifications passed.");return failed==0?0:1;

static class RectExtensions { public static DockSize Size(this DockRect r)=>new(r.Width,r.Height); }
