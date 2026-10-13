using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace DockingScene.App;

/// <summary>Standalone native MDI scrolling consumer. Buttons and keyboard input
/// perform operations; diagnostics only observe geometry and retained identities.</summary>
public sealed class MdiScrollSample : UserControl
{
    private readonly StandardMdiHost _host = new() { AutomationIdPrefix = "sample" };
    private readonly Dictionary<string, DocumentWindow> _content = new(StringComparer.Ordinal);
    private DockManager _manager = new();
    private string[] _ids = [];
    private DockTheme _theme = DockTheme.MetroLight;
    private int _generation;
    private string? _error;
    public MdiScrollSample()
    {
        RequestedTheme = ElementTheme.Light;
        var grid = new Grid(); Content = grid;
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new(4) };
        grid.Children.Add(toolbar); grid.Children.Add(_host); Grid.SetRow(_host, 1);
        DockChrome.Identify(_host, "scroll-sample:host");
        void Button(string text, string id, Action action) => toolbar.Children.Add(DockChrome.Button(text, "scroll-sample:" + id, _theme, action));
        Button("Reset", "reset", () => Reset(false));
        Button("Enable scrolling", "enable", () => _host.IsScrollingEnabled = true);
        Button("Disable", "disable", () => _host.IsScrollingEnabled = false);
        Button("Origin", "origin", () => _host.ScrollTo(-_host.ScrollLayout.ContentBounds.X, -_host.ScrollLayout.ContentBounds.Y));
        Button("Far", "far", () => Check(_manager.Activate("scroll-far")));
        Button("Negative", "negative", () => Check(_manager.Activate("scroll-negative")));
        Button("Reveal far", "reveal", () => _host.ScrollWindowIntoView("scroll-far"));
        Button("Minimized rows", "minimized", () => Reset(true));
        Button("Undo", "undo", () => Check(_manager.Undo()));
        Button("Dark / Light (F2)", "theme", ToggleTheme);
        KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.F2) { ToggleTheme(); e.Handled = true; }
            if (e.Key == VirtualKey.F4)
            { _host.ScrollTo(_host.ScrollLayout.HorizontalOffset - 64, _host.ScrollLayout.VerticalOffset - 64); e.Handled = true; }
        };
        _host.OperationRejected += error => _error = error;
        Loaded += (_, _) => Project();
        Reset(false);
    }
    private void Check(DockResult result) { if (!result.Success) _error = result.Error; }
    private void Reset(bool minimized)
    {
        _manager.Changed -= Changed;
        _host.ReleaseContent(); _content.Clear(); _error = null;
        _host.Height = minimized ? 140 : double.NaN;
        _host.VerticalAlignment = minimized ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        _ids = minimized ? Enumerable.Range(0, 40).Select(i => "scroll-min-" + i).ToArray() : ["scroll-home", "scroll-far", "scroll-negative"];
        var manager = new DockManager();
        manager.RegisterMany(_ids.Select((id, i) => new DockWindow(id, minimized ? "Minimized " + i : id, WindowKind.Document)
        { MdiBounds = id switch { "scroll-far" => new(1400, 1000, 220, 180), "scroll-negative" => new(-360, -240, 240, 160), _ => new(30, 35, 220, 180) } })).ThrowIfFailed();
        manager.SetMdiMode(MdiMode.Standard).ThrowIfFailed();
        if (minimized) manager.Transaction("Minimize example documents", _ids.Select(id => new Func<DockManager, DockResult>(m => m.SetMdiState(id, MdiWindowState.Minimized)))).ThrowIfFailed();
        manager.Activate(_ids[0]).ThrowIfFailed();
        _manager = manager; _manager.Changed += Changed; _generation++;
        if (minimized) _host.IsScrollingEnabled = true;
        Project();
    }
    private void Changed(object? sender, DockChangedEventArgs e) => Project();
    private AdvancedTabItem Window(string id)
    {
        if (!_content.TryGetValue(id, out var window))
        {
            var editor = DockChrome.Identify(new TextBox { AcceptsReturn = true, Text = "Retained editor: " + id + "\nWindow bounds stay in world coordinates.",
                FontSize = 12, Padding = new(8), BorderThickness = new(0), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                Foreground = DockChrome.Brush(_theme.Text), Background = DockChrome.Brush(_theme.EditorBackground) }, "scroll-sample:editor:" + id);
            window = new DocumentWindow { Content = editor, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            _content.Add(id, window);
        }
        window.Update(_manager.Layout.Windows[id]); return window;
    }
    private void Project() => _host.Update(_manager, _ids, Window, _theme);
    private void ToggleTheme()
    {
        _theme = _theme.IsDark ? DockTheme.MetroLight : DockTheme.MetroDark;
        RequestedTheme = _theme.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        foreach (var window in _content.Values)
            if (window.Content is TextBox editor)
            { editor.Foreground = DockChrome.Brush(_theme.Text); editor.Background = DockChrome.Brush(_theme.EditorBackground); }
        Project();
    }
    public void WriteDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("mdiScrolling");
        writer.WriteNumber("generation", _generation); writer.WriteNumber("revision", _manager.Layout.Revision);
        writer.WriteBoolean("enabled", _host.IsScrollingEnabled); writer.WriteString("active", _manager.Layout.ActiveWindowId);
        writer.WriteString("error", _error); writer.WriteBoolean("dark", _theme.IsDark);
        writer.WriteNumber("builds", _host.ScrollGeometryBuildCount); writer.WriteNumber("reuses", _host.ScrollGeometryReuseCount);
        var scroll = _host.ScrollLayout;
        writer.WriteNumber("offsetX", scroll.HorizontalOffset); writer.WriteNumber("offsetY", scroll.VerticalOffset);
        writer.WriteNumber("maxX", scroll.ScrollableWidth); writer.WriteNumber("maxY", scroll.ScrollableHeight);
        writer.WriteBoolean("horizontal", scroll.HasHorizontalScrollBar); writer.WriteBoolean("vertical", scroll.HasVerticalScrollBar);
        void Rect(string name, DockRect rect)
        { writer.WriteStartObject(name); writer.WriteNumber("x", rect.X); writer.WriteNumber("y", rect.Y); writer.WriteNumber("width", rect.Width); writer.WriteNumber("height", rect.Height); writer.WriteEndObject(); }
        Rect("viewport", scroll.Viewport); Rect("extent", scroll.ContentBounds);
        writer.WriteStartObject("windows");
        foreach (var id in _ids)
        {
            writer.WriteStartObject(id); Rect("model", _manager.Layout.Windows[id].MdiBounds);
            writer.WriteString("state", _manager.Layout.Windows[id].MdiState.ToString());
            if (_host.Windows.TryGetValue(id, out var frame))
            {
                Rect("frame", frame.Bounds); writer.WriteNumber("identity", RuntimeHelpers.GetHashCode(frame)); writer.WriteBoolean("moving", frame.IsMoving);
                writer.WriteStartObject("buttons"); foreach (var pair in frame.CaptionButtonIdentities) writer.WriteNumber(pair.Key, pair.Value); writer.WriteEndObject();
            }
            if (_content.TryGetValue(id, out var content)) writer.WriteNumber("editor", RuntimeHelpers.GetHashCode(content.Content!));
            writer.WriteEndObject();
        }
        writer.WriteEndObject(); writer.WriteEndObject();
    }
}
