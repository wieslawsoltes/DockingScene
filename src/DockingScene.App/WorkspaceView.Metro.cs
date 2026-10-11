using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private readonly Grid _metroStatus = new();
    private readonly TextBlock _statusDetails = new() { FontSize = 12, Margin = new(8, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private Button? _appearanceMenu;
    private Button? _activateMenu;
    private ComboBox? _mdiSelector;
    private bool _metroEnabled;
    private DockTheme? _metroTheme;
    private string[] _inspectorWindowIds = [];
    private string? _inspectorWindowId;
    public void EnableMetroChrome()
    {
        if (_metroEnabled) return;
        _metroEnabled = true;
        _site.AreDocumentTabEmbeddedButtonsAlwaysVisible = false;
        _site.HasToolTabCloseButtons = false;
        _site.HasDocumentTabStripHighlight = true;
        _root.Children.Remove(_status);
        _metroStatus.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        _metroStatus.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _metroStatus.Children.Add(_status); _metroStatus.Children.Add(_statusDetails); Grid.SetColumn(_statusDetails, 1);
        _root.Children.Add(_metroStatus); Grid.SetRow(_metroStatus, 3);
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        DockChrome.Identify(_metroStatus, "status:metro");
        _site.VisualsUpdated += UpdateMetroChrome;
        UpdateMetroChrome();
    }
    private void UpdateMetroChrome()
    {
        if (_metroTheme != _theme)
        {
            _metroTheme = _theme;
            _metroStatus.Background = DockChrome.Brush(_theme.Accent);
            _statusDetails.Foreground = DockChrome.Brush(_theme.AccentText);
        }
        _status.Foreground = _statusDetails.Foreground;
        _status.Text = _lastMessage;
        var layout = _site.Manager.Layout;
        if (_mdiSelector?.Parent != _toolbar)
            _mdiSelector = _toolbar.Children.OfType<ComboBox>().FirstOrDefault(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(control) == "toolbar:mdi");
        if (_mdiSelector is not null && _mdiSelector.SelectedIndex != (int)layout.MdiMode)
            _mdiSelector.SelectedIndex = (int)layout.MdiMode;
        _statusDetails.Text = layout.Windows.Values.Count(w => w.Kind == WindowKind.Document) + " documents    |    " +
            (layout.MdiMode == MdiMode.Tabbed ? "Tabbed MDI" : "Standard MDI");
        if (_appearanceMenu?.Parent != _toolbar)
        {
            Button? button = null;
            button = DockChrome.Button("Appearance", "toolbar:Appearance", _theme, () =>
            {
                var menu = new MenuFlyout();
                void Add(string label, string id, Action action)
                {
                    var item = DockChrome.Identify(new MenuFlyoutItem { Text = label, FontSize = 12, MinHeight = 25 }, "appearance:" + id);
                    item.Click += (_, _) => action(); menu.Items.Add(item);
                }
                Add("Contextual document buttons", "contextual", () => _site.AreDocumentTabEmbeddedButtonsAlwaysVisible = false);
                Add("Always show document buttons", "always", () => _site.AreDocumentTabEmbeddedButtonsAlwaysVisible = true);
                Add("Show tool tab close buttons", "tool-close", () => _site.HasToolTabCloseButtons = true);
                Add("Hide tool tab close buttons", "tool-no-close", () => _site.HasToolTabCloseButtons = false);
                Add("Toggle document strip highlight", "highlight", () => _site.HasDocumentTabStripHighlight = !_site.HasDocumentTabStripHighlight);
                menu.ShowAt(button!);
            });
            _appearanceMenu = button; _toolbar.Children.Add(button);
        }
        if (_activateMenu?.Parent != _menu)
        {
            Button? button = null;
            button = DockChrome.Button("Activate", "menubar:Activate", _theme, () =>
            {
                var menu = new MenuFlyout();
                var current = _site.Manager.Layout;
                foreach (var id in current.ActivationHistory.Concat(current.Windows.Keys).Distinct(StringComparer.Ordinal))
                {
                    if (DockTree.Locate(current, id).Placement == WindowPlacement.Hidden) continue;
                    string key = id;
                    var item = DockChrome.Identify(new MenuFlyoutItem { Text = current.Windows[id].Title, MinHeight = 25, FontSize = 12 }, "activate:" + id);
                    item.Click += (_, _) => Execute(_site.Manager.Activate(key)); menu.Items.Add(item);
                }
                menu.ShowAt(button!);
            });
            button.Height = 27; button.Padding = new(10, 2, 10, 2);
            _activateMenu = button; _menu.Children.Insert(Math.Min(4, _menu.Children.Count), button);
        }
    }
    private void WriteMetroDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteString("mdiModeSelection", _mdiSelector?.SelectedItem as string);
        writer.WriteString("inspectorWindowId", _inspectorWindowId);
        writer.WriteStartArray("inspectorWindowIds"); foreach (string id in _inspectorWindowIds) writer.WriteStringValue(id); writer.WriteEndArray();
        writer.WriteBoolean("documentButtonsAlwaysVisible", _site.AreDocumentTabEmbeddedButtonsAlwaysVisible);
        writer.WriteBoolean("toolTabCloseButtons", _site.HasToolTabCloseButtons);
        writer.WriteBoolean("documentStripHighlight", _site.HasDocumentTabStripHighlight);
        writer.WriteStartObject("captionButtonIdentities");
        foreach (var (id, buttons) in _site.CaptionButtonIdentities)
        { writer.WriteStartArray(id); foreach (int instance in buttons) writer.WriteNumberValue(instance); writer.WriteEndArray(); }
        writer.WriteEndObject();
    }
}
