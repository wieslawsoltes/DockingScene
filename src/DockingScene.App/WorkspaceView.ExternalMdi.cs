using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private readonly StackPanel _externalMdi = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly TextBlock _externalMdiTitle = new() { FontSize = 12, MaxWidth = 230, Margin = new(8,0,8,0),
        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<string, Button> _externalMdiButtons = new(StringComparer.Ordinal);
    private StandardMdiHost? _externalMdiHost;
    private DockTheme? _externalMdiTheme;
    private void EnableExternalMdiChrome()
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _root.Children.Remove(_menu); row.Children.Add(_menu); row.Children.Add(_externalMdi); Grid.SetColumn(_externalMdi, 1);
        _root.Children.Add(row); Grid.SetRow(row, 0);
        DockChrome.Identify(_externalMdi, "mdi-external:bar"); DockChrome.Identify(_externalMdiTitle, "mdi-external:title");
        _externalMdi.Children.Add(_externalMdiTitle);
        var style = (Style)new TabHeaderResources()["DockTabButtonStyle"];
        foreach (string name in new[] { "minimize", "restore", "close" })
        {
            var button = DockChrome.Button(name, "mdi-external:" + name, _theme, () => { });
            button.Style = style; button.Height = 25; button.Width = 26; button.Padding = new(5);
            _externalMdiButtons.Add(name, button); _externalMdi.Children.Add(button);
        }
        _site.VisualsUpdated += UpdateExternalMdiChrome;
        UpdateExternalMdiChrome();
    }
    private void UpdateExternalMdiChrome()
    {
        var host = _site.PrimaryMdiHost;
        if (!ReferenceEquals(host, _externalMdiHost))
        {
            if (_externalMdiHost is not null) _externalMdiHost.PrimaryWindowChanged -= UpdateExternalMdiChrome;
            _externalMdiHost = host;
            if (host is not null) host.PrimaryWindowChanged += UpdateExternalMdiChrome;
            _externalMdiButtons["minimize"].Command = host?.MinimizePrimaryWindowCommand;
            _externalMdiButtons["restore"].Command = host?.RestorePrimaryWindowCommand;
            _externalMdiButtons["close"].Command = host?.ClosePrimaryWindowCommand;
        }
        _externalMdi.Visibility = _site.Manager.Layout.MdiMode == MdiMode.Standard && host?.IsExternalMaximizedUIRequired == true ? Visibility.Visible : Visibility.Collapsed;
        _externalMdiTitle.Text = host?.PrimaryWindow?.Title ?? "";
        if (_externalMdiTheme == _theme) return;
        _externalMdiTheme = _theme; _externalMdiTitle.Foreground = DockChrome.Brush(_theme.Text);
        foreach (var (name, button) in _externalMdiButtons)
        {
            button.Background = DockChrome.Brush(_theme.Background); button.Foreground = DockChrome.Brush(_theme.Text);
            if (button.Content is Microsoft.UI.Xaml.Shapes.Path icon) icon.Stroke = button.Foreground;
            else button.Content = DockChrome.Glyph(name switch { "minimize" => DockGlyphs.Minimize, "restore" => DockGlyphs.Restore, _ => DockGlyphs.Close }, _theme.Text);
        }
    }
    private void AddMdiPresentationCommands(MenuFlyout menu)
    {
        menu.Items.Add(new MenuFlyoutSeparator());
        var toggle = DockChrome.Identify(new MenuFlyoutItem { Text = "Toggle maximized window frames", FontSize = 12, MinHeight = 26 }, "mdi-option:frames");
        toggle.Click += (_, _) => _site.AreStandardMdiMaximizedWindowFramesVisible = !_site.AreStandardMdiMaximizedWindowFramesVisible;
        menu.Items.Add(toggle);
        if (_site.PrimaryMdiHost is not { } host || _site.Manager.Layout.MdiMode != MdiMode.Standard) return;
        foreach (var (name, command) in new (string, ICommand)[] { ("minimize", host.MinimizePrimaryWindowCommand),
            ("maximize", host.MaximizePrimaryWindowCommand), ("restore", host.RestorePrimaryWindowCommand), ("close", host.ClosePrimaryWindowCommand) })
            menu.Items.Add(DockChrome.Identify(new MenuFlyoutItem { Text = name + " primary document", Command = command, FontSize = 12, MinHeight = 26 }, "mdi-primary:" + name));
    }
    private void WriteMdiPresentationDiagnostics(Utf8JsonWriter writer)
    {
        var host = _site.PrimaryMdiHost;
        writer.WriteString("mdiPrimaryId", host?.PrimaryWindow?.Id);
        writer.WriteBoolean("mdiFramesVisible", _site.AreStandardMdiMaximizedWindowFramesVisible);
        writer.WriteBoolean("mdiExternalVisible", _externalMdi.Visibility == Visibility.Visible);
        writer.WriteNumber("minimizedLayoutBuilds", host?.MinimizedLayoutBuildCount ?? 0);
        writer.WriteNumber("minimizedLayoutReuses", host?.MinimizedLayoutReuseCount ?? 0);
        writer.WriteStartObject("mdiExternalButtons");
        foreach (var (name, button) in _externalMdiButtons)
        {
            writer.WriteStartObject(name); writer.WriteNumber("instance", RuntimeHelpers.GetHashCode(button));
            writer.WriteBoolean("enabled", button.IsEnabled); writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}
