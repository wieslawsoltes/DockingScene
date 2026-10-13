using DockingScene.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DockingScene.Controls;

public partial class WindowControl
{
    private WindowKeyboardSession? _keyboardSession;
    private bool _keyboardCommitPending, _keyboardWasTabStop;
    public bool IsKeyboardInteractionActive => _keyboardSession is not null;
    public WindowKeyboardMode? KeyboardInteractionMode => _keyboardSession?.Mode;
    /// <summary>Start a provisional, arrow-driven move. Enter release commits once;
    /// Escape, pointer interaction, unload and stale-host cancellation restore.</summary>
    public bool BeginKeyboardMove() => BeginKeyboardInteraction(WindowKeyboardMode.Move);
    /// <summary>Start sizing. The first arrow on an axis selects its edge, then
    /// subsequent arrows resize. The untouched opposite edge stays anchored.</summary>
    public bool BeginKeyboardResize() => BeginKeyboardInteraction(WindowKeyboardMode.Resize);
    private bool AllowsKeyboard(WindowKeyboardMode mode) => IsLoaded && IsFrameVisible && !IsMaximized && !IsMinimized &&
        (mode == WindowKeyboardMode.Move ? CanMove : CanResize);
    private bool BeginKeyboardInteraction(WindowKeyboardMode mode)
    {
        if (_moving || !AllowsKeyboard(mode) || XamlRoot?.Content is null) return false;
        Activated?.Invoke();
        if (_moving || !AllowsKeyboard(mode) || XamlRoot?.Content is null) return false;
        DismissSystemMenu();
        _original = Bounds; _keyboardSession = new(Bounds, mode); _keyboardCommitPending = false;
        _keyboardWasTabStop = IsTabStop; IsTabStop = true;
        _moving = true; AcquireMovementInput(); Focus(FocusState.Programmatic);
        MovementStarted?.Invoke();
        return _moving && _keyboardSession is not null;
    }
    private void HandleMovementKey(KeyRoutedEventArgs e)
    {
        if (!_moving) return;
        if (e.Key == VirtualKey.Escape) { e.Handled = true; CancelMovement(); return; }
        if (_keyboardSession is not { } session) return;
        if (!AllowsKeyboard(session.Mode)) { CancelMovement(); return; }
        if (e.Key == VirtualKey.Enter) { e.Handled = true; _keyboardCommitPending = true; return; }
        WindowKeyboardDirection? direction = e.Key switch
        {
            VirtualKey.Left => WindowKeyboardDirection.Left, VirtualKey.Right => WindowKeyboardDirection.Right,
            VirtualKey.Up => WindowKeyboardDirection.Up, VirtualKey.Down => WindowKeyboardDirection.Down, _ => null
        };
        if (direction is null || _keyboardCommitPending) return;
        e.Handled = true;
        if (session.Step(direction.Value, 8, MinimumSize, MaximumSize))
        { SetBounds(session.Bounds); BoundsChanging?.Invoke(Bounds); }
    }
    private void CompleteKeyboardInteraction()
    {
        if (_keyboardSession is not { } session) return;
        if (!AllowsKeyboard(session.Mode)) { CancelMovement(); return; }
        var final = session.Complete(true);
        bool changed = final != _original;
        _moving = false; ResetKeyboardInteraction(); ReleaseMovementInput();
        try { if (changed) BoundsChanged?.Invoke(final); }
        finally { MovementEnded?.Invoke(); }
    }
    private void ResetKeyboardInteraction()
    {
        if (_keyboardSession is null) return;
        _keyboardSession = null; _keyboardCommitPending = false; IsTabStop = _keyboardWasTabStop;
    }
}
