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
Console.WriteLine("PASS: UI-independent package-only consumer: core/layout/serialization/MVVM/themes executed.");

