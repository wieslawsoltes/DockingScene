using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace DockingScene.Controls;

/// <summary>Native Uno ComboBox with synchronous popup focus completion. This
/// retains native selection, keyboard, scrolling and item templates; no keys are
/// synthesized or intercepted. Editable combos keep their native text focus.</summary>
public class DockComboBox : ComboBox
{
    private Popup? _popup;
    private bool _completingFocus;
    public DockComboBox() => DropDownOpened += (_, _) => CompletePopupFocus();
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _popup = GetTemplateChild("Popup") as Popup;
    }
    private void CompletePopupFocus()
    {
        if (_completingFocus || IsEditable || !IsDropDownOpen || XamlRoot is not { } root) return;
        _completingFocus = true;
        try
        {
            // A popup lives outside the selector's ordinary visual subtree. Its
            // selected container must be arranged before focus is requested;
            // otherwise immediate Home/Arrow/Enter input can target the old pane.
            (_popup?.Child as FrameworkElement)?.UpdateLayout();
            if (!IsLoaded || !IsDropDownOpen || XamlRoot != root) return;
            int index = SelectedIndex >= 0 ? SelectedIndex : 0;
            if (ContainerFromIndex(index) is Control item && item.IsEnabled && item.IsLoaded && item.Focus(FocusState.Keyboard)) return;
            Focus(FocusState.Keyboard);
        }
        finally { _completingFocus = false; }
    }
}
