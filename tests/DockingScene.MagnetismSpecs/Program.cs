using System.Diagnostics;
using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool value) { if (!value) throw new Exception("Assertion failed."); }
void Reject(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Expected invalid input rejection.");
}
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockSize min = new(10,10), max = new(1000,1000);
Test("Documented defaults and adjacent horizontal gap preserve size", () => {
    var plan = new WindowMagnetism(new[]{new DockRect(0,0,100,200)});
    var source = new DockRect(106,50,100,100);
    Check(plan.SnapDistance==5 && plan.GapDistance==1 && plan.NeighborCount==1);
    Check(plan.Move(source)==source with { X=101 });
});
Test("Right and vertical adjacency use the same configured gap", () => {
    var source=new DockRect(100,50,100,100);
    Check(new WindowMagnetism(new[]{new DockRect(205,0,100,200)}).Move(source).X==104);
    Check(new WindowMagnetism(new[]{new DockRect(0,155,300,100)}).Move(source).Y==54);
    Check(new WindowMagnetism(new[]{new DockRect(0,-56,300,100)}).Move(source).Y==45);
});
Test("Same-edge alignment snaps both axes without changing extents", () => {
    var source=new DockRect(13,17,80,60);
    Check(new WindowMagnetism(new[]{new DockRect(10,20,100,100)}).Move(source)==new DockRect(10,20,80,60));
});
Test("Far perpendicular windows cannot attract a passing edge", () => {
    var source=new DockRect(105,500,100,100);
    Check(new WindowMagnetism(new[]{new DockRect(0,0,100,100)}).Move(source)==source);
});
Test("Zero disables magnetism and empty plans preserve identity values", () => {
    var source=new DockRect(105,20,100,100);
    Check(new WindowMagnetism(new[]{new DockRect(0,0,100,200)},0).Move(source)==source);
    Check(new WindowMagnetism(Array.Empty<DockRect>()).Move(source)==source);
});
Test("Equal-distance ties are stable under reversed neighbor order", () => {
    var neighbors=new[]{new DockRect(103,0,100,100),new DockRect(97,0,100,100)};
    var source=new DockRect(100,0,100,100);
    var first=new WindowMagnetism(neighbors,5,0).Move(source); Array.Reverse(neighbors);
    Check(first.X==97 && new WindowMagnetism(neighbors,5,0).Move(source)==first);
});
Test("The gesture plan copies caller rectangles rather than aliasing mutable arrays", () => {
    var neighbors=new[]{new DockRect(0,0,100,200)};var plan=new WindowMagnetism(neighbors);
    neighbors[0]=new(1000,1000,10,10);
    Check(plan.Move(new(105,50,100,100)).X==101);
});
Test("Left and top resizing preserve their opposite edge exactly", () => {
    var plan=new WindowMagnetism(new[]{new DockRect(0,0,100,200)});
    var result=plan.Resize(new(104,50,100,100),DockSide.Left,DockSide.Center,min,max);
    Check(result==new DockRect(101,50,103,100) && result.Right==204);
    result=new WindowMagnetism(new[]{new DockRect(0,0,300,100)}).Resize(new(50,104,100,100),DockSide.Center,DockSide.Top,min,max);
    Check(result==new DockRect(50,101,100,103) && result.Bottom==204);
});
Test("Right and bottom resizing preserve the original corner", () => {
    var plan=new WindowMagnetism(new[]{new DockRect(200,0,100,100),new DockRect(0,200,100,100)});
    var result=plan.Resize(new(0,0,196,196),DockSide.Right,DockSide.Bottom,min,max);
    Check(result==new DockRect(0,0,199,199));
});
Test("An invalid nearest edge does not hide a valid constrained alternative", () => {
    var plan=new WindowMagnetism(new[]{new DockRect(103,0,100,100),new DockRect(97,0,100,100)});
    var result=plan.Resize(new(100,0,100,100),DockSide.Left,DockSide.Center,new(100,10),max);
    Check(result.X==97 && result.Width==103);
});
Test("Untouched overconstrained dimensions never resize", () => {
    var plan=new WindowMagnetism(new[]{new DockRect(0,100,10,20)});
    var result=plan.Resize(new(0,0,1,96),DockSide.Center,DockSide.Bottom,new(80,60),max);
    Check(result==new DockRect(0,0,1,99));
});
Test("Raw-pointer reversal escapes attraction without accumulated drift", () => {
    var plan=new WindowMagnetism(new[]{new DockRect(0,0,100,200)});
    Check(plan.Move(new(105,50,100,100)).X==101);
    Check(plan.Move(new(110,50,100,100)).X==110);
    Check(plan.Move(new(99,50,100,100)).X==101);
});
Test("Invalid and overflowing geometry and options are rejected", () => {
    Reject(()=>new WindowMagnetism(new[]{new DockRect(double.MaxValue,0,double.MaxValue,1)}));
    Reject(()=>new WindowMagnetism(Array.Empty<DockRect>(),double.NaN));
    Reject(()=>new WindowMagnetism(Array.Empty<DockRect>(),5,-1));
    Reject(()=>new WindowMagnetism(new DockRect[10001]));
    var plan=new WindowMagnetism(Array.Empty<DockRect>());
    Reject(()=>plan.Move(new(0,0,0,10)));
    Reject(()=>plan.Resize(new(0,0,10,10),DockSide.Top,DockSide.Center,min,max));
    Reject(()=>plan.Resize(new(0,0,10,10),DockSide.Left,DockSide.Top,new(100,100),new(10,10)));
});
Test("Overflowing optional targets are ignored, never emitted as invalid geometry", () => {
    var plan=new WindowMagnetism(new[]{new DockRect(0,0,100,100)},5,double.MaxValue);
    var result=plan.Move(new(10,10,100,100));
    Check(result.IsValid && double.IsFinite(result.Right) && double.IsFinite(result.Bottom));
});
Test("One thousand seeded constrained resizes keep anchors and valid sizes", () => {
    var random=new Random(44281);
    for(int i=0;i<1000;i++) {
        var raw=new DockRect(random.Next(-200,200),random.Next(-200,200),random.Next(30,300),random.Next(30,300));
        var neighbors=Enumerable.Range(0,8).Select(_=>new DockRect(random.Next(-300,400),random.Next(-300,400),random.Next(20,200),random.Next(20,200))).ToArray();
        var plan=new WindowMagnetism(neighbors,8,2);
        var horizontal=i%2==0?DockSide.Left:DockSide.Right;var vertical=i%3==0?DockSide.Top:DockSide.Bottom;
        var result=plan.Resize(raw,horizontal,vertical,new(20,20),new(400,400));
        Check(result.IsValid && result.Width>=20 && result.Width<=400 && result.Height>=20 && result.Height<=400);
        Check(horizontal==DockSide.Left?result.Right==raw.Right:result.X==raw.X);
        Check(vertical==DockSide.Top?result.Bottom==raw.Bottom:result.Y==raw.Y);
    }
});
Test("Warm magnetism queries perform no per-pointer managed allocation", () => {
    var plan=new WindowMagnetism(Enumerable.Range(0,128).Select(i=>new DockRect(i*20,0,10,200)).ToArray());
    DockRect result=default, raw=new(105,50,100,100);
    for(int i=0;i<200;i++) result=plan.Move(raw);
    long start=GC.GetAllocatedBytesForCurrentThread(), time=Stopwatch.GetTimestamp();
    for(int i=0;i<10000;i++) { result=plan.Move(raw); result=plan.Resize(result,DockSide.Right,DockSide.Bottom,min,max); }
    long elapsed=Stopwatch.GetTimestamp()-time, bytes=GC.GetAllocatedBytesForCurrentThread()-start;
    Check(result.IsValid && bytes==0);
    Console.WriteLine($"BENCHMARK magnetism 20000 queries/128 neighbors: {elapsed*1000.0/Stopwatch.Frequency:F3}ms {bytes}B; plan construction excluded");
});
Console.WriteLine($"{passed}/{passed+failed} magnetism specifications passed.");
return failed==0?0:1;
