using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Serialization;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; }
var manager = new DockManager();
manager.Register(new("doc", "Document", WindowKind.Document)).ThrowIfFailed();
manager.Register(new("left", "Explorer", WindowKind.Tool), targetId: "workspace", side: DockSide.Left).ThrowIfFailed();
manager.Register(new("bottom", "Output", WindowKind.Tool), targetId: "workspace", side: DockSide.Bottom).ThrowIfFailed();
foreach (double width in new[] { 0d, 1, 60, 320, 1280, 3840 })
foreach (double scale in new[] { 1d, 1.25, 1.5, 2 })
{
    var geometry = DockLayoutEngine.Arrange(manager.Layout, new(0, 0, width, 720), new() { DpiScale = scale });
    foreach (var rect in geometry.Nodes.Values) Check(rect.Width >= 0 && rect.Height >= 0 && double.IsFinite(rect.X), "finite nonnegative geometry");
    foreach (var split in geometry.Splitters) Check(split.Bounds.Right <= width + .001, "split fits viewport");
}
var saved = DockLayoutSerializer.Serialize(manager.Layout);
var loaded = DockLayoutSerializer.Deserialize(saved);
Check(loaded.Success, loaded.Error ?? "roundtrip");
Check(DockLayoutSerializer.Serialize(loaded.Layout!) == saved, "canonical JSON roundtrip");
Check(!DockLayoutSerializer.Deserialize("{\"schemaVersion\":99}").Success, "future schema rejected");
Check(!DockLayoutSerializer.Deserialize("{\"schemaVersion\":1,\"windows\":null}").Success, "null registry rejected");
Check(!DockLayoutSerializer.Deserialize(saved, new() { MaximumBytes = 10 }).Success, "size limit");
Check(!DockLayoutSerializer.Deserialize(saved.Replace("\"$type\": \"group\"", "\"$type\": \"process\"")).Success, "unknown discriminator rejected");
var known = new HashSet<string> { "doc" };
var partial = DockLayoutSerializer.Deserialize(saved, new() { KnownWindowIds = known, UnknownWindows = UnknownWindowHandling.Ignore });
Check(partial.Success && partial.Layout!.Windows.Count == 1, "unknown windows ignored structurally");
Check(!DockLayoutSerializer.Deserialize(saved, new() { KnownWindowIds = known, UnknownWindows = UnknownWindowHandling.Reject }).Success, "unknown windows rejected");
var tiled = WindowGeometry.ArrangeMdi(new[] { "a", "b", "c", "d", "e" }, new(0, 0, 1000, 600), MdiArrangement.TileGrid);
Check(Math.Abs(tiled.Values.Sum(r => r.Width * r.Height) - 600000) < .001, "grid fills area exactly");
var snapped = WindowGeometry.Snap(new(105, 50, 100, 100), [new(0, 0, 100, 200)]);
Check(snapped.X == 100, "window magnetism");
var resized = WindowGeometry.Resize(new(50, 50, 300, 200), 400, 500, DockSide.Left, DockSide.Top, new(80, 60), new(600, 600));
Check(resized.Right == 350 && resized.Bottom == 250 && resized.Width == 80 && resized.Height == 60, "anchored resize clamps");
var folder = Path.Combine(Path.GetTempPath(), "DockingScene-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new FileDockLayoutStore(folder);
    await store.SaveAsync("desktop", manager.Layout);
    Check((await store.LoadAsync("desktop")).Success, "atomic disk roundtrip");
    bool rejected = false;
    try { await store.SaveAsync("../bad", manager.Layout); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "profile traversal rejected");
}
finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
Console.WriteLine($"PASS: {passed} layout, geometry, serialization and storage assertions.");
