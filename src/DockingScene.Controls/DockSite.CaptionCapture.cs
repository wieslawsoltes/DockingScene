using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

public partial class DockSite
{
    private UIElement? _interactionCaptureOwner;
    private UIElement InteractionCaptureOwner => _interactionCaptureOwner ?? _canvas;
    private UIElement? FindCaptionCaptureTarget(DependencyObject? source)
    {
        for (var node = source; node is not null && !ReferenceEquals(node, _canvas); node = VisualTreeHelper.GetParent(node))
            if (node is DockContainer container) return container.GetCaptionCaptureTarget(source);
        return null;
    }
    private void ReleaseInteractionPointer(PointerRoutedEventArgs e)
    {
        var owner = InteractionCaptureOwner;
        _interactionCaptureOwner = null;
        owner.ReleasePointerCapture(e.Pointer);
    }
    private void ReleaseInteractionPointers()
    {
        var owner = InteractionCaptureOwner;
        _interactionCaptureOwner = null;
        owner.ReleasePointerCaptures();
        if (!ReferenceEquals(owner, _canvas)) _canvas.ReleasePointerCaptures();
    }
}

public partial class DockContainer
{
    internal UIElement? GetCaptionCaptureTarget(DependencyObject? source)
    {
        // Restrict this to caption/grip ancestry, never buttons, editors or tabs.
        for (var node = source; node is not null && !ReferenceEquals(node, this); node = VisualTreeHelper.GetParent(node))
            if (ReferenceEquals(node, _caption) || ReferenceEquals(node, _grip)) return (UIElement)node;
        return null;
    }
}
