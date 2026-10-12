using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private FrameworkElement Welcome()
    {
        var body = new StackPanel { Spacing = 10, Margin = new(18, 15, 18, 15), HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 900 };
        body.Children.Add(DockChrome.Text("Features Demo", _theme, 25));
        var subtitle = DockChrome.Text("Docking & MDI for Uno Platform", _theme, 21);
        subtitle.Foreground = DockChrome.Brush(_theme.MutedText); body.Children.Add(subtitle);
        void Paragraph(string text)
        {
            var block = DockChrome.Text(text, _theme, 13); block.TextWrapping = TextWrapping.Wrap; body.Children.Add(block);
        }
        Paragraph("Explore a document workspace with dockable tools, pinned and preview tabs, auto-hide rails, and tabbed or overlapping documents. All content is hosted by reusable Uno controls.");
        body.Children.Add(new Border { Height = 1, Background = DockChrome.Brush(_theme.Border), Margin = new(0, 5, 0, 5) });
        void Feature(string title, string description)
        {
            var heading = DockChrome.Text(title, _theme, 14); heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; body.Children.Add(heading);
            Paragraph(description);
        }
        Feature("Docking windows", "Drag a document tab or a tool caption's dotted grip. Dock guides preview the destination. Hold Ctrl to float; Escape cancels the gesture.");
        Feature("Tabbed and standard MDI", "Arrange documents in tab groups or overlapping windows. The Window menu provides cascade and tiling commands. Every editor retains its content when moved.");
        Feature("Auto-hide and window activation", "Reveal the Toolbox from the left rail. Pin it to keep it visible, or resize its popup from the inner edge. Use Activate or the F8 switcher to navigate open windows.");
        Feature("Reusable control samples", "Tab options opens the standalone tab playground, including overflow policies, virtualized headers, drag edge scrolling, content lifetime and large-tab workloads.");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new(0, 6, 0, 0) };
        actions.Children.Add(DockChrome.Button("New document", "welcome:new", _theme, NewDocument));
        actions.Children.Add(DockChrome.Button("Standard MDI", "welcome:mdi", _theme, () => Arrange(DockingScene.Layout.MdiArrangement.Cascade)));
        actions.Children.Add(DockChrome.Button("Show Toolbox", "welcome:toolbox", _theme, () => _site.OpenAutoHide("toolbox")));
        body.Children.Add(actions);
        var note = DockChrome.Text("Independent implementation. Full Actipro API, native-window and pixel parity remain incomplete.", _theme, 11);
        note.TextWrapping = TextWrapping.Wrap; note.Foreground = DockChrome.Brush(_theme.MutedText); body.Children.Add(note);
        return new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private FrameworkElement Properties()
    {
        var owner = _site.Manager;
        var panel = new Grid { Margin = new(6), RowSpacing = 1, ColumnSpacing = 6 };
        panel.ColumnDefinitions.Add(new() { Width = new(86) }); panel.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        int row = 0;
        void Add(string? label, FrameworkElement field, double height)
        {
            panel.RowDefinitions.Add(new() { Height = new(height) }); Grid.SetRow(field, row);
            if (label is null) Grid.SetColumnSpan(field, 2);
            else { var text = DockChrome.Text(label, _theme); Grid.SetRow(text, row); panel.Children.Add(text); Grid.SetColumn(field, 1); }
            panel.Children.Add(field); row++;
        }
        var choices = DockChrome.Identify(new DockComboBox { MinHeight = 25, FontSize = 12, Padding = new(5, 0, 22, 0), HorizontalAlignment = HorizontalAlignment.Stretch }, "properties:window");
        Add(null, choices, 26);
        var category = new Border { Background = DockChrome.Brush(_theme.Background), Child = DockChrome.Text("  General", _theme) }; Add(null, category, 20);
        var title = DockChrome.Identify(new TextBox { MinHeight = 24, FontSize = 12, Padding = new(5, 1, 5, 1) }, "properties:title"); Add("Title", title, 25);
        CheckBox Flag(string id, string label)
        {
            var check = DockChrome.Identify(new CheckBox { MinHeight = 22, Padding = new(0) }, id, label); Add(label, check, 22); return check;
        }
        var readOnly = Flag("properties:readonly", "Read only"); var floatable = Flag("properties:float", "Can float"); var closable = Flag("properties:close", "Can close");
        var apply = DockChrome.Button("Apply", "properties:apply", _theme, () =>
        {
            if (!ReferenceEquals(owner, _site.Manager) || choices.SelectedItem is not string id) return;
            Execute(owner.UpdateWindow(id, w => w with
            {
                Title = title.Text, IsReadOnly = readOnly.IsChecked == true,
                Capabilities = (w.Capabilities & ~(DockCapabilities.Float | DockCapabilities.Close)) |
                    (floatable.IsChecked == true ? DockCapabilities.Float : 0) | (closable.IsChecked == true ? DockCapabilities.Close : 0)
            }));
        });
        apply.HorizontalAlignment = HorizontalAlignment.Right; Add(null, apply, 24);
        bool reconcilingChoices = false;
        DockWindow? shown = null;
        object? previousWindows = null; string[] previousIds = [];
        void Load()
        {
            if (choices.SelectedItem is not string id || !owner.Layout.Windows.TryGetValue(id, out var window) || shown == window) return;
            _inspectorWindowId = id; shown = window; title.Text = window.Title; readOnly.IsChecked = window.IsReadOnly;
            floatable.IsChecked = window.Capabilities.HasFlag(DockCapabilities.Float); closable.IsChecked = window.Capabilities.HasFlag(DockCapabilities.Close);
        }
        void Refresh()
        {
            if (!ReferenceEquals(owner, _site.Manager)) return;
            var windows = owner.Layout.Windows;
            if (!ReferenceEquals(previousWindows, windows))
            {
                previousWindows = windows; var ids = windows.Keys.Order(StringComparer.Ordinal).ToArray();
                if (!ids.SequenceEqual(previousIds))
                {
                    string selected = choices.SelectedItem as string ?? "main";
                    _inspectorWindowIds = ids; previousIds = ids;
                    // ItemsSource changes emit transient SelectionChanged events.
                    // Preserve a same-window draft instead of reloading its fields.
                    reconcilingChoices = true;
                    try { choices.ItemsSource = ids; choices.SelectedItem = windows.ContainsKey(selected) ? selected : ids.FirstOrDefault(); }
                    finally { reconcilingChoices = false; }
                    if (choices.SelectedItem as string != selected) shown = null;
                }
            }
            Load();
        }
        choices.SelectionChanged += (_, _) => { if (reconcilingChoices) return; shown = null; Load(); };
        panel.Loaded += (_, _) => { _site.VisualsUpdated += Refresh; Refresh(); };
        panel.Unloaded += (_, _) => _site.VisualsUpdated -= Refresh;
        Refresh();
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
