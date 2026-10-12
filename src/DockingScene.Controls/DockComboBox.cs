using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace DockingScene.Controls;

/// <summary>Native Uno ComboBox with popup-local keyboard routing. Native selection,
/// cancel, type search, scrolling and templates remain owned by ComboBox. The
/// detached popup forwards the original event, never a synthesized key.</summary>
public class DockComboBox : ComboBox
{
    private Popup? _popup;
    private FrameworkElement? _inputScope;
    private readonly KeyEventHandler _popupKeyDown;
    private readonly KeyEventHandler _popupKeyUp;
    private bool _completingFocus, _forwarding;
    public long ForwardedKeyCount { get; private set; }

    public DockComboBox()
    {
        _popupKeyDown = (_, e) =>
        {
            if (!CanForward(e)) return;
            _forwarding = true;
            try { ForwardedKeyCount++; base.OnKeyDown(e); }
            finally { _forwarding = false; }
        };
        _popupKeyUp = (_, e) =>
        {
            if (!CanForward(e)) return;
            _forwarding = true;
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
    private bool CanForward(KeyRoutedEventArgs e) => !_forwarding && !e.Handled && !IsEditable &&
        IsEnabled && IsLoaded && IsDropDownOpen && _inputScope is { IsLoaded: true } &&
        _inputScope.XamlRoot == XamlRoot;

    private void AttachPopupInput()
    {
        if (IsEditable || _popup?.Child is not FrameworkElement child || ReferenceEquals(child, _inputScope)) return;
        DetachPopupInput(); _inputScope = child;
        // Popup content is outside the selector's ordinary visual ancestry. Route
        // once at its input boundary before container/default navigation. A key
        // handled here cannot be handled a second time by the native bubble path.
        child.AddHandler(PreviewKeyDownEvent, _popupKeyDown, false);
        child.AddHandler(PreviewKeyUpEvent, _popupKeyUp, false);
    }
    private void DetachPopupInput()
    {
        if (_inputScope is not { } scope) return;
        _inputScope = null;
        scope.RemoveHandler(PreviewKeyDownEvent, _popupKeyDown);
        scope.RemoveHandler(PreviewKeyUpEvent, _popupKeyUp);
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
