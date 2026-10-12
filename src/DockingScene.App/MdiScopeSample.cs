using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

/// <summary>A real standalone two-host consumer sharing one manager. Commands are
/// scoped to the corresponding host; diagnostics never issue model operations.</summary>
public sealed class MdiScopeSample : UserControl
{
    private readonly DockManager _manager = new();
    private readonly StandardMdiHost _left = new(), _right = new();
    private readonly Dictionary<string, AdvancedTabItem> _content = new(StringComparer.Ordinal);
    private bool _leftActive = true;
    private DockResult _lastResult;
    private readonly MdiContextResources _templates = new();
    private readonly DockContextPresenter _probe = new();
    private DocumentWindow? _releasedDocument;
    public MdiScopeSample()
    {
        var grid = new Grid(); Content = grid;
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new(4) };
        grid.Children.Add(toolbar); Grid.SetColumnSpan(toolbar, 2);
        grid.Children.Add(_left); grid.Children.Add(_right); Grid.SetRow(_left, 1); Grid.SetRow(_right, 1); Grid.SetColumn(_right, 1);
        void Button(string text, string id, Action action) => toolbar.Children.Add(DockChrome.Button(text, "scope:" + id, DockTheme.MetroLight, action));
        Button("Cascade left", "left", () => _lastResult = _left.Cascade());
        Button("Tile right", "right", () => _lastResult = _right.TileHorizontally());
        Button("Release left", "release", () => { _leftActive = false; _left.ReleaseContent(); });
        Button("Undo", "undo", () => _lastResult = _manager.Undo());
        Button("Replace context", "reentrant-context", () =>
        {
            Action replace = () => _probe.Update("Replacement context", null);
            _probe.Update(replace, (DataTemplate)_templates["ReentrantCaption"]);
        });
        Button("Release in context", "reentrant-release", () =>
        {
            _leftActive = true;
            _releasedDocument = new DocumentWindow { Id = "a", Header = "A",
                Content = new TextBox { Text = "Must stay detached" },
                StandardMdiTitleBarContextContentTemplate = (DataTemplate)_templates["ReentrantCaption"],
                DataContext = (Action)(() => { _leftActive = false; _left.ReleaseContent(); }) };
            _content["a"] = _releasedDocument;
            _left.Update(_manager, new[] { "a", "b" }, id => _content[id], DockTheme.MetroLight);
        });
        toolbar.Children.Add(_probe);
        _manager.RegisterMany(new[] { "a", "b", "c", "d" }.Select(id => new DockWindow(id, "Document " + id, WindowKind.Document)
            { MdiBounds = new(id is "a" or "c" ? 8 : 35, id is "a" or "c" ? 8 : 40, 190, 130) })).ThrowIfFailed();
        _manager.SetMdiMode(MdiMode.Standard).ThrowIfFailed();
        foreach (string id in new[] { "a", "b", "c", "d" })
            _content[id] = new() { Id = id, Header = id, Content = new TextBox { AcceptsReturn = true, Text = "Independent editor " + id } };
        _manager.Changed += (_, _) => Project();
        Loaded += (_, _) => Project();
        Unloaded += (_, _) => { _left.ReleaseContent(); _right.ReleaseContent(); };
    }
    private void Project()
    {
        if (_leftActive) _left.Update(_manager, new[] { "a", "b" }, id => _content[id], DockTheme.MetroLight);
        _right.Update(_manager, new[] { "c", "d" }, id => _content[id], DockTheme.MetroLight);
    }
    public void WriteDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("scopedMdi"); writer.WriteNumber("revision", _manager.Layout.Revision);
        writer.WriteNumber("obsoleteContextLoads", _templates.ProbeLoadedCount);
        writer.WriteString("contextProbe", _probe.PresentedContent as string);
        writer.WriteBoolean("releasedDocumentDetached", _releasedDocument is not null &&
            Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(_releasedDocument) is null);
        writer.WriteBoolean("success", _lastResult.Success); writer.WriteString("error", _lastResult.Error);
        writer.WriteStartObject("windows");
        foreach (var (id, window) in _manager.Layout.Windows)
        {
            writer.WriteStartObject(id);
            writer.WriteNumber("x", window.MdiBounds.X); writer.WriteNumber("y", window.MdiBounds.Y);
            writer.WriteNumber("width", window.MdiBounds.Width); writer.WriteNumber("height", window.MdiBounds.Height);
            writer.WriteEndObject();
        }
        writer.WriteEndObject(); writer.WriteEndObject();
    }
}
