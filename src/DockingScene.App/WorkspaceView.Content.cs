using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private readonly Dictionary<string, WeakReference<TextBox>> _editors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WeakReference<FrameworkElement>> _contentRoots = new(StringComparer.Ordinal);
    private DockTheme _contentTheme = DockTheme.MetroLight;
    private bool _contentThemeSubscribed;
    private object CreateWindowContent(DockWindow window)
    {
        if (!_contentThemeSubscribed) { _contentThemeSubscribed = true; _site.VisualsUpdated += ApplyContentTheme; }
        FrameworkElement content = window.Id switch
        {
            "welcome" => Welcome(), "explorer" => Explorer(), "outline" => Outline(), "properties" => Properties(),
            "output" => Output(), "errors" => Errors(), "find" => Find(), "toolbox" => Toolbox(), _ => Editor(window)
        };
        _contentRoots[window.Id] = new(content); return content;
    }
    private void ApplyContentTheme()
    {
        if (_contentTheme == _theme) return;
        var old = _contentTheme; _contentTheme = _theme;
        string? Map(Brush? brush)
        {
            if (brush is not SolidColorBrush solid) return null;
            foreach (var pair in new[] { (old.Text, _theme.Text), (old.MutedText, _theme.MutedText), (old.EditorBackground, _theme.EditorBackground), (old.Surface, _theme.Surface), (old.Background, _theme.Background), (old.Border, _theme.Border) })
                if (solid.Color == DockChrome.Brush(pair.Item1).Color) return pair.Item2;
            return null;
        }
        foreach (var reference in _contentRoots.Values)
        {
            if (!reference.TryGetTarget(out var root)) continue;
            var stack = new Stack<DependencyObject>(); stack.Push(root);
            while (stack.TryPop(out var node))
            {
                if (node is TextBlock text && Map(text.Foreground) is { } textColor) text.Foreground = DockChrome.Brush(textColor);
                if (node is Control control)
                {
                    if (Map(control.Foreground) is { } foreground) control.Foreground = DockChrome.Brush(foreground);
                    if (Map(control.Background) is { } background) control.Background = DockChrome.Brush(background);
                }
                if (node is Border border && Map(border.Background) is { } borderColor) border.Background = DockChrome.Brush(borderColor);
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
            }
        }
    }
    private FrameworkElement Welcome()
    {
        var body = new StackPanel { Spacing = 14, Margin = new(30, 25, 30, 25), MaxWidth = 850, HorizontalAlignment = HorizontalAlignment.Left };
        var eyebrow = DockChrome.Text("DOCKING & MULTIPLE DOCUMENT INTERFACE", _theme, 11); eyebrow.Foreground = DockChrome.Brush(_theme.MutedText); body.Children.Add(eyebrow);
        var title = DockChrome.Text("A workspace that works your way.", _theme, 29); title.TextWrapping = TextWrapping.Wrap; body.Children.Add(title);
        var intro = DockChrome.Text("DockingScene brings reusable docking, tabbed documents, auto-hide tools and overlapping MDI windows to Uno Platform. Every editor is a real retained Uno control — not a screenshot or an HTML imitation.", _theme, 14); intro.TextWrapping = TextWrapping.Wrap; body.Children.Add(intro);
        body.Children.Add(new Border { Height = 1, Background = DockChrome.Brush(_theme.Border), Margin = new(0, 8, 0, 6) });
        void Section(string heading, string text)
        {
            body.Children.Add(DockChrome.Text(heading, _theme, 17)); var block = DockChrome.Text(text, _theme, 13); block.TextWrapping = TextWrapping.Wrap; block.Foreground = DockChrome.Brush(_theme.MutedText); body.Children.Add(block);
        }
        Section("Dock, split, float", "Drag a document tab or a tool-window title. The guide compass previews its destination. Drop away from a guide to float. Hold Ctrl to suppress docking, or press Escape to cancel.");
        Section("Keep your tools close", "The Toolbox lives on the left auto-hide rail. Hover or click to reveal it; drag its inner edge to resize, or pin it to keep it open. Tab context menus expose placement and document commands.");
        Section("One model. Two MDI experiences.", "Switch between tabbed and standard MDI. Standard MDI supports overlapping documents, minimize, maximize, cascade and three tiling arrangements. Changes are atomic and undoable.");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, Margin = new(0, 6, 0, 0) };
        actions.Children.Add(DockChrome.Button("New document", "welcome:new", _theme, NewDocument));
        actions.Children.Add(DockChrome.Button("Try standard MDI", "welcome:mdi", _theme, () => Arrange(DockingScene.Layout.MdiArrangement.Cascade)));
        actions.Children.Add(DockChrome.Button("Show Toolbox", "welcome:toolbox", _theme, () => _site.OpenAutoHide("toolbox"))); body.Children.Add(actions);
        var note = DockChrome.Text("Independent implementation · Actipro API and pixel parity remain incomplete.\nCtrl+Tab / F8: switcher  ·  Ctrl+F4: close  ·  Ctrl+1…9: document  ·  Right-click a tab: commands", _theme, 11);
        note.TextWrapping = TextWrapping.Wrap; note.Margin = new(0, 8, 0, 0); note.Foreground = DockChrome.Brush(_theme.MutedText); body.Children.Add(note);
        return new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private FrameworkElement Editor(DockWindow window)
    {
        string source = _documents.TryGetValue(window.Id, out var saved) ? saved : window.Id switch
        {
            "main" => "<Page\n    x:Class=\"Example.MainPage\"\n    xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\n    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n    xmlns:dock=\"using:DockingScene.Controls\">\n\n    <dock:DockSite Manager=\"{Binding Manager}\" />\n\n</Page>\n",
            "code" => "using DockingScene.Core;\nusing DockingScene.Mvvm;\n\nnamespace Example;\n\npublic sealed class DockingViewModel : IDisposable\n{\n    public DockManager Manager { get; } = new();\n    public DockWorkspaceViewModel Workspace { get; }\n\n    public DockingViewModel()\n    {\n        Workspace = new(Manager);\n        Workspace.Register(new(\"document\", \"Document.cs\",\n            WindowKind.Document)).ThrowIfFailed();\n        Workspace.Register(new(\"explorer\", \"Explorer\",\n            WindowKind.Tool)).ThrowIfFailed();\n    }\n\n    public void Dispose() => Workspace.Dispose();\n}\n",
            "readme" => "# DockingScene packages\n\nDockingScene.Core\n  Immutable trees, policies, atomic batches, preview lifecycle and undo/redo.\n\nDockingScene.Layout\n  Constrained layout, docking guides, measured tabs and MDI arrangement.\n\nDockingScene.Serialization\n  Versioned bounded JSON and atomic file storage.\n\nDockingScene.Mvvm\n  Observable window projections, commands and collection synchronization.\n\nDockingScene.Themes\n  Palettes, metrics, localization strings, SVG path parsing and vector icons.\n\nDockingScene.Controls\n  DockSite, AdvancedTabControl, WindowControl, StandardMdiHost and switcher.\n\nIndependent implementation. No Actipro source code or proprietary assets.\nNot a drop-in WPF binary replacement; native OS floating and several advanced\ncompatibility areas remain in progress. See docs/parity.md in the repository.\n",
            _ => "// Preview document\n// Type here to promote this preview into a regular document.\n\nusing DockingScene.Core;\n\nvar manager = new DockManager();\nmanager.Register(new(\"example\", \"Example\", WindowKind.Document));\n"
        };
        var editor = DockChrome.Identify(new TextBox
        {
            // Configure multiline behavior before assigning Text; otherwise the default single-line
            // coercion can truncate the initial value before the TextChanged observer is attached.
            AcceptsReturn = true, Text = source, TextWrapping = TextWrapping.NoWrap, IsReadOnly = window.IsReadOnly,
            FontFamily = new FontFamily("Consolas, monospace"), FontSize = 13, Padding = new(14, 10, 14, 10), BorderThickness = new(0),
            Background = DockChrome.Brush(_theme.EditorBackground), Foreground = DockChrome.Brush(_theme.Text), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch
        }, "editor:" + window.Id, window.Title + " editor");
        _editors[window.Id] = new(editor); _documents[window.Id] = source;
        ScrollViewer.SetHorizontalScrollBarVisibility(editor, ScrollBarVisibility.Auto); ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        string previous = source.ReplaceLineEndings("\n");
        editor.TextChanged += (_, _) =>
        {
            // Uno may deliver the initial Text assignment after the handler is attached. Initialization is not an edit.
            // WinUI/Uno uses CR internally. Canonicalize line separators without
            // suppressing actual text edits or altering the control's caret offsets.
            var text = editor.Text.ReplaceLineEndings("\n");
            if (text == previous) return;
            previous = text; _documents[window.Id] = text;
            if (_site.Manager.Layout.Windows.TryGetValue(window.Id, out var current) && !current.IsModified && !current.IsReadOnly)
                Execute(_site.Manager.UpdateWindow(window.Id, w => w with { IsModified = true }));
        };
        return editor;
    }
    private FrameworkElement Explorer()
    {
        var tree = new TreeView { SelectionMode = TreeViewSelectionMode.Single, FontSize = 12 };
        var root = new TreeViewNode { Content = "Solution 'DockingScene' (7 projects)", IsExpanded = true };
        foreach (var package in new[] { "Core", "Layout", "Serialization", "Mvvm", "Themes", "Controls", "App" })
        {
            var project = new TreeViewNode { Content = "◈  DockingScene." + package, IsExpanded = package is "Core" or "Controls" };
            foreach (var name in package switch
            {
                "Core" => new[] { "DockManager.cs", "DockTree.cs", "Model.cs", "DockLayoutValidator.cs" },
                "Controls" => new[] { "DockSite.cs", "AdvancedTabControl.cs", "StandardMdiHost.cs", "WindowControl.cs" },
                "Layout" => new[] { "DockLayoutEngine.cs", "WindowGeometry.cs", "TabStripMetrics.cs", "DockGuides.cs" },
                "Serialization" => new[] { "DockLayoutSerializer.cs" }, "Mvvm" => new[] { "DockWorkspaceViewModel.cs", "DockSourceBinding.cs" },
                "Themes" => new[] { "DockTheme.cs", "VectorPath.cs" }, _ => new[] { "App.xaml", "MainWindow.xaml" }
            }) project.Children.Add(new() { Content = "   " + name });
            root.Children.Add(project);
        }
        tree.RootNodes.Add(root);
        tree.ItemInvoked += (_, e) => { if (e.InvokedItem is TreeViewNode node && node.Children.Count == 0) Execute(_site.Manager.Open(node.Content?.ToString()?.Contains("xaml") == true ? "main" : "code")); };
        return tree;
    }
    private FrameworkElement Outline()
    {
        var list = new ListView { FontSize = 12, SelectionMode = ListViewSelectionMode.Single };
        list.ItemsSource = new[] { "▾  DockingScene.Core", "   ◇ DockManager", "      Register(window)", "      Dock(id, target, side)", "      Float(id)", "      AutoHide(id, side)", "      Transaction(name, operations)", "      OpenPreview(window)", "      Undo() / Redo()", "▾  DockingScene.Mvvm", "   ◇ DockWorkspaceViewModel", "   ◇ DockSourceBinding<T>" }; return list;
    }
    private FrameworkElement Properties()
    {
        var panel = new StackPanel { Margin = new(8), Spacing = 7 }; panel.Children.Add(DockChrome.Text("Window properties", _theme, 13));
        var choices = DockChrome.Identify(new ComboBox { ItemsSource = _site.Manager.Layout.Windows.Keys.Order().ToArray(), SelectedItem = "main", MinHeight = 27, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch }, "properties:window"); panel.Children.Add(choices);
        var title = DockChrome.Identify(new TextBox { Header = "Title", MinHeight = 28, FontSize = 12 }, "properties:title"); panel.Children.Add(title);
        var readOnly = DockChrome.Identify(new CheckBox { Content = "Read only", FontSize = 12, MinHeight = 25 }, "properties:readonly"); panel.Children.Add(readOnly);
        var floatable = DockChrome.Identify(new CheckBox { Content = "Can float", FontSize = 12, MinHeight = 25 }, "properties:float"); panel.Children.Add(floatable);
        var closable = DockChrome.Identify(new CheckBox { Content = "Can close", FontSize = 12, MinHeight = 25 }, "properties:close"); panel.Children.Add(closable);
        void Load()
        {
            if (choices.SelectedItem is string id && _site.Manager.Layout.Windows.TryGetValue(id, out var w))
            { title.Text = w.Title; readOnly.IsChecked = w.IsReadOnly; floatable.IsChecked = w.Capabilities.HasFlag(DockCapabilities.Float); closable.IsChecked = w.Capabilities.HasFlag(DockCapabilities.Close); }
        }
        choices.SelectionChanged += (_, _) => Load(); Load();
        panel.Children.Add(DockChrome.Button("Apply", "properties:apply", _theme, () =>
        {
            if (choices.SelectedItem is not string id) return;
            Execute(_site.Manager.UpdateWindow(id, w => w with
            {
                Title = title.Text, IsReadOnly = readOnly.IsChecked == true,
                Capabilities = (w.Capabilities & ~(DockCapabilities.Float | DockCapabilities.Close)) | (floatable.IsChecked == true ? DockCapabilities.Float : 0) | (closable.IsChecked == true ? DockCapabilities.Close : 0)
            }));
        }));
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private FrameworkElement Output()
    {
        _output = new TextBox { AcceptsReturn = true, Text = string.Join('\n', _events.Reverse()), IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 12, Padding = new(8, 5, 8, 5), BorderThickness = new(0), Background = DockChrome.Brush(_theme.Surface), Foreground = DockChrome.Brush(_theme.Text) };
        DockChrome.Identify(_output, "output:log"); return _output;
    }
    private FrameworkElement Errors()
    {
        var grid = new Grid { Padding = new(8) }; grid.RowDefinitions.Add(new() { Height = new(28) }); grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        grid.Children.Add(DockChrome.Text("Layout validation", _theme));
        var text = DockChrome.Text("The manager validates each transaction before committing. Rejected operations are listed in Output.", _theme); text.TextWrapping = TextWrapping.Wrap; Grid.SetRow(text, 1); grid.Children.Add(text); return grid;
    }
    private FrameworkElement Find()
    {
        var panel = new StackPanel { Margin = new(8), Spacing = 8 };
        var query = new TextBox { PlaceholderText = "Find text in loaded documents", FontSize = 12, MinHeight = 27 }; panel.Children.Add(query);
        var results = new ListView { FontSize = 12, MaxHeight = 180 }; panel.Children.Add(results);
        query.TextChanged += (_, _) => results.ItemsSource = string.IsNullOrEmpty(query.Text) ? [] : _documents.Where(pair => pair.Value.Contains(query.Text, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key + ": " + query.Text).ToArray(); return panel;
    }
    private FrameworkElement Toolbox()
    {
        var panel = new StackPanel { Margin = new(9), Spacing = 5 }; panel.Children.Add(DockChrome.Text("Reusable control examples", _theme, 14));
        foreach (string name in new[] { "DockSite", "AdvancedTabControl", "DocumentWindow", "ToolWindow", "WindowControl", "StandardMdiHost", "StandardSwitcher" })
            panel.Children.Add(DockChrome.Button(name, "toolbox:" + name, _theme, () =>
            {
                string id = "sample-" + name;
                if (_site.Manager.Layout.Windows.ContainsKey(id)) Execute(_site.Manager.Open(id));
                else { _documents[id] = "// " + name + "\n// Reusable Uno control in DockingScene.Controls\n\nvar control = new " + name + "();\n"; Execute(_site.Manager.Register(new(id, name + ".cs", WindowKind.Document))); }
            }));
        return new ScrollViewer { Content = panel };
    }
}
