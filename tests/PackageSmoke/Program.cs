using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Mvvm;
using DockingScene.Serialization;
using DockingScene.Themes;

var manager = new DockManager();
using var model = new DockWorkspaceViewModel(manager);
model.Register(new("document", "Document", WindowKind.Document)).ThrowIfFailed();
model.Register(new("tool", "Tool", WindowKind.Tool)).ThrowIfFailed();
var geometry = DockLayoutEngine.Arrange(manager.Layout, new DockRect(0, 0, 1280, 720));
if (geometry.Nodes.Count == 0 || model.Windows.Count != 2) throw new Exception("Package model/layout integration failed.");
var loaded = DockLayoutSerializer.Deserialize(DockLayoutSerializer.Serialize(manager.Layout));
if (!loaded.Success || loaded.Layout!.Windows.Count != 2) throw new Exception("Package persistence integration failed.");
if (VectorPath.Parse(DockGlyphs.Close).Length == 0) throw new Exception("Package vector integration failed.");
manager.Float("document").ThrowIfFailed();
if (model.Find("document")!.Placement != WindowPlacement.Floating) throw new Exception("Package notification integration failed.");
using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(DockLayoutSerializer.Serialize(manager.Layout)));
if (!(await DockLayoutStream.ReadAsync(input)).Success || !input.CanRead) throw new Exception("Package bounded stream integration failed.");
manager.RegisterMany(Enumerable.Range(0, 100).Select(i => new DockWindow("bulk" + i, "Bulk", WindowKind.Document))).ThrowIfFailed();
if (manager.Layout.Windows.Count != 102 || model.Windows.Count != 102) throw new Exception("Package bulk registration integration failed.");
var cache = new DockGeometryCache();
var first = cache.Arrange(manager.Layout, new(0, 0, 1280, 720));
manager.Activate("bulk0").ThrowIfFailed();
if (!ReferenceEquals(first, cache.Arrange(manager.Layout, new(0, 0, 1280, 720)))) throw new Exception("Package geometry cache integration failed.");
if (TabEdgeScroll.NextOffset(200, 200, 1000, 300, .1) <= 300) throw new Exception("Package drag edge scroll integration failed.");
var minimized = MinimizedWindowLayout.Arrange(new[] { "document", "bulk0" }, new(0, 0, 170, 100));
if (minimized.Windows["bulk0"].Y >= minimized.Windows["document"].Y) throw new Exception("Minimized captions did not wrap.");
manager.MoveMdiWindow("bulk0", 40, 60).ThrowIfFailed();


var keyboard = new WindowKeyboardSession(new(10,20,200,100), WindowKeyboardMode.Move);
keyboard.Step(WindowKeyboardDirection.Right, 8, new(1,1), new(1000,1000));
if (keyboard.Complete(true).X != 18) throw new Exception("Packaged keyboard geometry failed.");
manager.UpdateWindow("bulk0", w => w with { CanStandardMdiMaximize = false }).ThrowIfFailed();
if (MdiPrimaryWindow.Resolve(manager.Layout, new[] { "bulk0" }).CanMaximize)
    throw new Exception("Individual MDI policy package integration failed.");
Console.WriteLine("PASS: UI-independent package-only consumer: core/layout/serialization/MVVM/themes executed.");
