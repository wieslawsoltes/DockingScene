using System.Runtime.CompilerServices;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace DockingScene.Controls;

public partial class WindowControl
{
    private readonly Dictionary<string, Button> _captionButtons = new(StringComparer.Ordinal);
    private Button? _systemButton;
    private MenuFlyout? _systemMenu;
    private FrameworkElement? _pendingSystemMenuAnchor;
    private long _systemMenuGeneration;
    private DockTheme _theme = DockTheme.MetroLight;
    private UIElement? _movementInputRoot;
    private KeyEventHandler? _movementEscape, _movementKeyUp;
    private PointerEventHandler? _movementPointer;
    private WindowChromeState? _chrome;
    private readonly record struct WindowChromeState(string Id, string Title, DockTheme Theme, bool Active,
        bool Minimize, bool Maximize, bool Maximized, bool Minimized, bool Close, bool Restore, bool SystemMenu);
    public bool IsMinimized { get; set; }
    public bool CanRestore { get; set; } = true;
    public bool HasSystemMenu { get; set; } = true;
    public event Action? RestoreRequested;
    public IReadOnlyDictionary<string, int> CaptionButtonIdentities => _captionButtons.ToDictionary(p => p.Key, p => RuntimeHelpers.GetHashCode(p.Value));

    private void ConfigureWindowChrome()
    {
        SizeChanged += (_, _) => Clip = new RectangleGeometry { Rect = new(0, 0, ActualWidth, ActualHeight) };
    }
    private void RequestRestore()
    {
        if (!CanRestore || !(IsMaximized || IsMinimized)) return;
        if (RestoreRequested is { } restore) restore();
        else if (IsMaximized) MaximizeRequested?.Invoke();
    }
    private void RequestMaximize() { if (ShowMaximize && !IsMaximized) MaximizeRequested?.Invoke(); }
    private void RequestMinimize() { if (ShowMinimize && !IsMinimized) MinimizeRequested?.Invoke(); }
    private void RequestClose() { if (CanClose) CloseRequested?.Invoke(); }

