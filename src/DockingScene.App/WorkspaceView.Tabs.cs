using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private AdvancedTabControl? _tabPlayground;
    private Button? _tabCommandsAnchor;
    private bool _tabDemoEnabled;
    private bool _vetoPlaygroundClose = true;
    private string? _lastPlaygroundClosingId;
    private int _playgroundClosingCount;

    public void EnableTabDemo()
    {
        if (_tabDemoEnabled) return;
        _tabDemoEnabled = true;
        var factory = _site.ContentFactory;
        _site.ContentFactory = window =>
        {
            if (window.Id == "content-lifetime") return OwnershipSample();
            if (window.Id != "tab-playground") return factory?.Invoke(window);
            var content = TabPlayground(); _contentRoots[window.Id] = new(content); return content;
        };
        _site.VisualsUpdated += () =>
        {
            if (_tabCommandsAnchor?.Parent != _toolbar) AddTabCommands();
            if (!_site.Manager.Layout.Windows.ContainsKey("tab-playground")) _tabPlayground = null;
            else if (_tabPlayground is { } tabs && tabs.Theme != _theme) tabs.Update(tabs.Items.ToArray(), tabs.SelectedId, _theme, tabs.Placement, true);
        };
        AddTabCommands();
    }
    private void AddTabCommands()
    {
        Button? button = null;
        button = DockChrome.Button("Tab options", "toolbar:TabOptions", _theme, () =>
        {
            var menu = new MenuFlyout();
            void Item(string title, Action action)
            {
                var item = DockChrome.Identify(new MenuFlyoutItem { Text = title, FontSize = 12, MinHeight = 25 }, "tab-options:" + title);
                item.Click += (_, _) => action(); menu.Items.Add(item);
            }
            Item("Open standalone tab playground", OpenTabPlayground);
            foreach (var behavior in Enum.GetValues<TabOverflowBehavior>())
                Item("Document overflow: " + behavior, () => _site.DocumentTabOverflowBehavior = behavior);
            Item("Show single tool tabs", () => _site.ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Show);
            Item("Hide single tool tabs", () => _site.ToolWindowsSingleTabLayoutBehavior = SingleTabLayoutBehavior.Hide);
            Item("Show document pin buttons", () => _site.HasDocumentTabPinButtons = true);
            Item("Hide document pin buttons", () => _site.HasDocumentTabPinButtons = false);
            menu.ShowAt(button!);
        });
        _tabCommandsAnchor = button; _toolbar.Children.Add(button);
    }
    private void OpenTabPlayground()
    {
        if (_site.Manager.Layout.Windows.ContainsKey("tab-playground")) Execute(_site.Manager.Open("tab-playground"));
        else Execute(_site.Manager.Register(new("tab-playground", "Tab control playground", WindowKind.Document)));
    }
    private FrameworkElement TabPlayground()
    {
        var root = new Grid { Padding = new(10), RowSpacing = 8 };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        root.Children.Add(modes); root.Children.Add(options); Grid.SetRow(options, 1);
        var tabs = DockChrome.Identify(new AdvancedTabControl { AutomationIdPrefix = "playground", HasTabPinButtons = true }, "play:tabs");
        _tabPlayground = tabs; _vetoPlaygroundClose = true; _lastPlaygroundClosingId = null; _playgroundClosingCount = 0;
        var performance = PerformanceActions(tabs); root.Children.Add(performance); Grid.SetRow(performance, 2);
        Grid.SetRow(tabs, 3); root.Children.Add(tabs);
        var items = new List<AdvancedTabItem>();
        void Add(string id, string header, bool pinned = false, bool preview = false)
        {
            var item = new AdvancedTabItem { Id = id, Header = header, IsPinned = pinned, IsPreview = preview, CanClose = !pinned,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            item.ContentFactory = () => DockChrome.Identify(new TextBox
            {
                AcceptsReturn = true, Text = header + "\n\nA lazily created, retained Uno editor.\nChange overflow modes, pin tabs, and hide the strip without recreating this editor.",
                TextWrapping = TextWrapping.Wrap, BorderThickness = new(0), FontSize = 14, Padding = new(16)
            }, "play-editor:" + id);
            items.Add(item);
        }
        Add("play-pinned", "Pinned document", pinned: true);
        for (int i = 0; i < 18; i++) Add("play-" + i, "Document " + (i + 1) + " — reusable tabs");
        Add("play-preview", "Preview document", preview: true);
        tabs.Update(items, "play-0", _theme, TabPlacement.Top, true);
        tabs.TabClosing += (_, e) =>
        {
            _lastPlaygroundClosingId = e.Item.Id; _playgroundClosingCount++;
            e.Cancel = _vetoPlaygroundClose && e.Item.Id == "play-0";
        };
        tabs.MenuOpening += (_, e) =>
        {
            e.Menu.Items.Add(new MenuFlyoutSeparator());
            var custom = DockChrome.Identify(new MenuFlyoutItem { Text = "Custom action: select preview" }, "play:custom-menu");
            custom.Click += (_, _) => tabs.Select("play-preview", true); e.Menu.Items.Add(custom);
        };
        foreach (var mode in Enum.GetValues<TabOverflowBehavior>())
            modes.Children.Add(DockChrome.Button(mode.ToString(), "play:mode:" + mode, _theme, () => tabs.OverflowBehavior = mode));
        void Option(string title, string id, Action action) => options.Children.Add(DockChrome.Button(title, "play:" + id, _theme, action));
        Option("Compact", "compact", () => { tabs.MaxTabExtent = 300; tabs.MinTabExtent = 24; });
        Option("Fixed 100", "fixed", () => { tabs.MaxTabExtent = 300; tabs.MinTabExtent = 100; tabs.MaxTabExtent = 100; });
        Option("Measured", "measured", () => { tabs.MaxTabExtent = 300; tabs.MinTabExtent = 60; });
        Option("Toggle strip", "strip", () => tabs.IsTabStripVisible = !tabs.IsTabStripVisible);
        Option("Toggle close veto", "veto", () => _vetoPlaygroundClose = !_vetoPlaygroundClose);
        foreach (var placement in Enum.GetValues<TabPlacement>())
            Option(placement.ToString(), "placement:" + placement, () => tabs.Update(tabs.Items.ToArray(), tabs.SelectedId, _theme, placement, true));
        return root;
    }
}
