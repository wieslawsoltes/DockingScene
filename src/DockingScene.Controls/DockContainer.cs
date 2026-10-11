using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DockingScene.Controls;

public class DockContainer : UserControl
{
    private readonly Grid _root = new();
    private readonly Grid _title = new();
    private readonly TextBlock _caption = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new(6, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal };
    public AdvancedTabControl Tabs { get; } = new();
    private (string Id, DockWindow? Selected, DockTheme Theme, bool Active, bool AutoHidden, bool Locked, DockCapabilities Allowed)? _chrome;
    public DockGroup? Group { get; private set; }
    public event EventHandler<WindowDragEventArgs>? HeaderPointerPressed;
    public event Action? HeaderDoubleTapped;
    public event Action<FrameworkElement>? OptionsRequested;
    public event Action? PinRequested;
    public event Action? CloseRequested;

    public DockContainer()
    {
        Content = _root;
        _root.RowDefinitions.Add(new() { Height = new(0) }); _root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        _root.Children.Add(_title); _root.Children.Add(Tabs); Grid.SetRow(Tabs, 1);
        _title.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _title.Children.Add(_caption); _title.Children.Add(_buttons); Grid.SetColumn(_buttons, 1);
        _caption.PointerPressed += (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { HeaderPointerPressed?.Invoke(this, new(e)); e.Handled = true; } };
        _caption.DoubleTapped += (_, e) => { HeaderDoubleTapped?.Invoke(); e.Handled = true; };
        _caption.RightTapped += (_, e) => { OptionsRequested?.Invoke(_caption); e.Handled = true; };
    }
    public void Update(DockGroup group, IReadOnlyList<AdvancedTabItem> items, DockLayout layout, DockTheme theme, bool autoHidden = false, DockCapabilities allowed = DockCapabilities.All)
    {
        Group = group;
        bool tools = group.Kind == GroupKind.Tools;
        bool active = group.Windows.Contains(layout.ActiveWindowId ?? "");
        var selected = group.SelectedId is null ? null : layout.Windows[group.SelectedId];
        bool title = tools && selected?.HasTitleBar == true;
        var chrome = (group.Id, selected, theme, active, autoHidden, layout.IsLayoutLocked, allowed);
        if (_chrome != chrome)
        {
        _chrome = chrome;
        _root.BorderBrush = DockChrome.Brush(theme.Border); _root.BorderThickness = tools ? new(1) : new(0);
        _root.RowDefinitions[0].Height = new(title ? theme.TitleHeight : 0);
        _title.Visibility = title ? Visibility.Visible : Visibility.Collapsed;
        _title.Background = DockChrome.Brush(active ? theme.ActiveToolTitle : theme.Background);
        _caption.Text = selected?.Title ?? ""; _caption.FontSize = theme.FontSize; _caption.Foreground = DockChrome.Brush(active ? theme.AccentText : theme.Text);
        DockChrome.Identify(_caption, "group-title:" + group.Id, selected?.Title);
        _buttons.Children.Clear();
        void Add(string name, string glyph, Action<FrameworkElement> action, bool enabled)
        {
            Button? button = null;
            button = DockChrome.Button(name, "group:" + group.Id + ":" + name, theme, () => action(button!), glyph);
            button.Width = 23; button.Height = theme.TitleHeight; button.Padding = new(5); button.IsEnabled = enabled;
            button.Background = _title.Background; button.Content = DockChrome.Glyph(glyph, active ? theme.AccentText : theme.Text);
            _buttons.Children.Add(button);
        }
        if (tools)
        {
            Add("Options", DockGlyphs.Chevron, b => OptionsRequested?.Invoke(b), true);
            Add(autoHidden ? "Pin" : "AutoHide", autoHidden ? DockGlyphs.Unpin : DockGlyphs.Pin, _ => PinRequested?.Invoke(), !layout.IsLayoutLocked && ((selected?.Capabilities ?? DockCapabilities.None) & allowed).HasFlag(autoHidden ? DockCapabilities.Dock : DockCapabilities.AutoHide));
            Add("Close", DockGlyphs.Close, _ => CloseRequested?.Invoke(), ((selected?.Capabilities ?? DockCapabilities.None) & allowed).HasFlag(DockCapabilities.Close));
        }
        }
        Tabs.IsToolGroup = tools; Tabs.ShowNewTabButton = !tools;
        Tabs.Update(items, group.SelectedId, theme, group.TabPlacement, active);
    }
}
public sealed class ToolWindowContainer : DockContainer { }
public sealed class TabbedMdiContainer : DockContainer { }
public sealed class Workspace : ContentControl { }
