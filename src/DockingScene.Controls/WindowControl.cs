using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace DockingScene.Controls;

public sealed class WindowDragEventArgs(PointerRoutedEventArgs pointer) : EventArgs
{
    public PointerRoutedEventArgs Pointer { get; } = pointer;
    public bool Handled { get; set; }
}

/// <summary>Reusable window-like control with eight resize grips and pointer-captured movement.</summary>
public class WindowControl : UserControl
{
    private readonly Grid _root = new();
    private readonly Grid _title = new();
    private readonly ContentPresenter _content = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly TextBlock _titleText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new(7, 0, 4, 0), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal };
    private Point _start;
    private DockRect _original;
    private DockSide _resizeX, _resizeY;
    private bool _moving;
    private UIElement? _coordinateRoot;
    public string WindowId { get; set; } = "window";
    public DockRect Bounds { get; private set; } = new(80, 80, 500, 320);
    public DockSize MinimumSize { get; set; } = new(100, 70);
    public DockSize MaximumSize { get; set; } = DockSize.Maximum;
    public bool CanResize { get; set; } = true;
    public bool CanMove { get; set; } = true;
    public bool CanClose { get; set; } = true;
    private (string Id, string Title, DockTheme Theme, bool Active, bool Minimize, bool Maximize, bool Maximized, bool Close)? _chrome;
    public bool ShowMinimize { get; set; }
    public bool ShowMaximize { get; set; } = true;
    public bool IsMaximized { get; set; }
    public event EventHandler<WindowDragEventArgs>? HeaderDragStarting;
    public event Action<DockRect>? BoundsChanging;
    public event Action<DockRect>? BoundsChanged;
    public event Action? CloseRequested;
    public event Action? MaximizeRequested;
    public event Action? MinimizeRequested;
    public event Action? Activated;

    public WindowControl()
    {
        Content = _root;
        _root.RowDefinitions.Add(new() { Height = new(24) }); _root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        _root.Children.Add(_title); _root.Children.Add(_content); Grid.SetRow(_content, 1);
        _title.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); _title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _title.Children.Add(_titleText); _title.Children.Add(_buttons); Grid.SetColumn(_buttons, 1);
        _titleText.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Activated?.Invoke();
            var request = new WindowDragEventArgs(e); HeaderDragStarting?.Invoke(this, request);
            if (!request.Handled && !IsMaximized && CanMove) Begin(e, DockSide.Center, DockSide.Center);
            e.Handled = true;
        };
        _titleText.DoubleTapped += (_, e) => { if (ShowMaximize) MaximizeRequested?.Invoke(); e.Handled = true; };
        foreach (var (horizontal, vertical) in new[]
        {
            (DockSide.Left, DockSide.Top), (DockSide.Center, DockSide.Top), (DockSide.Right, DockSide.Top),
            (DockSide.Left, DockSide.Center), (DockSide.Right, DockSide.Center),
            (DockSide.Left, DockSide.Bottom), (DockSide.Center, DockSide.Bottom), (DockSide.Right, DockSide.Bottom)
        })
        {
            var grip = new Border { Background = DockChrome.Brush("#00000000") };
            bool corner = horizontal != DockSide.Center && vertical != DockSide.Center;
            grip.HorizontalAlignment = horizontal == DockSide.Left ? HorizontalAlignment.Left : horizontal == DockSide.Right ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
            grip.VerticalAlignment = vertical == DockSide.Top ? VerticalAlignment.Top : vertical == DockSide.Bottom ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            if (horizontal != DockSide.Center) grip.Width = corner ? 8 : 4;
            if (vertical != DockSide.Center) grip.Height = corner ? 8 : 4;
            if (horizontal == DockSide.Center) grip.Margin = new(8, 0, 8, 0);
            if (vertical == DockSide.Center) grip.Margin = new(0, 8, 0, 8);
            Grid.SetRowSpan(grip, 2); _root.Children.Add(grip);
            var rx = horizontal; var ry = vertical;
            grip.PointerPressed += (_, e) => { if (CanResize && !IsMaximized && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { Begin(e, rx, ry); e.Handled = true; } };
        }
        PointerMoved += (_, e) =>
        {
            if (!_moving || _coordinateRoot is null) return;
            if (_resizeX == DockSide.Center && _resizeY == DockSide.Center ? !CanMove : !CanResize) { CancelMovement(); return; }
            var point = e.GetCurrentPoint(_coordinateRoot).Position;
            double dx = point.X - _start.X, dy = point.Y - _start.Y;
            var rect = _resizeX == DockSide.Center && _resizeY == DockSide.Center ? _original with { X = _original.X + dx, Y = _original.Y + dy } : WindowGeometry.Resize(_original, dx, dy, _resizeX, _resizeY, MinimumSize, MaximumSize);
            SetBounds(rect); BoundsChanging?.Invoke(rect); e.Handled = true;
        };
        PointerReleased += (_, e) => { if (!_moving) return; _moving = false; ReleasePointerCapture(e.Pointer); BoundsChanged?.Invoke(Bounds); e.Handled = true; };
        PointerCanceled += (_, _) => CancelMovement();
        Unloaded += (_, _) => CancelMovement();
        PointerCaptureLost += (_, _) => { if (_moving) CancelMovement(); };
    }
    private void Begin(PointerRoutedEventArgs e, DockSide x, DockSide y)
    {
        _coordinateRoot = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(this) as UIElement ?? this;
        _start = e.GetCurrentPoint(_coordinateRoot).Position; _original = Bounds; _resizeX = x; _resizeY = y;
        _moving = CapturePointer(e.Pointer);
    }
    public void CancelMovement() { if (!_moving) return; _moving = false; SetBounds(_original); ReleasePointerCaptures(); }
    public void SetBounds(DockRect bounds) { if (Bounds == bounds && Width == bounds.Width && Height == bounds.Height) return; Bounds = bounds; DockChrome.Position(this, bounds); }
    public void SetWindowContent(object? content) => ContentLease.Set(_content, content);
    public void UpdateChrome(string title, DockTheme theme, bool active)
    {
        var chrome = (WindowId, title, theme, active, ShowMinimize, ShowMaximize, IsMaximized, CanClose);
        if (_chrome == chrome) return;
        _chrome = chrome;
        _root.Background = DockChrome.Brush(theme.Surface); _root.BorderBrush = DockChrome.Brush(active ? theme.Accent : theme.Border); _root.BorderThickness = new(1);
        _root.RowDefinitions[0].Height = new(theme.TitleHeight);
        _title.Background = DockChrome.Brush(active ? theme.ActiveToolTitle : theme.Background);
        _titleText.Text = title; _titleText.FontSize = theme.FontSize; _titleText.Foreground = DockChrome.Brush(active ? theme.AccentText : theme.Text);
        DockChrome.Identify(_titleText, "window-title:" + WindowId, title);
        _buttons.Children.Clear();
        void Add(string name, string glyph, Action action)
        {
            var button = DockChrome.Button(name, "window:" + WindowId + ":" + name.ToLowerInvariant(), theme, action, glyph);
            button.Width = 26; button.Height = theme.TitleHeight; button.Padding = new(5); button.Background = _title.Background;
            button.Content = DockChrome.Glyph(glyph, active ? theme.AccentText : theme.Text);
            if (name == "Close") button.IsEnabled = CanClose;
            _buttons.Children.Add(button);
        }
        if (ShowMinimize) Add("Minimize", DockGlyphs.Minimize, () => MinimizeRequested?.Invoke());
        if (ShowMaximize) Add(IsMaximized ? "Restore" : "Maximize", IsMaximized ? DockGlyphs.Restore : DockGlyphs.Maximize, () => MaximizeRequested?.Invoke());
        Add("Close", DockGlyphs.Close, () => CloseRequested?.Invoke());
    }
}
