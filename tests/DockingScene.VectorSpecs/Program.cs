using System.Globalization;
using DockingScene.Themes;

int passed = 0, failed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
void Invalid(string path)
{
    try { VectorPath.Parse(path); } catch (FormatException) { return; } throw new Exception("Invalid path accepted: " + path);
}
foreach (var field in typeof(DockGlyphs).GetFields())
{
    if (field.GetRawConstantValue() is string data) Test("Glyph " + field.Name, () => Check(VectorPath.Parse(data).Length > 0));
}
Test("Implicit repeated move operands become line segments", () => {
    var path = VectorPath.Parse("M1 2 3 4 5 6"); Check(path.Length == 3); Check(path[1].Kind == VectorSegmentKind.Line && path[2].X == 5);
});
Test("Relative moves and horizontal/vertical lines", () => {
    var path = VectorPath.Parse("m1 2 3 4 h5 v-2"); Check(path[^1].X == 9 && path[^1].Y == 4);
});
Test("Signed adjacency, decimal fractions and exponents", () => {
    var path = VectorPath.Parse("M.5-.25L1e2-2E-1"); Check(path[0].Y == -.25 && path[1].X == 100 && path[1].Y == -.2);
});
Test("Cubic smooth control reflects previous cubic control", () => {
    var path = VectorPath.Parse("M0 0 C1 2 3 4 5 6 S9 10 11 12"); Check(path[^1].Control1X == 7 && path[^1].Control1Y == 8);
});
Test("Smooth control resets after an unrelated segment", () => {
    var path = VectorPath.Parse("M0 0 C1 2 3 4 5 6 L7 8 S9 10 11 12"); Check(path[^1].Control1X == 7 && path[^1].Control1Y == 8);
});
Test("Relative quadratic and reflected smooth quadratic", () => {
    var path = VectorPath.Parse("M10 10 q1 2 3 4 t5 6"); Check(path[^1].Control1X == 15 && path[^1].Control1Y == 16); Check(path[^1].X == 18 && path[^1].Y == 20);
});
Test("Arc flags can be adjacent without confusing endpoint digits", () => {
    var path = VectorPath.Parse("M0 0 A5 6 30 0110 20"); Check(path[^1].Kind == VectorSegmentKind.Arc && path[^1].Sweep && !path[^1].LargeArc && path[^1].X == 10);
});
Test("Zero-radius arcs degrade to exact straight segments", () => Check(VectorPath.Parse("M0 0 A0 6 0 0 1 10 20")[^1].Kind == VectorSegmentKind.Line));
Test("Closing returns the current point to the subpath origin", () => {
    var path = VectorPath.Parse("M1 2 L3 4 Z m2 3"); Check(path[^1].X == 3 && path[^1].Y == 5);
});
Test("Parsing ignores current-culture number punctuation", () => {
    var old = CultureInfo.CurrentCulture;
    try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL"); Check(VectorPath.Parse("M1.5,2.5")[0].X == 1.5); }
    finally { CultureInfo.CurrentCulture = old; }
});
foreach (string path in new[] { "L1 2", "M1", "M,1 2", "M1,,2", "M1 2,", "M1e 2", "M1e999 2", "M0 0 R1 2", "M0 0 A-1 2 0 0 1 2 3", "M0 0 A1 2 0 2 0 3 4", "M0 0 Z 1 2", "M1e308 0 l1e308 0" })
    Test("Reject " + path, () => Invalid(path));
Test("Segment budget is enforced", () => {
    try { VectorPath.Parse("M0 0 L1 1 L2 2", 2); } catch (FormatException) { return; } throw new Exception("Budget ignored");
});
Console.WriteLine($"{passed}/{passed + failed} vector path specifications passed.");
return failed == 0 ? 0 : 1;
