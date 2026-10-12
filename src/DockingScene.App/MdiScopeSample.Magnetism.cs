using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class MdiScopeSample
{
    private void ConfigureMagnetismSample(Grid grid)
    {
        grid.RowDefinitions.Insert(1, new() { Height = GridLength.Auto });
        Grid.SetRow(_left, 2); Grid.SetRow(_right, 2);
        DockChrome.Identify(_left, "scope:left-host"); DockChrome.Identify(_right, "scope:right-host");
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new(4,0,4,4) };
        Grid.SetRow(tools, 1); Grid.SetColumnSpan(tools, 2); grid.Children.Add(tools);
        void Button(string label, string id, Action action) => tools.Children.Add(DockChrome.Button(label, "scope:" + id, DockTheme.MetroLight, action));
        Button("Magnetic layout", "magnetic-reset", ResetMagneticLayout);
        Button("Disable snapping", "magnetic-off", () => _left.MagnetismSnapDistance = 0);
        Button("5-unit snapping", "magnetic-on", () => _left.MagnetismSnapDistance = 5);
        Button("8-unit gap", "magnetic-gap8", () => _left.MagnetismGapDistance = 8);
        Button("1-unit gap", "magnetic-gap1", () => _left.MagnetismGapDistance = 1);
    }
    private void ResetMagneticLayout()
    {
        _leftActive = false; _left.ReleaseContent();
        _releasedDocument = null;
        foreach (string id in new[] { "a", "b" })
            _content[id] = new() { Id = id, Header = id, Content = new TextBox { AcceptsReturn = true, Text = "Snap document " + id } };
        _left.MagnetismSnapDistance = 5; _left.MagnetismGapDistance = 1;
        _leftActive = true;
        _lastResult = _manager.Transaction("Prepare magnetism example", new Func<DockManager,DockResult>[]
        {
            m => m.SetLayoutLocked(false),
            m => m.SetMdiState("a", MdiWindowState.Normal), m => m.SetMdiState("b", MdiWindowState.Normal),
            m => m.SetMdiBounds("a", new(16,24,160,140)), m => m.SetMdiBounds("b", new(220,24,160,140)),
            m => m.Activate("a")
        });
        Project();
    }
    private void WriteMagnetismDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteNumber("snapDistance", _left.MagnetismSnapDistance);
        writer.WriteNumber("gapDistance", _left.MagnetismGapDistance);
        writer.WriteNumber("planBuilds", _left.MagnetismPlanBuildCount);
        writer.WriteStartObject("leftFrames");
        foreach (var (id, frame) in _left.Windows)
        {
            writer.WriteStartObject(id);
            writer.WriteNumber("x", frame.Bounds.X); writer.WriteNumber("y", frame.Bounds.Y);
            writer.WriteNumber("width", frame.Bounds.Width); writer.WriteNumber("height", frame.Bounds.Height);
            writer.WriteBoolean("moving", frame.IsMoving); writer.WriteBoolean("magnetic", frame.HasPointerMagnetism);
            writer.WriteNumber("queries", frame.MagnetismQueryCount);
            writer.WriteNumber("rawX", frame.RawPointerBounds.X); writer.WriteNumber("rawWidth", frame.RawPointerBounds.Width);
            writer.WriteNumber("contentInstance", _content.TryGetValue(id, out var content) ? RuntimeHelpers.GetHashCode(content) : 0);
            writer.WriteStartObject("buttons"); foreach (var (name, identity) in frame.CaptionButtonIdentities) writer.WriteNumber(name, identity); writer.WriteEndObject();
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}
