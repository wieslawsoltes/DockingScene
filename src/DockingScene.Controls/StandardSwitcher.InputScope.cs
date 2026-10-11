using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DockingScene.Controls;

public sealed partial class StandardSwitcher
{
    private UIElement? _inputRoot;
    private KeyEventHandler? _previewDown, _previewUp;
    private bool _completed;
    internal bool HasInputScope => _inputRoot is not null;

    /// <summary>Intercept modal keys before an external launch button can handle
    /// them. The scope is local to this XamlRoot and exists only for this opening.</summary>
    internal void AcquireInputScope(UIElement? root)
    {
        if (_completed || root is null || ReferenceEquals(root, _inputRoot)) return;
        DetachInputScope(); _inputRoot = root;
        _previewDown ??= OnRootPreviewKeyDown;
        _previewUp ??= OnRootPreviewKeyUp;
        root.AddHandler(PreviewKeyDownEvent, _previewDown, true);
        root.AddHandler(PreviewKeyUpEvent, _previewUp, true);
    }
    internal void ReleaseInputScope()
    {
        _completed = true; DetachInputScope();
    }
    private void DetachInputScope()
    {
        if (_inputRoot is not { } root) return;
        _inputRoot = null;
        root.RemoveHandler(PreviewKeyDownEvent, _previewDown!);
        root.RemoveHandler(PreviewKeyUpEvent, _previewUp!);
    }
    private void OnRootPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_completed || _inputRoot is null) return;
        switch (e.Key)
        {
            case VirtualKey.Enter: e.Handled = true; Commit(); break;
            case VirtualKey.Escape: e.Handled = true; Cancel(); break;
            case VirtualKey.Up: e.Handled = true; Move(-1); break;
            case VirtualKey.Down: e.Handled = true; Move(1); break;
        }
    }
    private void OnRootPreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (!_completed && _inputRoot is not null && e.Key == VirtualKey.Control)
        { e.Handled = true; Commit(); }
    }
}
