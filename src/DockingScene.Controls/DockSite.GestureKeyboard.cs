using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DockingScene.Controls;

public partial class DockSite
{
    private UIElement? _gestureKeyboardRoot;
    private KeyEventHandler? _gestureEscape;
    public bool HasCapturedGestureKeyboardScope => _gestureKeyboardRoot is not null;
    private void AcquireGestureKeyboard()
    {
        if (XamlRoot?.Content is not { } root) return;
        if (ReferenceEquals(root, _gestureKeyboardRoot)) return;
        ReleaseGestureKeyboard();
        _gestureKeyboardRoot = root;
        _gestureEscape ??= (_, e) =>
        {
            if (e.Key != VirtualKey.Escape || !IsInteractionActive ||
                InteractionCaptureOwner.PointerCaptures?.Count is not > 0 ||
                !ReferenceEquals(XamlRoot?.Content, _gestureKeyboardRoot)) return;
            // Pointer capture does not transfer keyboard focus from an external
            // toolbar. Only the currently capturing site consumes this key.
            e.Handled = true;
            CancelInteraction();
        };
        root.AddHandler(PreviewKeyDownEvent, _gestureEscape, true);
    }
    private void ReleaseGestureKeyboard()
    {
        if (_gestureKeyboardRoot is not { } root) return;
        _gestureKeyboardRoot = null;
        root.RemoveHandler(PreviewKeyDownEvent, _gestureEscape!);
    }
}
