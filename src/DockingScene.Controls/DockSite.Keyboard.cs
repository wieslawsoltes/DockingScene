using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DockingScene.Controls;

public partial class DockSite
{
    private void ConfigureInput()
    {
        Unloaded += (_, _) => CloseSwitcher(false);
        _canvas.AddHandler(PointerMovedEvent, new PointerEventHandler((_, e) => MoveInteraction(e)), true);
        _canvas.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, e) => CompletePointerInteraction(e)), true);
        _canvas.AddHandler(PointerCanceledEvent, new PointerEventHandler((_, _) => CancelInteraction()), true);
        _canvas.AddHandler(PointerCaptureLostEvent, new PointerEventHandler((_, e) => { TraceInput("CaptureLost", e); if (IsInteractionActive && _canvas.PointerCaptures?.Any(p => p.PointerId == e.Pointer.PointerId) != true) CancelInteraction(); }), true);
        VisualsUpdated += RenderAutoHideResizeGrip;
        VisualsUpdated += ApplyTabOptions;
        AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            // A modal preview handler may already have moved selection. Do not
            // process the same key again while its bubbling route completes.
            if (e.Handled && _switcher?.HasInputScope == true) return;
            if (e.Handled && _switcher is null && !IsInteractionActive) return;
            if (e.Key == VirtualKey.Escape) { CancelInteraction(); CloseSwitcher(false); CloseAutoHide(); e.Handled = true; }
            else if (_switcher is not null && e.Key == VirtualKey.Enter) { CloseSwitcher(true); e.Handled = true; }
            else if (_switcher is not null && e.Key is VirtualKey.Up or VirtualKey.Down) { _switcher.Move(e.Key == VirtualKey.Up ? -1 : 1); e.Handled = true; }
        }), true);
        AddHandler(KeyUpEvent, new KeyEventHandler((_, e) => { if (e.Key == VirtualKey.Control && _switcher is not null) { CloseSwitcher(true); e.Handled = true; } }), true);
        void Shortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += (_, e) => { action(); e.Handled = true; }; KeyboardAccelerators.Add(accelerator);
        }
        Shortcut(VirtualKey.Tab, VirtualKeyModifiers.Control, () => ShowSwitcher(1));
        Shortcut(VirtualKey.Tab, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => ShowSwitcher(-1));
        Shortcut(VirtualKey.F8, VirtualKeyModifiers.None, () => ShowSwitcher(1));
        Shortcut(VirtualKey.W, VirtualKeyModifiers.Control, () => { if (Manager.Layout.ActiveWindowId is { } id) Submit(Manager.Close(id)); });
        Shortcut(VirtualKey.F4, VirtualKeyModifiers.Control, () => { if (Manager.Layout.ActiveWindowId is { } id) Submit(Manager.Close(id)); });
        Shortcut(VirtualKey.Down, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu, () => ActiveDocumentTabs()?.ShowOverflowMenu());
        Shortcut((VirtualKey)0xBD, VirtualKeyModifiers.Menu, ShowActiveDocumentMenu);
        for (int digit = 1; digit <= 9; digit++)
        {
            int key = digit;
            void ActivateDigit(TabDisplayMode mode)
            {
                var layout = Manager.Layout;
                var group = layout.ActiveWindowId is { } id ? DockTree.GroupOf(layout, id) : null;
                group = group?.Kind == GroupKind.Documents ? group : DockTree.Groups(layout).FirstOrDefault(g => g.Kind == GroupKind.Documents);
                if (group is not null) Submit(Manager.ActivateTabByDigit(group.Id, key, mode));
            }
            Shortcut((VirtualKey)((int)VirtualKey.Number1 + digit - 1), VirtualKeyModifiers.Control, () => ActivateDigit(TabDisplayMode.Normal));
            Shortcut((VirtualKey)((int)VirtualKey.Number1 + digit - 1), VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu, () => ActivateDigit(TabDisplayMode.Pinned));
            Shortcut((VirtualKey)((int)VirtualKey.Number1 + digit - 1), VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => ActivateDigit(TabDisplayMode.Preview));
        }
    }
}
