using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;

namespace DockingScene.Controls;

public class DockContainer : UserControl
{
    private readonly Grid _root = new();
    private readonly Grid _title = new();
    private readonly TextBlock _caption = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new(6, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly DockCaptionGrip _grip = new() { Margin = new(7, 0, 6, 0) };
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal };
    public AdvancedTabControl Tabs { get; } = new();
    private (string Id, DockWindow? Selected, DockTheme Theme, bool Active, bool AutoHidden, bool Locked, DockCapabilities Allowed)? _chrome;
    public DockGroup? Group { get; private set; }
    public IReadOnlyList<int> CaptionButtonIdentities => _buttons.Children.Select(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode).ToArray();
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
        _title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _title.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        _title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _title.Children.Add(_caption); _title.Children.Add(_grip); _title.Children.Add(_buttons);
        Grid.SetColumn(_grip, 1); Grid.SetColumn(_buttons, 2);
        _title.SizeChanged += (_, _) => _caption.MaxWidth = Math.Max(0, _title.ActualWidth - 100);
        foreach (var surface in new FrameworkElement[] { _caption, _grip })
        {
            surface.PointerPressed += (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { HeaderPointerPressed?.Invoke(this, new(e)); e.Handled = true; } };
            surface.DoubleTapped += (_, e) => { HeaderDoubleTapped?.Invoke(); e.Handled = true; };
            surface.RightTapped += (_, e) => { OptionsRequested?.Invoke(surface); e.Handled = true; };
        }
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
            string foreground = active ? theme.AccentText : theme.Text;
            _caption.Text = selected?.Title ?? ""; _caption.FontSize = theme.FontSize; _caption.Foreground = DockChrome.Brush(foreground);
            _grip.Foreground = DockChrome.Brush(active ? theme.AccentText : theme.Border);
            DockChrome.Identify(_caption, "group-title:" + group.Id, selected?.Title);
            DockChrome.Identify(_grip, "group-grip:" + group.Id, "Drag " + selected?.Title);
            if (tools)
            {
                if (_buttons.Children.Count == 0)
                {
                    var style = (Style)new TabHeaderResources()["DockTabButtonStyle"];
                    Button Make(Action<FrameworkElement> action)
                    {
                        Button? button = null;
                        button = DockChrome.Button("", "", theme, () => action(button!));
                        button.Style = style; button.Width = 23; button.Padding = new(5); _buttons.Children.Add(button); return button;
                    }
                    Make(b => OptionsRequested?.Invoke(b)); Make(_ => PinRequested?.Invoke()); Make(_ => CloseRequested?.Invoke());
                }
                void Paint(int index, string name, string glyph, bool enabled)
                {
                    var button = (Button)_buttons.Children[index];
                    DockChrome.Identify(button, "group:" + group.Id + ":" + name, name);
                    ToolTipService.SetToolTip(button, name); button.Height = theme.TitleHeight; button.IsEnabled = enabled;
                    button.Background = _title.Background; button.Foreground = DockChrome.Brush(foreground);
                    if (button.Tag as string != glyph) { button.Tag = glyph; button.Content = DockChrome.Glyph(glyph, foreground); }
                    else if (button.Content is Microsoft.UI.Xaml.Shapes.Path path) path.Stroke = button.Foreground;
                }
                var capabilities = (selected?.Capabilities ?? DockCapabilities.None) & allowed;
                Paint(0, "Options", DockGlyphs.Chevron, true);
                Paint(1, autoHidden ? "Pin" : "AutoHide", autoHidden ? DockGlyphs.Unpin : DockGlyphs.Pin,
                    !layout.IsLayoutLocked && capabilities.HasFlag(autoHidden ? DockCapabilities.Dock : DockCapabilities.AutoHide));
                Paint(2, "Close", DockGlyphs.Close, capabilities.HasFlag(DockCapabilities.Close));
            }
        }
        Tabs.IsToolGroup = tools; Tabs.ShowNewTabButton = !tools;
        Tabs.Update(items, group.SelectedId, theme, group.TabPlacement, active);
    }
}
public sealed class ToolWindowContainer : DockContainer { }
public sealed class TabbedMdiContainer : DockContainer { }
public sealed class Workspace : ContentControl { }
