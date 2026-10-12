using System.Runtime.CompilerServices;
using System.Windows.Input;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

/// <summary>Reusable external system icon, title and primary-window command bar.
/// It can live in an application menu or ribbon when a maximized frame is hidden.
/// Controls are retained, and host event subscriptions follow the loaded lifetime.</summary>
public sealed class StandardMdiControlBar : UserControl
{
    private readonly Grid _root = new();
    private readonly TextBlock _title = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(6,0,8,0) };
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private StandardMdiHost? _subscribedHost;
    private string _prefix = "mdi-primary";
    private bool _ready;
    public static readonly DependencyProperty HostProperty = DependencyProperty.Register(nameof(Host), typeof(StandardMdiHost), typeof(StandardMdiControlBar), new PropertyMetadata(null, HostChanged));
    public static readonly DependencyProperty ShowOnlyWhenMaximizedProperty = DependencyProperty.Register(nameof(ShowOnlyWhenMaximized), typeof(bool), typeof(StandardMdiControlBar), new PropertyMetadata(true, (d, _) => ((StandardMdiControlBar)d).Refresh()));
    public StandardMdiHost? Host { get => (StandardMdiHost?)GetValue(HostProperty); set => SetValue(HostProperty, value); }
    public bool ShowOnlyWhenMaximized { get => (bool)GetValue(ShowOnlyWhenMaximizedProperty); set => SetValue(ShowOnlyWhenMaximizedProperty, value); }
    public string AutomationIdPrefix
    {
        get => _prefix;
        set { ArgumentException.ThrowIfNullOrWhiteSpace(value); if (_prefix == value) return; _prefix = value; Refresh(); }
    }
    public IReadOnlyDictionary<string, int> ButtonIdentities => _buttons.ToDictionary(p => p.Key, p => RuntimeHelpers.GetHashCode(p.Value));
    public StandardMdiControlBar()
    {
        Content = _root; MaxWidth = 480;
        _root.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _root.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        _root.Children.Add(_title); Grid.SetColumn(_title,1); _root.Children.Add(actions); Grid.SetColumn(actions,2);
        var theme = DockTheme.MetroLight; var style = (Style)new TabHeaderResources()["DockTabButtonStyle"];
        var system = DockChrome.Button("Window menu", "", theme, () =>
        {
            if (Host is { } host && host.PrimaryWindow is { } primary && host.Windows.TryGetValue(primary.Id, out var frame)) frame.ShowSystemMenu(_buttons["system"]);
        }, DockGlyphs.Document);
        system.Style = style; system.Width = 23; system.Padding = new(5); _buttons.Add("system",system); _root.Children.Add(system);
        foreach (string key in new[] { "minimize", "restore", "maximize", "close" })
        {
            var button = DockChrome.Button(key, "", theme, () => { });
            button.Style = style; button.Width = 26; button.Padding = new(5); _buttons.Add(key,button); actions.Children.Add(button);
        }
        Loaded += (_, _) => { Attach(); Refresh(); };
        Unloaded += (_, _) => Detach();
        _ready = true; Refresh();
    }
    private static void HostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bar = (StandardMdiControlBar)d; bar.Detach(); if (bar.IsLoaded) bar.Attach(); bar.Refresh();
    }
    private void Attach()
    {
        if (_subscribedHost is not null || Host is not { } host) return;
        _subscribedHost = host; host.ProjectionChanged += OnProjectionChanged;
    }
    private void Detach()
    {
        if (_subscribedHost is not { } host) return;
        _subscribedHost = null; host.ProjectionChanged -= OnProjectionChanged;
    }
    private void OnProjectionChanged(object? sender, EventArgs e) => Refresh();
    private void Refresh()
    {
        if (!_ready) return;
        var host = Host; var primary = host?.PrimaryWindow; var theme = host?.PresentationTheme ?? DockTheme.MetroLight;
        Visibility = primary is not null && (!ShowOnlyWhenMaximized || primary.MdiState == MdiWindowState.Maximized) ? Visibility.Visible : Visibility.Collapsed;
        _root.Background = DockChrome.Brush(theme.Background); _title.Foreground = DockChrome.Brush(theme.Text); _title.FontSize = theme.FontSize;
        _title.Text = primary is null ? "" : primary.Title + (primary.IsModified ? " *" : "");
        DockChrome.Identify(_title, _prefix + ":title", _title.Text);
        void Set(string key, string label, string glyph, ICommand? command, bool visible = true)
        {
            var button = _buttons[key]; button.Command = command;
            button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            button.IsEnabled = key == "system" ? primary is not null : command?.CanExecute(null) == true;
            button.Height = theme.TitleHeight; button.Background = _root.Background; button.Foreground = _title.Foreground;
            DockChrome.Identify(button, _prefix + ":" + key, label); ToolTipService.SetToolTip(button,label);
            if (button.Tag as string != glyph) { button.Tag = glyph; button.Content = DockChrome.Glyph(glyph,theme.Text); }
            else if (button.Content is Microsoft.UI.Xaml.Shapes.Path icon) icon.Stroke = _title.Foreground;
        }
        Set("system", "Window menu", DockGlyphs.Document, null);
        Set("minimize", "Minimize", DockGlyphs.Minimize, host?.MinimizePrimaryWindowCommand);
        Set("restore", "Restore", DockGlyphs.Restore, host?.RestorePrimaryWindowCommand, primary?.MdiState is MdiWindowState.Minimized or MdiWindowState.Maximized);
        Set("maximize", "Maximize", DockGlyphs.Maximize, host?.MaximizePrimaryWindowCommand, primary?.MdiState != MdiWindowState.Maximized);
        Set("close", "Close", DockGlyphs.Close, host?.ClosePrimaryWindowCommand);
    }
}
