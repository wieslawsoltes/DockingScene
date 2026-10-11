using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

/// <summary>MRU keyboard switcher. Selection is provisional until committed.</summary>
public sealed partial class StandardSwitcher : UserControl
{
    private readonly string[] _ids;
    private readonly DockLayout _layout;
    private readonly ListView _tools = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
    private readonly ListView _documents = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock _description;
    private int _index;
    public string? SelectedId => _ids.Length == 0 ? null : _ids[_index];
    public event Action<string>? Committed;
    public event Action? Cancelled;
    public StandardSwitcher(DockLayout layout, DockTheme theme, DockStrings? strings = null)
    {
        strings ??= new(); _layout = layout; IsTabStop = true;
        _ids = layout.ActivationHistory.Concat(layout.Windows.Keys.Where(id => !layout.HiddenWindows.Contains(id))).Distinct(StringComparer.Ordinal).ToArray();
        var grid = new Grid { Padding = new(16), Background = DockChrome.Brush(theme.Surface) };
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); grid.RowDefinitions.Add(new() { Height = new(50) });
        grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new(1.4, GridUnitType.Star) });
        var toolTitle = DockChrome.Text(strings.Tools, theme, 14); toolTitle.Margin = new(0, 0, 0, 8);
        var documentTitle = DockChrome.Text(strings.Documents, theme, 14); documentTitle.Margin = new(8, 0, 0, 8);
        grid.Children.Add(toolTitle); grid.Children.Add(documentTitle); Grid.SetColumn(documentTitle, 1);
        Grid.SetRow(_tools, 1); Grid.SetRow(_documents, 1); Grid.SetColumn(_documents, 1); grid.Children.Add(_tools); grid.Children.Add(_documents);
        _tools.ItemsSource = _ids.Where(id => layout.Windows[id].Kind == WindowKind.Tool).Select(id => layout.Windows[id]).ToArray();
        _documents.ItemsSource = _ids.Where(id => layout.Windows[id].Kind == WindowKind.Document).Select(id => layout.Windows[id]).ToArray();
        _tools.DisplayMemberPath = nameof(DockWindow.Title); _documents.DisplayMemberPath = nameof(DockWindow.Title);
        foreach (var list in new[] { _tools, _documents })
        {
            list.FontSize = theme.FontSize; list.Foreground = DockChrome.Brush(theme.Text);
            list.ItemClick += (_, e) => { if (e.ClickedItem is DockWindow w) { _index = Array.IndexOf(_ids, w.Id); Commit(); } };
        }
        _description = DockChrome.Text("", theme); _description.TextWrapping = TextWrapping.Wrap; _description.Margin = new(0, 8, 0, 0);
        Grid.SetRow(_description, 2); Grid.SetColumnSpan(_description, 2); grid.Children.Add(_description);
        Content = new Border { BorderBrush = DockChrome.Brush(theme.Accent), BorderThickness = new(2), Child = grid };
        DockChrome.Identify(this, "window-switcher", "Window switcher");
        Loaded += (_, _) =>
        {
            if (_completed) return;
            AcquireInputScope(XamlRoot?.Content);
            // Do not defer focus past the input that opened the switcher. The
            // root preview scope also covers the interval before Loaded fires.
            TakeInputFocus();
        };
        Unloaded += (_, _) => DetachInputScope();
        Move(_ids.Length > 1 ? 1 : 0);
    }
    public void Move(int delta)
    {
        if (_completed || _ids.Length == 0) return;
        _index = (_index + delta % _ids.Length + _ids.Length) % _ids.Length;
        var window = _layout.Windows[_ids[_index]];
        _tools.SelectedItem = window.Kind == WindowKind.Tool ? window : null;
        _documents.SelectedItem = window.Kind == WindowKind.Document ? window : null;
        _description.Text = window.Title + "\n" + window.Description;
    }
    public void Commit()
    {
        if (_completed || SelectedId is not { } id) return;
        ReleaseInputScope(); Committed?.Invoke(id);
    }
    public void Cancel()
    {
        if (_completed) return;
        ReleaseInputScope(); Cancelled?.Invoke();
    }
}
