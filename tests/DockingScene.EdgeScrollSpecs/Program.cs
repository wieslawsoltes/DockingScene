using DockingScene.Layout;
int passed = 0, failed = 0;
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e); } }
Test("Center and positions outside the lane never scroll", () => { foreach (double x in new[] { -1d, 100, 201 }) Check(TabEdgeScroll.NextOffset(x, 200, 1000, 300, .1) == 300); });
Test("Leading and trailing edges scroll in opposite directions", () => { Check(TabEdgeScroll.NextOffset(0, 200, 1000, 300, .1) == 240); Check(TabEdgeScroll.NextOffset(200, 200, 1000, 300, .1) == 360); });
Test("Speed ramps continuously with distance from the edge", () => { Check(TabEdgeScroll.NextOffset(12, 200, 1000, 300, .1) == 270); Check(TabEdgeScroll.NextOffset(24, 200, 1000, 300, .1) == 300); });
Test("Short content and zero viewports are stable", () => { Check(TabEdgeScroll.NextOffset(0, 200, 100, 0, 1) == 0); Check(TabEdgeScroll.NextOffset(0, 0, 1000, 5, 1) == 5); });
Test("Zero time does no work and delayed ticks cannot jump", () => { Check(TabEdgeScroll.NextOffset(200, 200, 1000, 300, 0) == 300); Check(TabEdgeScroll.NextOffset(200, 200, 1000, 300, 10) == 360); });
Test("Scroll extent clamps at both ends", () => { Check(TabEdgeScroll.NextOffset(0, 200, 1000, 1, .1) == 0); Check(TabEdgeScroll.NextOffset(200, 200, 1000, 799, .1) == 800); });
Test("Small viewports split the edge bands without overlap", () => { Check(TabEdgeScroll.NextOffset(5, 10, 1000, 100, .1) == 100); Check(TabEdgeScroll.NextOffset(10, 10, 1000, 100, .1) == 160); });
Test("Nonfinite input and invalid options reject", () => { foreach (Action invalid in new Action[] { () => TabEdgeScroll.NextOffset(double.NaN, 100, 1000, 0, .1), () => TabEdgeScroll.NextOffset(0, -1, 10, 0, .1), () => TabEdgeScroll.NextOffset(0, 1, 10, -1, .1), () => TabEdgeScroll.NextOffset(0, 1, 10, 0, -.1), () => TabEdgeScroll.NextOffset(0, 1, 10, 0, .1, 0), () => TabEdgeScroll.NextOffset(0, 1, 10, 0, .1, 24, double.PositiveInfinity) }) { bool rejected = false; try { invalid(); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected); } });
Test("Equal elapsed time yields equal offsets across tick rates", () => { double a = 100, b = 100; for (int i = 0; i < 10; i++) a = TabEdgeScroll.NextOffset(200, 200, 2000, a, .1); for (int i = 0; i < 20; i++) b = TabEdgeScroll.NextOffset(200, 200, 2000, b, .05); Check(Math.Abs(a - b) < .0001); });
Test("Extreme finite extents cannot overflow the result", () => { double next = TabEdgeScroll.NextOffset(100, 100, double.MaxValue, double.MaxValue * .99, .1, 24, double.MaxValue); Check(double.IsFinite(next) && next <= double.MaxValue); });
Console.WriteLine($"{passed}/{passed + failed} edge scrolling specifications passed.");
return failed == 0 ? 0 : 1;
