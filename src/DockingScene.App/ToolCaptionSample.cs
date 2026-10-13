using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.App;

/// <summary>Nested DockSite exercising native tool captions, policy and retained
/// title controls. Toolbar actions are ordinary application commands.</summary>
public sealed partial class ToolCaptionSample : UserControl
{
    private readonly DockSite _site = new() { OpenAutoHideOnHover = false };
    private readonly Dictionary<string, TextBox> _editors = new(StringComparer.Ordinal);
    private DockManager _manager = new();
    private string _toolHost = "", _documentHost = "";
    private DockTheme _theme = DockTheme.MetroLight;
    private int _generation;
    private string? _error;
    public ToolCaptionSample()
    {
        RequestedTheme = ElementTheme.Light;
        var root = new Grid(); Content = root;
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Margin = new(4) };
        root.Children.Add(toolbar); root.Children.Add(_site); Grid.SetRow(_site, 1);
        void Button(string text, string id, Action action) => toolbar.Children.Add(DockChrome.Button(text, "caption-sample:" + id, _theme, action));
        Button("Reset", "reset", Reset);
        Button("Caption options", "options", () => { });
        var options = (Button)toolbar.Children[^1];
        options.Click += (_, _) =>
        {
            var menu = new MenuFlyout();
            void Item(string text, string id, Action action)
            {
                var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, FontSize = 12, MinHeight = 26 }, "caption-option:" + id);
                item.Click += (_, _) => action(); menu.Items.Add(item);
            }
            Item("Toggle close button", "close", () => _site.ToolWindowsHaveCloseButtons = !_site.ToolWindowsHaveCloseButtons);
            Item("Toggle options button", "options", () => _site.ToolWindowsHaveOptionsButtons = !_site.ToolWindowsHaveOptionsButtons);
            Item("Toggle pin button", "pin", () => _site.ToolWindowsHaveToggleAutoHideButtons = !_site.ToolWindowsHaveToggleAutoHideButtons);
            Item("Double-click maximizes / restores", "maximize", () => _site.FloatingToolWindowContainerTitleBarDoubleClickMode = FloatingWindowTitleBarDoubleClickMode.ToggleMaximized);
            Item("Double-click docks", "dock", () => _site.FloatingToolWindowContainerTitleBarDoubleClickMode = FloatingWindowTitleBarDoubleClickMode.Dock);
            Item("Toggle sibling resize permission", "resize", () => Check(_manager.UpdateWindow("cap-b", w => w with { Capabilities = w.Capabilities ^ DockCapabilities.Resize })));
            Item("Use custom title delimiter", "delimiter", () => _site.FloatingWindowTitleDelimiter = " | ");
            Item("Clear application title", "no-app", () => _site.FloatingWindowTitle = null);
            Item("Rename first document", "rename", () => Check(_manager.UpdateWindow("cap-doc1", w => w with { Title = "Renamed.cs" })));
            Item("Attach Explorer to document host", "mixed", () => Check(_manager.Dock("cap-a", "cap-doc1", DockSide.Left)));
            Item("Auto-hide Explorer", "autohide", () => Check(_manager.AutoHide("cap-a", DockSide.Left)));
            menu.ShowAt(options);
        };
        AddPolicyOptions(toolbar);
        Button("Explorer", "a", () => Check(_manager.Activate("cap-a")));
        Button("Properties", "b", () => Check(_manager.Activate("cap-b")));
        Button("Document 1", "doc1", () => Check(_manager.Activate("cap-doc1")));
        Button("Document 2", "doc2", () => Check(_manager.Activate("cap-doc2")));
        Button("Undo", "undo", () => Check(_manager.Undo()));
        Button("Dark / Light", "theme", () =>
        {
            _theme = _theme.IsDark ? DockTheme.MetroLight : DockTheme.MetroDark;
            RequestedTheme = _theme.IsDark ? ElementTheme.Dark : ElementTheme.Light; _site.Theme = _theme;
            foreach (var editor in _editors.Values)
            { editor.Foreground = DockChrome.Brush(_theme.Text); editor.Background = DockChrome.Brush(_theme.EditorBackground); }
        });
        _site.ContentFactory = model =>
        {
            var editor = DockChrome.Identify(new TextBox { AcceptsReturn = true, Text = "Native content for " + model.Id,
                FontSize = 12, Padding = new(8), BorderThickness = new(0), Foreground = DockChrome.Brush(_theme.Text),
                Background = DockChrome.Brush(_theme.EditorBackground), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch }, "caption-editor:" + model.Id);
            _editors[model.Id] = editor; return editor;
        };
        _site.OperationRejected += error => _error = error;
        Reset();
    }
    private void Check(DockResult result) { if (!result.Success) _error = result.Error; }
    private void Reset()
    {
        var m = new DockManager(); _error = null; _editors.Clear();
        m.Register(new("cap-a", "Solution Explorer — CustomerManagement.Server/Services", WindowKind.Tool)).ThrowIfFailed();
        m.Register(new("cap-b", "Properties", WindowKind.Tool)).ThrowIfFailed();
        m.Register(new("cap-doc1", "Document1.cs", WindowKind.Document)).ThrowIfFailed();
        m.Register(new("cap-doc2", "Document2.cs", WindowKind.Document)).ThrowIfFailed();
        m.FloatMany(new[] { "cap-a", "cap-b" }, new(20, 40, 370, 320)).ThrowIfFailed();
        m.Dock("cap-b", "cap-a", DockSide.Bottom).ThrowIfFailed();
        m.FloatMany(new[] { "cap-doc1", "cap-doc2" }, new(440, 40, 380, 300)).ThrowIfFailed();
        m.Activate("cap-doc1").ThrowIfFailed(); m.Activate("cap-a").ThrowIfFailed();
        _toolHost = DockTree.Locate(m.Layout, "cap-a").HostId!; _documentHost = DockTree.Locate(m.Layout, "cap-doc1").HostId!;
        _savedPolicyLayout = null;
        _manager = m; _site.Manager = m; _site.Theme = _theme;
        _site.ToolWindowsHaveTitleBars = _site.ToolWindowsHaveCloseButtons = _site.ToolWindowsHaveOptionsButtons = _site.ToolWindowsHaveToggleAutoHideButtons = true;
        _site.FloatingToolWindowContainerTitleBarDoubleClickMode = FloatingWindowTitleBarDoubleClickMode.ToggleMaximized;
        _site.FloatingWindowTitle = "DockingScene"; _site.FloatingWindowTitleDelimiter = " - "; _generation++;
    }
    public void WriteDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("toolCaptions");
        var layout = _manager.Layout;
        writer.WriteNumber("generation", _generation); writer.WriteNumber("revision", layout.Revision);
        writer.WriteString("active", layout.ActiveWindowId); writer.WriteString("error", _error);
        writer.WriteString("toolHost", _toolHost); writer.WriteString("documentHost", _documentHost);
        writer.WriteString("mode", _site.FloatingToolWindowContainerTitleBarDoubleClickMode.ToString());
        writer.WriteBoolean("close", _site.ToolWindowsHaveCloseButtons); writer.WriteBoolean("options", _site.ToolWindowsHaveOptionsButtons); writer.WriteBoolean("pin", _site.ToolWindowsHaveToggleAutoHideButtons);
        writer.WriteBoolean("interaction", _site.IsInteractionActive); writer.WriteBoolean("dark", _theme.IsDark);
        writer.WriteNumber("geometryBuilds", _site.GeometryBuildCount); writer.WriteNumber("titleFormats", _site.FloatingCaptionFormatCount);
        writer.WriteStartObject("windows");
        foreach (var (id, model) in layout.Windows)
        {
            writer.WriteStartObject(id); writer.WriteString("title", model.Title);
            var location = DockTree.Locate(layout, id); writer.WriteString("placement", location.Placement.ToString()); writer.WriteString("host", location.HostId);
            writer.WriteString("group", DockTree.GroupOf(layout, id)?.Id ?? layout.AutoHideGroups.FirstOrDefault(g => g.Windows.Contains(id))?.Id);
            if (_editors.TryGetValue(id, out var editor)) writer.WriteNumber("editor", RuntimeHelpers.GetHashCode(editor));
            writer.WriteEndObject();
        }
        writer.WriteEndObject(); writer.WriteStartObject("hosts");
        foreach (var host in layout.FloatingHosts)
        {
            writer.WriteStartObject(host.Id); writer.WriteBoolean("maximized", host.IsMaximized);
            writer.WriteNumber("root", RuntimeHelpers.GetHashCode(host.Root));
            writer.WriteNumber("x", host.Bounds.X); writer.WriteNumber("y", host.Bounds.Y); writer.WriteNumber("width", host.Bounds.Width); writer.WriteNumber("height", host.Bounds.Height);
            if (_site.FloatingWindows.TryGetValue(host.Id, out var frame))
            {
                writer.WriteNumber("frame", RuntimeHelpers.GetHashCode(frame));
                // Observe actual retained label text; do not call the title resolver
                // here because the diagnostics must not warm or mutate its cache.
                var stack = new Stack<DependencyObject>(); stack.Push(frame);
                while (stack.TryPop(out var node))
                {
                    if (node is TextBlock text && AutomationProperties.GetAutomationId(text) == "window-title:" + host.Id) writer.WriteString("title", text.Text);
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
                }
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject(); writer.WriteStartObject("buttons");
        foreach (var (id, buttons) in _site.CaptionButtonIdentities)
        { writer.WriteStartArray(id); foreach (int identity in buttons) writer.WriteNumberValue(identity); writer.WriteEndArray(); }
        writer.WriteEndObject(); writer.WriteStartObject("captionWidths");
        foreach (var (id, width) in _site.ToolCaptionWidths) writer.WriteNumber(id, width);
        writer.WriteEndObject(); WritePolicyDiagnostics(writer); writer.WriteEndObject();
    }
}
