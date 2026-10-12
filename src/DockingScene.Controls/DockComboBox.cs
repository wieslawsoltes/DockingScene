using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

/// <summary>Native Uno ComboBox with opening-scoped keyboard routing. Popup and
/// root routes forward the original event at most once. Native selection, Escape,
/// type search, scrolling and item templates remain owned by ComboBox.</summary>
public class DockComboBox : ComboBox
{
    private Popup? _popup;
    private FrameworkElement? _inputScope;
    private UIElement? _inputRoot;
    private readonly KeyEventHandler _popupKeyDown, _popupKeyUp;
    private KeyRoutedEventArgs? _lastDown, _lastUp;
    private bool _completingFocus, _forwarding;
    public long ForwardedKeyCount { get; private set; }

    public DockComboBox()
    {
        _popupKeyDown = (_, e) =>
        {
            if (ReferenceEquals(e, _lastDown) || !CanForward(e)) return;
            _lastDown = e; _forwarding = true;
            try { ForwardedKeyCount++; base.OnKeyDown(e); }
            finally { _forwarding = false; }
        };
        _popupKeyUp = (_, e) =>
        {
            if (ReferenceEquals(e, _lastUp) || !CanForward(e)) return;
            _lastUp = e; _forwarding = true;
            try { base.OnKeyUp(e); }
            finally { _forwarding = false; }
        };
        DropDownOpened += (_, _) => { AttachPopupInput(); CompletePopupFocus(); };
        DropDownClosed += (_, _) => DetachPopupInput();
        Unloaded += (_, _) => DetachPopupInput();
    }
    protected override void OnApplyTemplate()
    {
        DetachPopupInput();
        base.OnApplyTemplate();
        _popup = GetTemplateChild("Popup") as Popup;
        if (IsDropDownOpen) AttachPopupInput();
    }
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsDropDownOpen) { AttachPopupInput(); CompletePopupFocus(); }
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    { if (!ReferenceEquals(e, _lastDown)) base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyRoutedEventArgs e)
    { if (!ReferenceEquals(e, _lastUp)) base.OnKeyUp(e); }

    private bool CanForward(KeyRoutedEventArgs e)
    {
        if (_forwarding || e.Handled || IsEditable || !IsEnabled || !IsLoaded || !IsDropDownOpen ||
            XamlRoot is not { } root || _inputRoot != root.Content) return false;
        // A popup is a separate visual root on some Uno targets. The actual
        // focused item still identifies its selector even when its key route
        // bypasses the popup child. Never take a key from another selector/pane.
        var focused = FocusManager.GetFocusedElement(root) as DependencyObject;
        for (var node = focused; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, this) || ReferenceEquals(node, _inputScope)) return true;
            if (node is ComboBoxItem item && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item), this)) return true;
        }
        return false;
    }
    private void AttachPopupInput()
    {
        if (IsEditable || XamlRoot?.Content is not { } root) return;
        var child = _popup?.Child as FrameworkElement;
        if (ReferenceEquals(root, _inputRoot) && ReferenceEquals(child, _inputScope)) return;
        DetachPopupInput(); _inputRoot = root; _inputScope = child;
        root.AddHandler(PreviewKeyDownEvent, _popupKeyDown, false);
        root.AddHandler(PreviewKeyUpEvent, _popupKeyUp, false);
        if (child is not null && !ReferenceEquals(child, root))
        {
            child.AddHandler(PreviewKeyDownEvent, _popupKeyDown, false);
            child.AddHandler(PreviewKeyUpEvent, _popupKeyUp, false);
        }
    }
    private void DetachPopupInput()
    {
        if (_inputRoot is { } root)
        {
            root.RemoveHandler(PreviewKeyDownEvent, _popupKeyDown);
            root.RemoveHandler(PreviewKeyUpEvent, _popupKeyUp);
        }
        if (_inputScope is { } scope && !ReferenceEquals(scope, _inputRoot))
        {
            scope.RemoveHandler(PreviewKeyDownEvent, _popupKeyDown);
            scope.RemoveHandler(PreviewKeyUpEvent, _popupKeyUp);
        }
        _inputRoot = null; _inputScope = null;
        // Keep the last event identities until the current route ends. A native
        // Enter handler can close this popup inside the forwarding call itself.
    }
    private void CompletePopupFocus()
    {
        if (_completingFocus || IsEditable || !IsDropDownOpen || XamlRoot is not { } root) return;
        _completingFocus = true;
        try
        {
            (_popup?.Child as FrameworkElement)?.UpdateLayout();
            if (!IsLoaded || !IsDropDownOpen || XamlRoot != root) return;
            int index = SelectedIndex >= 0 ? SelectedIndex : 0;
            if (ContainerFromIndex(index) is Control item && item.IsEnabled && item.IsLoaded && item.Focus(FocusState.Keyboard)) return;
            Focus(FocusState.Keyboard);
        }
        finally { _completingFocus = false; }
    }
}
