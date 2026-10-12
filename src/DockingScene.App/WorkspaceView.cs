using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Mvvm;
using DockingScene.Serialization;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace DockingScene.App;

public sealed partial class WorkspaceView : UserControl
{
    private readonly Grid _root = new();
    private readonly StackPanel _menu = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _toolbar = new() { Orientation = Orientation.Horizontal, Spacing = 3, Margin = new(5, 0, 0, 0) };
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 8, 0), FontSize = 12 };
    private readonly DockSite _site = new();
    private readonly DispatcherTimer _diagnostics = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Queue<string> _events = new();
    private TextBox? _output;
    private DockWorkspaceViewModel? _models;
    private DockTheme _theme = DockTheme.MetroLight;
    private DockLayout? _saved;
    private int _nextDocument = 1;
    private long _workspaceGeneration;
    private string _lastMessage = "Ready";
    private bool _modified;
    private readonly Dictionary<string, string> _documents = new(StringComparer.Ordinal);
    public WorkspaceView()
    {
        Content = _root;
        _root.RowDefinitions.Add(new() { Height = new(27) });
        _root.RowDefinitions.Add(new() { Height = new(30) });
        _root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new() { Height = new(23) });
        _root.Children.Add(_menu); _root.Children.Add(_toolbar); _root.Children.Add(_site); _root.Children.Add(_status);
        Grid.SetRow(_toolbar, 1); Grid.SetRow(_site, 2); Grid.SetRow(_status, 3);
        DockChrome.Identify(_site, "dock-site", "Docking workspace");
        _site.ContentFactory = window =>
        {
            var content = CreateWindowContent(window);
            if (content is TextBox editor && window.Kind == WindowKind.Document && _models?.Find(window.Id) is { } model)
                editor.SetBinding(TextBox.IsReadOnlyProperty, new Binding { Source = model, Path = new PropertyPath(nameof(DockWindowViewModel.IsReadOnly)), Mode = BindingMode.OneWay });
            return content;
        };
        _site.NewDocumentRequested += NewDocument;
        _site.OperationRejected += message => Log(message);
        _site.LayoutChanged += (_, e) => { _modified = true; Log(e.Command + " · revision " + e.After.Revision); };
        Reset();
        _diagnostics.Tick += (_, _) => PublishDiagnostics();
        Loaded += (_, _) => { _diagnostics.Start(); PublishDiagnostics(); };
        Unloaded += (_, _) => _diagnostics.Stop();
    }
    private void Log(string message)
    {
        _lastMessage = message; _events.Enqueue(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
        while (_events.Count > 150) _events.Dequeue();
        if (_output is not null) _output.Text = string.Join('\n', _events.Reverse());
        _status.Text = message + "     |     " + _theme.Name + "     |     Uno · Skia renderer     |     F8: window switcher";
    }
    private void Execute(DockResult result) { if (!result.Success) Log(result.Error ?? "Operation rejected"); }
    private void Reset()
    {
        _output = null;
        var manager = new DockManager();
        manager.Register(new("welcome", "Welcome", WindowKind.Document) { Description = "DockingScene feature tour", DisplayMode = TabDisplayMode.Pinned }).ThrowIfFailed();
        manager.Register(new("main", "MainWindow.xaml", WindowKind.Document) { Description = "Editable XAML document", ContentKey = "xaml" }).ThrowIfFailed();
        manager.Register(new("code", "DockingViewModel.cs", WindowKind.Document) { Description = "Editable C# document", ContentKey = "csharp" }).ThrowIfFailed();
        manager.Register(new("readme", "README.md", WindowKind.Document) { Description = "Read-only package overview", IsReadOnly = true }).ThrowIfFailed();
        manager.Register(new("preview", "Preview.cs", WindowKind.Document) { DisplayMode = TabDisplayMode.Preview, Description = "Editing this preview promotes it to a normal tab" }).ThrowIfFailed();
        manager.Register(new("explorer", "Solution Explorer", WindowKind.Tool) { MinimumSize = new(150, 80), Description = "Browse the reusable packages" }, targetId: "workspace", side: DockSide.Right).ThrowIfFailed();
        manager.Register(new("outline", "Class View", WindowKind.Tool), targetId: "explorer").ThrowIfFailed();
        manager.Register(new("properties", "Properties", WindowKind.Tool) { MinimumSize = new(150, 100), Description = "Edit window metadata and behavior" }, targetId: "explorer", side: DockSide.Bottom).ThrowIfFailed();
        manager.Register(new("output", "Output", WindowKind.Tool) { MinimumSize = new(100, 60) }, targetId: "workspace", side: DockSide.Bottom).ThrowIfFailed();
        manager.Register(new("errors", "Error List", WindowKind.Tool), targetId: "output").ThrowIfFailed();
        manager.Register(new("find", "Find Results", WindowKind.Tool), targetId: "output").ThrowIfFailed();
        manager.Register(new("toolbox", "Toolbox", WindowKind.Tool), open: false).ThrowIfFailed();
        manager.AutoHide("toolbox", DockSide.Left, false).ThrowIfFailed();
        manager.Activate("explorer").ThrowIfFailed(); manager.Activate("output").ThrowIfFailed(); manager.Activate("welcome").ThrowIfFailed();
        _models?.Dispose();
        _models = new DockWorkspaceViewModel(manager, action => { if (DispatcherQueue.HasThreadAccess) action(); else DispatcherQueue.TryEnqueue(() => action()); });
        _workspaceGeneration++;
        _site.Manager = manager;
        SetTheme(_theme); Log("Ready — drag a document tab or tool-window title to rearrange the workspace");
    }
    private void SetTheme(DockTheme theme)
    {
        _theme = theme; _site.Theme = theme;
        RequestedTheme = theme.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        _root.Background = DockChrome.Brush(theme.Background); _status.Foreground = DockChrome.Brush(theme.Text);
        BuildMenu(); _site.Refresh(); Log("Theme: " + theme.Name);
    }
    private void NewDocument()
    {
        string id = "new-" + _nextDocument++;
        _documents[id] = "// " + id + "\n// This editor instance is retained while docking, floating and hiding.\n";
        Execute(_site.Manager.Register(new(id, "Document " + (_nextDocument - 1) + ".cs", WindowKind.Document)));
    }
    private void SaveLayout()
    {
        try
        {
            _saved = _site.Manager.Layout; string json = DockLayoutSerializer.Serialize(_saved);
#if BROWSER_WASM
            Uno.Foundation.WebAssemblyRuntime.InvokeJS("localStorage.setItem('dockingscene.layout.v1', " + JsonSerializer.Serialize(json) + ");");
#else
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DockingScene");
            Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "workspace.json"), json);
#endif
            _modified = false; Log("Layout saved");
        }
        catch (Exception e) { Log("Could not save layout: " + e.Message); }
    }
    private void LoadLayout()
    {
        try
        {
            string? json = null;
#if BROWSER_WASM
            json = Uno.Foundation.WebAssemblyRuntime.InvokeJS("localStorage.getItem('dockingscene.layout.v1') || ''");
#else
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DockingScene", "workspace.json");
            if (File.Exists(path)) json = File.ReadAllText(path);
#endif
            if (string.IsNullOrWhiteSpace(json) && _saved is not null) json = DockLayoutSerializer.Serialize(_saved);
            if (string.IsNullOrWhiteSpace(json)) { Log("No saved layout. Use Save layout first."); return; }
            var result = DockLayoutSerializer.Restore(_site.Manager, json); Execute(result);
            if (result.Success) Log("Layout restored");
        }
        catch (Exception e) { Log("Could not load layout: " + e.Message); }
    }
    private void BuildMenu()
    {
        _menu.Children.Clear(); _toolbar.Children.Clear();
        void Menu(string name, Action<MenuFlyout> fill)
        {
            Button? button = null;
            button = DockChrome.Button(name, "menubar:" + name, _theme, () => { var menu = new MenuFlyout(); fill(menu); menu.ShowAt(button!); });
            button.Height = 27; button.Padding = new(10, 2, 10, 2); _menu.Children.Add(button);
        }
        void Item(MenuFlyout menu, string title, Action action, bool enabled = true)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = title, IsEnabled = enabled, FontSize = 12, MinHeight = 26, Padding = new(10, 3, 10, 3) }, "appmenu:" + title);
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        Menu("File", m => { Item(m, "New document", NewDocument); Item(m, "Save layout", SaveLayout); Item(m, "Load layout", LoadLayout); m.Items.Add(new MenuFlyoutSeparator()); Item(m, "Reset workspace", Reset); Item(m, "Close active window", () => { if (_site.Manager.Layout.ActiveWindowId is { } id) Execute(_site.Manager.Close(id)); }); });
        Menu("Edit", m => { Item(m, "Undo layout change", () => Execute(_site.Manager.Undo()), _site.Manager.CanUndo); Item(m, "Redo layout change", () => Execute(_site.Manager.Redo()), _site.Manager.CanRedo); });
        Menu("View", m => { foreach (var window in _site.Manager.Layout.Windows.Values.OrderBy(w => w.Kind).ThenBy(w => w.Title)) { string id = window.Id; Item(m, window.Title, () => Execute(_site.Manager.Open(id))); } });
        Menu("Theme", m => { foreach (var theme in DockTheme.BuiltIn) Item(m, theme.Name, () => SetTheme(theme)); });
        Menu("Window", m =>
        {
            Item(m, "Tabbed MDI", () => Execute(_site.Manager.SetMdiMode(MdiMode.Tabbed)));
            Item(m, "Standard MDI", () => { Execute(_site.Manager.SetMdiMode(MdiMode.Standard)); Arrange(MdiArrangement.Cascade); });
            Item(m, "Cascade", () => Arrange(MdiArrangement.Cascade)); Item(m, "Tile horizontally", () => Arrange(MdiArrangement.TileHorizontally));
            Item(m, "Tile vertically", () => Arrange(MdiArrangement.TileVertically)); Item(m, "Tile grid", () => Arrange(MdiArrangement.TileGrid));
            Item(m, _site.Manager.Layout.IsLayoutLocked ? "Unlock layout" : "Lock layout", () => Execute(_site.Manager.SetLayoutLocked(!_site.Manager.Layout.IsLayoutLocked)));
            AddMdiReviewCommands(m);
            Item(m, "Window switcher", () => _site.ShowSwitcher());
            Item(m, "Close all documents", () => Execute(_site.Manager.CloseMany(_site.Manager.Layout.Windows.Values.Where(w => w.Kind == WindowKind.Document && w.DisplayMode != TabDisplayMode.Pinned).Select(w => w.Id))));
        });
        Menu("Help", m => { Item(m, "Welcome", () => Execute(_site.Manager.Open("welcome"))); Item(m, "Package overview", () => Execute(_site.Manager.Open("readme"))); });
        void Tool(string name, string glyph, Action action) { var button = DockChrome.Button(name, "toolbar:" + name, _theme, action, glyph); button.Width = 27; button.Height = 27; _toolbar.Children.Add(button); }
        Tool("New", DockGlyphs.Add, NewDocument); Tool("Save", DockGlyphs.Save, SaveLayout); Tool("Load", DockGlyphs.Folder, LoadLayout);
        _toolbar.Children.Add(new Border { Width = 1, Height = 18, Background = DockChrome.Brush(_theme.Border), Margin = new(5, 0, 5, 0) });
        Tool("Undo", "M6,3 L2,7 L6,11 M2,7 L9,7 Q15,7 13,13", () => Execute(_site.Manager.Undo()));
        Tool("Redo", "M10,3 L14,7 L10,11 M14,7 L7,7 Q1,7 3,13", () => Execute(_site.Manager.Redo()));
        var mode = new ComboBox { MinHeight = 25, Height = 26, Width = 135, FontSize = 12, Padding = new(7, 1, 20, 1), ItemsSource = new[] { "Tabbed MDI", "Standard MDI" }, SelectedIndex = _site.Manager.Layout.MdiMode == MdiMode.Tabbed ? 0 : 1 };
        DockChrome.Identify(mode, "toolbar:mdi", "MDI mode");
        mode.SelectionChanged += (_, _) =>
        {
            if (mode.SelectedIndex >= 0 && mode.SelectedIndex != (int)_site.Manager.Layout.MdiMode)
            { Execute(_site.Manager.SetMdiMode((MdiMode)mode.SelectedIndex)); if (mode.SelectedIndex == 1) Arrange(MdiArrangement.Cascade); }
        };
        _toolbar.Children.Add(mode);
        var themePicker = new ComboBox { MinHeight = 25, Height = 26, Width = 146, FontSize = 12, Padding = new(7, 1, 20, 1), ItemsSource = DockTheme.BuiltIn.Select(t => t.Name).ToArray(), SelectedItem = _theme.Name };
        DockChrome.Identify(themePicker, "toolbar:theme", "Theme");
        // Loading a ComboBox can deliver its initial selection asynchronously. Never rebuild it for that same selection.
        themePicker.SelectionChanged += (_, _) => { if (themePicker.SelectedIndex >= 0 && DockTheme.BuiltIn[themePicker.SelectedIndex].Name != _theme.Name) SetTheme(DockTheme.BuiltIn[themePicker.SelectedIndex]); };
        _toolbar.Children.Add(themePicker);
        _toolbar.Children.Add(DockChrome.Button("Reset layout", "toolbar:Reset", _theme, Reset));
        _toolbar.Children.Add(DockChrome.Button("Windows", "toolbar:Windows", _theme, () => _site.ShowSwitcher()));
        var brand = DockChrome.Text("DOCKINGSCENE", _theme, 11); brand.Margin = new(24, 0, 0, 0); brand.Foreground = DockChrome.Brush(_theme.MutedText); _toolbar.Children.Add(brand);
    }
    private void Arrange(MdiArrangement arrangement)
    {
        var workspace = DockTree.Descendants(_site.Manager.Layout.Root).OfType<DockWorkspace>().FirstOrDefault();
        var bounds = workspace is not null && _site.Geometry?.Nodes.TryGetValue(workspace.Id, out var rect) == true ? rect : new DockRect(0, 0, 800, 500);
        Execute(WindowGeometry.ArrangeMdi(_site.Manager, new(0, 0, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height)), arrangement));
    }
}
