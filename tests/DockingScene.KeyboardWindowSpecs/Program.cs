using DockingScene.Core;
using DockingScene.Layout;

int passed = 0, failed = 0;
void Check(bool ok) { if (!ok) throw new Exception("Assertion failed"); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
var original = new DockRect(100, 80, 400, 250);
var minimum = new DockSize(100, 100); var maximum = new DockSize(600, 500);
WindowKeyboardSession Session(WindowKeyboardMode mode) => new(original, mode);
void Step(WindowKeyboardSession s, WindowKeyboardDirection d, double n = 8) => s.Step(d,n,minimum,maximum);
Test("Keyboard move changes only coordinates", () => {
    var s=Session(WindowKeyboardMode.Move); Step(s,WindowKeyboardDirection.Right); Step(s,WindowKeyboardDirection.Up);
    Check(s.Bounds == new DockRect(108,72,400,250) && s.OriginalBounds == original);
});
Test("Resize chooses an edge before changing geometry", () => {
    var s=Session(WindowKeyboardMode.Resize); Step(s,WindowKeyboardDirection.Left);
    Check(s.HorizontalEdge == DockSide.Left && s.Bounds == original);
    Step(s,WindowKeyboardDirection.Left); Check(s.Bounds == new DockRect(92,80,408,250));
    Step(s,WindowKeyboardDirection.Right); Check(s.Bounds == original);
});
Test("Top edge resize keeps its bottom anchored", () => {
    var s=Session(WindowKeyboardMode.Resize); Step(s,WindowKeyboardDirection.Up); Step(s,WindowKeyboardDirection.Up);
    Check(s.Bounds.Y == 72 && s.Bounds.Bottom == original.Bottom && s.Bounds.Width == original.Width);
});
Test("Right and bottom edges grow without moving the origin", () => {
    var s=Session(WindowKeyboardMode.Resize);
    foreach(var d in new[]{WindowKeyboardDirection.Right,WindowKeyboardDirection.Right,WindowKeyboardDirection.Down,WindowKeyboardDirection.Down}) Step(s,d);
    Check(s.Bounds == new DockRect(100,80,408,258));
});
Test("Clamping at either limit remains reversible", () => {
    var s=Session(WindowKeyboardMode.Resize); Step(s,WindowKeyboardDirection.Right); Step(s,WindowKeyboardDirection.Right,1000);
    Check(s.Bounds.Width == 600); Step(s,WindowKeyboardDirection.Left,1000); Check(s.Bounds.Width == 100);
    Step(s,WindowKeyboardDirection.Right); Check(s.Bounds.Width == 108);
});
Test("Resizing one axis preserves an overconstrained untouched axis", () => {
    var s=new WindowKeyboardSession(new(0,0,400,50),WindowKeyboardMode.Resize);
    Step(s,WindowKeyboardDirection.Right); Step(s,WindowKeyboardDirection.Right);
    Check(s.Bounds.Height == 50 && s.Bounds.Width == 408);
});
Test("Cancel returns the exact original bounds", () => {
    var s=Session(WindowKeyboardMode.Move); Step(s,WindowKeyboardDirection.Down);
    Check(s.Complete(false) == original && s.IsCompleted && !s.HasChanges);
});
Test("Commit returns one final result and rejects late input", () => {
    var s=Session(WindowKeyboardMode.Move); Step(s,WindowKeyboardDirection.Left);
    Check(s.Complete(true) == new DockRect(92,80,400,250));
    bool rejected=false;try { Step(s,WindowKeyboardDirection.Right); } catch(InvalidOperationException){rejected=true;} Check(rejected);
});
Test("Invalid direction, increment and geometry cannot change the session", () => {
    var s=Session(WindowKeyboardMode.Move);
    foreach(double increment in new[]{0d,-1,double.NaN,double.PositiveInfinity}) {
        bool rejected=false;try{s.Step(WindowKeyboardDirection.Right,increment,minimum,maximum);}catch(ArgumentException){rejected=true;}Check(rejected && s.Bounds == original);
    }
    try { s.Step((WindowKeyboardDirection)99,8,minimum,maximum); throw new Exception("Enum accepted"); } catch(ArgumentException) { }
    try { _=new WindowKeyboardSession(new(0,0,0,100),WindowKeyboardMode.Move); throw new Exception("Empty accepted"); } catch(ArgumentException) { }
});
Test("Overflowing movement is rejected without changing bounds", () => {
    var r=new DockRect(double.MaxValue*.75,0,10,10);var s=new WindowKeyboardSession(r,WindowKeyboardMode.Move);
    try{s.Step(WindowKeyboardDirection.Right,double.MaxValue,minimum,maximum);throw new Exception("Overflow accepted");}catch(ArgumentException){Check(s.Bounds==r);}
});
Test("Seeded resize sequences stay within the chosen axis constraints", () => {
    var random=new Random(431072);
    for(int i=0;i<500;i++) {
        var s=Session(WindowKeyboardMode.Resize);double right=original.Right,bottom=original.Bottom;
        Step(s,WindowKeyboardDirection.Left);Step(s,WindowKeyboardDirection.Up);
        for(int j=0;j<20;j++)Step(s,(WindowKeyboardDirection)random.Next(4),random.Next(1,100));
        Check(s.Bounds.Width>=100 && s.Bounds.Width<=600 && s.Bounds.Height>=100 && s.Bounds.Height<=500);
        Check(s.Bounds.Right==right && s.Bounds.Bottom==bottom);
    }
});
DockManager Floating()
{
    var m=new DockManager();m.Register(new("d","Document",WindowKind.Document)).ThrowIfFailed();m.Float("d").ThrowIfFailed();
    m.UpdateWindow("d",w=>w with {Capabilities=DockCapabilities.Drag|DockCapabilities.Close}).ThrowIfFailed();return m;
}
Test("Floating movement needs Drag but not Resize", () => {
    var m=Floating();var h=m.Layout.FloatingHosts[0];m.MoveFloatingHost(h.Id,20,30).ThrowIfFailed();
    Check(m.Layout.FloatingHosts[0].Bounds==(h.Bounds with {X=20,Y=30}));
    Check(m.Layout.Windows["d"].FloatingBounds==m.Layout.FloatingHosts[0].Bounds);
    Check(!m.SetFloatingBounds(h.Id,h.Bounds with {Width=800}).Success);
});
Test("Floating no-op does not commit and move undo restores all metadata", () => {
    var m=Floating();var h=m.Layout.FloatingHosts[0];var before=m.Layout;
    m.MoveFloatingHost(h.Id,h.Bounds.X,h.Bounds.Y).ThrowIfFailed();Check(ReferenceEquals(before,m.Layout));
    m.MoveFloatingHost(h.Id,30,40).ThrowIfFailed();m.Undo().ThrowIfFailed();Check(m.Layout.FloatingHosts[0].Bounds==h.Bounds && m.Layout.Windows["d"].FloatingBounds==before.Windows["d"].FloatingBounds);
});
Test("Floating stale locked and nonfinite commands reject atomically", () => {
    var m=Floating();var h=m.Layout.FloatingHosts[0];var before=m.Layout;
    Check(!m.MoveFloatingHost(h.Id,0,0,before.Revision-1).Success);
    Check(!m.MoveFloatingHost(h.Id,double.NaN,0).Success && ReferenceEquals(before,m.Layout));
    m.SetLayoutLocked(true).ThrowIfFailed();before=m.Layout;
    Check(!m.MoveFloatingHost(h.Id,0,0).Success && ReferenceEquals(before,m.Layout));
});
Console.WriteLine($"{passed}/{passed+failed} keyboard window specifications passed.");return failed==0?0:1;
