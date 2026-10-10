using System.Collections.Immutable;
using DockingScene.Core;
using DockingScene.Serialization;

var results = new List<(string Name, bool Passed)>();
void Test(string name, Action action)
{
    try { action(); results.Add((name, true)); Console.WriteLine("PASS " + name); }
    catch (Exception error) { results.Add((name, false)); Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Reject(DockLayout layout)
{
    try { DockLayoutValidator.Validate(layout); } catch (InvalidOperationException) { return; }
    throw new Exception("An invalid layout was accepted.");
}
DockLayout Sample()
{
    var manager = new DockManager();
    manager.Register(new("document", "Document", WindowKind.Document)).ThrowIfFailed();
    manager.Register(new("tool", "Tool", WindowKind.Tool), targetId: "workspace", side: DockSide.Right).ThrowIfFailed();
    return manager.Layout;
}
Test("Valid layout passes bounded structural validation", () => DockLayoutValidator.Validate(Sample()));
Test("Null registry rejected before traversal", () => Reject(new() { Windows = null! }));
Test("Default immutable arrays are rejected", () => Reject(new() { FloatingHosts = default }));
Test("Null floating host rejected", () => Reject(new() { FloatingHosts = [null!] }));
Test("Null auto-hide group rejected", () => Reject(new() { AutoHideGroups = [null!] }));
Test("Null metadata rejected", () => Reject(new() { Windows = ImmutableDictionary<string, DockWindow>.Empty.Add("x", null!), HiddenWindows = ["x"] }));
Test("Invalid colors rejected before renderer can parse them", () => {
    var layout = Sample(); Reject(layout with { Windows = layout.Windows.SetItem("tool", layout.Windows["tool"] with { TabColor = "invalid" }) });
});
Test("ARGB tab color survives serialization", () => {
    var layout = Sample(); layout = layout with { Windows = layout.Windows.SetItem("tool", layout.Windows["tool"] with { TabColor = "#80aBcDeF" }) };
    Check(DockLayoutSerializer.Deserialize(DockLayoutSerializer.Serialize(layout)).Success);
});
Test("Overflowing rectangle edges rejected", () => {
    var layout = Sample(); Reject(layout with { Windows = layout.Windows.SetItem("tool", layout.Windows["tool"] with { FloatingBounds = new(double.MaxValue, 0, double.MaxValue, 10) }) });
});
Test("Missing bookmark rejected", () => {
    var layout = Sample(); Reject(layout with { Windows = layout.Windows.SetItem("tool", layout.Windows["tool"] with { Bookmark = null! }) });
});
Test("Deep workspace input terminates with a validation failure", () => {
    DockNode node = new DockWorkspace("leaf");
    for (int i = 0; i < 4096; i++) node = new DockWorkspace("nested-" + i, node);
    Reject(new() { Root = node });
});
Test("A cyclic application-created tree is rejected without recursion", () => {
    var node = new DockWorkspace("cycle");
    typeof(DockWorkspace).GetProperty(nameof(DockWorkspace.Content))!.SetValue(node, node);
    Reject(new() { Root = node });
});
Test("Duplicate node identities rejected", () => Reject(new() { Root = new DockSplit("split", SplitAxis.Horizontal, .5, new DockWorkspace("same"), new DockWorkspace("same")) }));
Test("Null JSON collections fail without escaping", () => {
    var json = DockLayoutSerializer.Serialize(Sample()).Replace("\"floatingHosts\": []", "\"floatingHosts\": null");
    Check(!DockLayoutSerializer.Deserialize(json).Success);
});
Test("Unknown windows may be ignored without dangling references", () => {
    var options = new LayoutLoadOptions { KnownWindowIds = new HashSet<string> { "document" }, UnknownWindows = UnknownWindowHandling.Ignore };
    var loaded = DockLayoutSerializer.Deserialize(DockLayoutSerializer.Serialize(Sample()), options);
    Check(loaded.Success); Check(loaded.Layout!.Windows.Count == 1); DockLayoutValidator.Validate(loaded.Layout);
});
Test("Schema version remains mandatory", () => Check(!DockLayoutSerializer.Deserialize("{}").Success));
Console.WriteLine($"{results.Count(x => x.Passed)}/{results.Count} parity regression specifications passed.");
return results.All(x => x.Passed) ? 0 : 1;