    public void DismissSystemMenu()
    {
        _systemMenuGeneration++;
        _pendingSystemMenuAnchor = null;
        // Keep the closing instance until Closed. Uno completes native flyout
        // cleanup asynchronously and can otherwise close a newly opened menu.
        _systemMenu?.Hide();
    }
    public void ShowSystemMenu() => ShowSystemMenu(_systemButton);
    /// <summary>Open state/close commands at a caption or an external command bar.
    /// Callbacks recheck current flags rather than the menu's original snapshot.</summary>
    public void ShowSystemMenu(FrameworkElement? anchor)
    {
        if (!HasSystemMenu || anchor is null || !anchor.IsLoaded || !IsLoaded || anchor.XamlRoot != XamlRoot) return;
        long generation = ++_systemMenuGeneration;
        if (_systemMenu is { } previous)
        {
            _pendingSystemMenuAnchor = anchor;
            previous.Hide();
            return;
        }
        OpenSystemMenu(anchor, generation);
    }
    private void OpenSystemMenu(FrameworkElement anchor, long generation)
    {
        if (generation != _systemMenuGeneration || !HasSystemMenu || !IsLoaded ||
            !anchor.IsLoaded || anchor.XamlRoot != XamlRoot) return;
        string idAtOpen = WindowId;
        var root = XamlRoot;
        Activated?.Invoke();
        if (generation != _systemMenuGeneration || !IsLoaded || !anchor.IsLoaded || XamlRoot != root) return;
        var menu = new MenuFlyout(); _systemMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (!ReferenceEquals(_systemMenu, menu)) return;
            _systemMenu = null;
            var next = _pendingSystemMenuAnchor;
            long request = _systemMenuGeneration;
            _pendingSystemMenuAnchor = null;
            if (next is not null)
                // Run after the native Closed handler finishes its open-flyout
                // cleanup; opening inside Closed would be immediately dismissed.
                DispatcherQueue.TryEnqueue(() => OpenSystemMenu(next, request));
        };
        void Item(string text, string id, bool enabled, Action action)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = text, IsEnabled = enabled, FontSize = _theme.FontSize, MinHeight = 26 }, "window-menu:" + WindowId + ":" + id);
            item.Click += (_, _) =>
            {
                if (generation == _systemMenuGeneration && WindowId == idAtOpen && HasSystemMenu &&
                    IsLoaded && anchor.IsLoaded && XamlRoot == root && anchor.XamlRoot == root) action();
            };
            menu.Items.Add(item);
        }
        Item("Restore", "restore", CanRestore && (IsMaximized || IsMinimized), RequestRestore);
        Item("Move", "move", AllowsKeyboard(DockingScene.Layout.WindowKeyboardMode.Move), () => BeginKeyboardMove());
        Item("Size", "size", AllowsKeyboard(DockingScene.Layout.WindowKeyboardMode.Resize), () => BeginKeyboardResize());
        Item("Minimize", "minimize", ShowMinimize && !IsMinimized, RequestMinimize);
        Item("Maximize", "maximize", ShowMaximize && !IsMaximized, RequestMaximize);
        menu.Items.Add(new MenuFlyoutSeparator());
        Item("Close", "close", CanClose, RequestClose);
        menu.ShowAt(anchor);
    }
    public void UpdateChrome(string title, DockTheme theme, bool active)
    {
        var chrome = new WindowChromeState(WindowId, title, theme, active, ShowMinimize, ShowMaximize, IsMaximized, IsMinimized, CanClose, CanRestore, HasSystemMenu);
        if (_chrome == chrome) return;
        _chrome = chrome; _theme = theme;
        _root.Background = DockChrome.Brush(theme.Surface); _root.BorderBrush = DockChrome.Brush(active ? theme.Accent : theme.Border);
        _title.Background = DockChrome.Brush(active ? theme.ActiveToolTitle : theme.Background);
        string foreground = active ? theme.AccentText : theme.Text;
        _titleText.Text = title; _titleText.FontSize = theme.FontSize; _titleText.Foreground = DockChrome.Brush(foreground);
        DockChrome.Identify(_titleText, "window-title:" + WindowId, title);
        if (_systemButton is null)
        {
            var style = (Style)new TabHeaderResources()["DockTabButtonStyle"];
            _systemButton = DockChrome.Button("Window menu", "", theme, ShowSystemMenu, DockGlyphs.Document);
            _systemButton.Style = style; _systemButton.Width = 23; _systemButton.Padding = new(5); _title.Children.Insert(0, _systemButton);
            void Add(string key, Action action)
            {
                var button = DockChrome.Button("", "", theme, action);
                button.Style = style; button.Width = 26; button.Padding = new(5);
                _captionButtons.Add(key, button); _buttons.Children.Add(button);
            }
            Add("restore", RequestRestore); Add("minimize", RequestMinimize);
            Add("maximize", () => { if (IsMaximized) RequestRestore(); else RequestMaximize(); }); Add("close", RequestClose);
        }
        void Paint(Button button, string name, string suffix, string glyph, bool visible, bool enabled)
        {
            DockChrome.Identify(button, "window:" + WindowId + ":" + suffix, name); ToolTipService.SetToolTip(button, name);
            button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; button.IsEnabled = enabled;
            button.Height = theme.TitleHeight; button.Background = _title.Background; button.Foreground = DockChrome.Brush(foreground);
            if (button.Tag as string != glyph) { button.Tag = glyph; button.Content = DockChrome.Glyph(glyph, foreground); }
            else if (button.Content is Microsoft.UI.Xaml.Shapes.Path icon) icon.Stroke = button.Foreground;
        }
        Paint(_systemButton, "Window menu", "system", DockGlyphs.Document, HasSystemMenu, true);
        Paint(_captionButtons["restore"], "Restore", "restore", DockGlyphs.Restore, IsMinimized, CanRestore);
        Paint(_captionButtons["minimize"], "Minimize", "minimize", DockGlyphs.Minimize, ShowMinimize && !IsMinimized, true);
        Paint(_captionButtons["maximize"], IsMaximized ? "Restore" : "Maximize", IsMaximized ? "restore" : "maximize",
            IsMaximized ? DockGlyphs.Restore : DockGlyphs.Maximize, ShowMaximize || IsMaximized, IsMaximized ? CanRestore : ShowMaximize);
        Paint(_captionButtons["close"], "Close", "close", DockGlyphs.Close, true, CanClose);
        ApplyFrameVisibility();
    }
    private void AcquireMovementInput()
    {
        if (XamlRoot?.Content is not { } root || _movementInputRoot is not null) return;
        _movementInputRoot = root;
        _movementEscape ??= (_, e) => HandleMovementKey(e);
        _movementKeyUp ??= (_, e) =>
        {
            if (IsKeyboardInteractionActive && _keyboardCommitPending && e.Key == VirtualKey.Enter)
            { e.Handled = true; CompleteKeyboardInteraction(); }
        };
        _movementPointer ??= (_, _) => { if (IsKeyboardInteractionActive) CancelMovement(); };
        root.AddHandler(PreviewKeyDownEvent, _movementEscape, true);
        root.AddHandler(PreviewKeyUpEvent, _movementKeyUp, true);
        root.AddHandler(PointerPressedEvent, _movementPointer, true);
    }
    private void ReleaseMovementInput()
    {
        if (_movementInputRoot is not { } root) return;
        _movementInputRoot = null; root.RemoveHandler(PreviewKeyDownEvent, _movementEscape!);
        root.RemoveHandler(PreviewKeyUpEvent, _movementKeyUp!);
        root.RemoveHandler(PointerPressedEvent, _movementPointer!);
    }
}
