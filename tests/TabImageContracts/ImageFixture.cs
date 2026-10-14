using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace DockingScene.ImageContracts;

// Test-only native fixture. No application workspace hooks or JS model commands.
public sealed class ImageFixture : UserControl
{
    private readonly AdvancedTabControl _tabs = new() { MaxTabExtent = 280, AutomationIdPrefix = "fixture" };
    private readonly DockSite _site = new() { OpenAutoHideOnHover = false, ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Show };
    private readonly BitmapImage _red = new(new Uri("ms-appx:///Assets/Red.png"));
    private readonly BitmapImage _blue = new(new Uri("ms-appx:///Assets/Blue.png"));
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Dictionary<string, TextBox> _editors = new(StringComparer.Ordinal);
    private DockTheme _theme = DockTheme.MetroLight;
    private int _created, _sequence;
    private string? _error;
    public ImageFixture()
    {
        RequestedTheme = ElementTheme.Light;
        var root = new Grid { Padding = new(12), RowSpacing = 8 }; Content = root;
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        root.Children.Add(toolbar); root.Children.Add(_tabs); root.Children.Add(_site);
        Grid.SetRow(_tabs, 1); Grid.SetRow(_site, 2);
        void Button(string id, Action action) => toolbar.Children.Add(DockChrome.Button(id, "action:" + id, _theme, action));
        Button("Toggle", () => _tabs.HasTabImages = !_tabs.HasTabImages);
        Button("Swap", () => { if (Selected() is { } item) item.ImageSource = ReferenceEquals(item.ImageSource, _red) ? _blue : _red; });
        Button("Clear", () => { if (Selected() is { } item) item.ImageSource = null; });
        Button("Restore", () => { if (Selected() is { } item) item.ImageSource = _red; });
        Button("Strip", () => _tabs.IsTabStripVisible = !_tabs.IsTabStripVisible);
        Button("Edge", () => _tabs.Update(_tabs.Items.ToArray(), _tabs.SelectedId, _theme, (TabPlacement)(((int)_tabs.Placement + 1) % 4), true));
        Button("Theme", () =>
        {
            _theme = _theme.IsDark ? DockTheme.MetroLight : DockTheme.MetroDark;
            RequestedTheme = _theme.IsDark ? ElementTheme.Dark : ElementTheme.Light;
            _tabs.Update(_tabs.Items.ToArray(), _tabs.SelectedId, _theme, _tabs.Placement, true); _site.Theme = _theme;
            foreach (var editor in _editors.Values) Paint(editor);
        });
        Button("Many", () => Populate(2002));
        Button("Tools", () => _site.ToolWindowsHaveTabImages = !_site.ToolWindowsHaveTabImages);
        Button("Documents", () => _site.DocumentWindowsHaveTabImages = !_site.DocumentWindowsHaveTabImages);
        Button("Float", () => Check(_site.Manager.Float("host-doc", new(20, 20, 330, 200))));
        Button("Undo", () => Check(_site.Manager.Undo()));
        _site.WindowCreated += window => window.ImageSource = window.Model.Kind == WindowKind.Document ? _red : _blue;
        _site.ContentFactory = window => Editor(window.Id);
        _site.OperationRejected += message => _error = message;
        var manager = new DockManager();
        manager.Register(new("host-doc", "Hosted document", WindowKind.Document)).ThrowIfFailed();
        manager.Register(new("host-tool-a", "Tool A", WindowKind.Tool), targetId: "workspace", side: DockSide.Right).ThrowIfFailed();
        manager.Register(new("host-tool-b", "Tool B", WindowKind.Tool), targetId: "host-tool-a", side: DockSide.Center).ThrowIfFailed();
        _site.Manager = manager;
        Populate(3);
        Loaded += (_, _) => _timer.Start(); Unloaded += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) => Observe();
    }
    private void Check(DockResult result) { if (!result.Success) _error = result.Error; }
    private AdvancedTabItem? Selected() => _tabs.Items.FirstOrDefault(item => item.Id == _tabs.SelectedId);
    private void Paint(TextBox editor) { editor.Background = DockChrome.Brush(_theme.EditorBackground); editor.Foreground = DockChrome.Brush(_theme.Text); }
    private TextBox Editor(string id)
    {
        var editor = DockChrome.Identify(new TextBox { Text = "Content " + id, AcceptsReturn = true,
            BorderThickness = new(0), Padding = new(10), HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch }, "editor:" + id);
        Paint(editor); _editors[id] = editor; return editor;
    }
    private void Populate(int count)
    {
        _created = 0;
        foreach (string id in _editors.Keys.Where(id => id.StartsWith("tab-", StringComparison.Ordinal)).ToArray()) _editors.Remove(id);
        var items = new AdvancedTabItem[count];
        for (int i = 0; i < count; i++)
        {
            string id = "tab-" + i;
            items[i] = new() { Id = id, Header = "Item " + i, ImageSource = i == 2 && count == 3 ? null : _red,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
                ContentFactory = () => { _created++; return Editor(id); } };
        }
        _tabs.Update(items, "tab-0", _theme, TabPlacement.Top, true);
    }
    private void Observe()
    {
        try
        {
            UpdateLayout(); if (_site.IsVisualUpdatePending) return;
            using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject(); writer.WriteNumber("sequence", ++_sequence); writer.WriteString("error", _error);
            writer.WriteBoolean("enabled", _tabs.HasTabImages); writer.WriteBoolean("strip", _tabs.IsTabStripVisible);
            writer.WriteBoolean("documents", _site.DocumentWindowsHaveTabImages); writer.WriteBoolean("tools", _site.ToolWindowsHaveTabImages);
            writer.WriteBoolean("dark", _theme.IsDark); writer.WriteString("placement", _tabs.Placement.ToString());
            writer.WriteNumber("created", _created); writer.WriteNumber("count", _tabs.Items.Count);
            writer.WriteNumber("measured", _tabs.TextMeasurementCount); writer.WriteNumber("headers", _tabs.HeaderCreationCount);
            writer.WriteNumber("layouts", _tabs.HeaderLayoutCount); writer.WriteNumber("realized", _tabs.RealizedHeaderCount);
            writer.WriteNumber("extent", _tabs.HeaderLayout?.Lanes.Sum(l => l.Metrics.Length) ?? 0);
            writer.WriteString("selected", _tabs.SelectedId);
            writer.WriteNumber("source", Selected()?.ImageSource is { } source ? RuntimeHelpers.GetHashCode(source) : 0);
            writer.WriteStartObject("contents");
            foreach (var (id, editor) in _editors) writer.WriteNumber(id, RuntimeHelpers.GetHashCode(editor));
            writer.WriteEndObject(); writer.WriteStartObject("bounds");
            foreach (var (id, r) in DockChrome.AutomationBounds(this))
            { writer.WriteStartObject(id); writer.WriteNumber("x", r.X); writer.WriteNumber("y", r.Y); writer.WriteNumber("width", r.Width); writer.WriteNumber("height", r.Height); writer.WriteEndObject(); }
            writer.WriteEndObject(); writer.WriteStartArray("images");
            var stack = new Stack<DependencyObject>(); stack.Push(this);
            while (stack.TryPop(out var node))
            {
                if (node is Image image && AutomationProperties.GetAutomationId(image) is { } id && id.StartsWith("tab-image:", StringComparison.Ordinal))
                {
                    var point = image.TransformToVisual(this).TransformPoint(new Point());
                    writer.WriteStartObject(); writer.WriteString("id", id); writer.WriteNumber("identity", RuntimeHelpers.GetHashCode(image));
                    writer.WriteNumber("source", image.Source is null ? 0 : RuntimeHelpers.GetHashCode(image.Source));
                    writer.WriteBoolean("hitTest", image.IsHitTestVisible); writer.WriteNumber("pixels", (image.Source as BitmapSource)?.PixelWidth ?? 0);
                    writer.WriteNumber("x", point.X); writer.WriteNumber("y", point.Y); writer.WriteNumber("width", image.ActualWidth); writer.WriteNumber("height", image.ActualHeight);
                    writer.WriteEndObject();
                }
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
            }
            writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
            Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.imageContracts=" + System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (Exception error) { App.Report(error); }
    }
}
