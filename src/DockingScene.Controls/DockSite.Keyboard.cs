using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DockingScene.Controls;

public partial class DockSite
{
    private void ConfigureInput()
    {
        // Child controls may mark pointer events handled. Captured gestures must still
        // receive their terminal event; otherwise their preview survives the physical release.
        _canvas.AddHandler(PointerMovedEvent, new PointerEventHandler((_, e) => MoveInteraction(e)), true);
        _canvas.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, e) => EndInteraction(e)), true);
        _canvas.AddHandler(PointerCanceledEvent, new PointerEventHandler((_, _) => CancelInteraction()), true);
        _canvas.AddHandler(PointerCaptureLostEvent, new PointerEventHandler((_, e) => { TraceInput("CaptureLost", e); if (ReferenceEquals(e.OriginalSource, _canvas) && IsInteractionActive) CancelInteraction(); }), true);
        VisualsUpdated += RenderAutoHideResizeGrip;
        AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
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
        for (int i = 0; i < 9; i++)
        {
            int index = i;
            void ActivateIndex(TabDisplayMode mode)
            {
                var layout = Manager.Layout;
                var group = layout.ActiveWindowId is { } id ? DockTree.GroupOf(layout, id) : null;
                group = group?.Kind == GroupKind.Documents ? group : DockTree.Groups(layout).FirstOrDefault(g => g.Kind == GroupKind.Documents);
                if (group is not null && group.Windows.Count(id => layout.Windows[id].DisplayMode == mode) > index) Submit(Manager.ActivateTab(group.Id, index, mode));
            }
            Shortcut((VirtualKey)((int)VirtualKey.Number1 + i), VirtualKeyModifiers.Control, () => ActivateIndex(TabDisplayMode.Normal));
            Shortcut((VirtualKey)((int)VirtualKey.Number1 + i), VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu, () => ActivateIndex(TabDisplayMode.Pinned));
        }
    }
}
