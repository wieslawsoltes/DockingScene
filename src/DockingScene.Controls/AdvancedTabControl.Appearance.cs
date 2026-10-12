using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

public partial class AdvancedTabControl
{
    private readonly Border _stripHighlight = new() { IsHitTestVisible = false };
    public static readonly DependencyProperty AreTabEmbeddedButtonsAlwaysVisibleProperty = DependencyProperty.Register(nameof(AreTabEmbeddedButtonsAlwaysVisible), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(true, OptionChanged));
    public static readonly DependencyProperty CanTabsHighlightOnPointerOverWhenInactiveProperty = DependencyProperty.Register(nameof(CanTabsHighlightOnPointerOverWhenInactive), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(false, OptionChanged));
    public static readonly DependencyProperty HighlightThicknessProperty = DependencyProperty.Register(nameof(HighlightThickness), typeof(Thickness), typeof(AdvancedTabControl), new PropertyMetadata(new Thickness(0), OptionChanged));
    public static readonly DependencyProperty HighlightBrushActiveProperty = DependencyProperty.Register(nameof(HighlightBrushActive), typeof(Brush), typeof(AdvancedTabControl), new PropertyMetadata(null, OptionChanged));
    public static readonly DependencyProperty HighlightBrushInactiveProperty = DependencyProperty.Register(nameof(HighlightBrushInactive), typeof(Brush), typeof(AdvancedTabControl), new PropertyMetadata(null, OptionChanged));
    public bool AreTabEmbeddedButtonsAlwaysVisible { get => (bool)GetValue(AreTabEmbeddedButtonsAlwaysVisibleProperty); set => SetValue(AreTabEmbeddedButtonsAlwaysVisibleProperty, value); }
    public bool CanTabsHighlightOnPointerOverWhenInactive { get => (bool)GetValue(CanTabsHighlightOnPointerOverWhenInactiveProperty); set => SetValue(CanTabsHighlightOnPointerOverWhenInactiveProperty, value); }
    public Thickness HighlightThickness { get => (Thickness)GetValue(HighlightThicknessProperty); set { ValidateHighlight(value); SetValue(HighlightThicknessProperty, value); } }
    public Brush? HighlightBrushActive { get => (Brush?)GetValue(HighlightBrushActiveProperty); set => SetValue(HighlightBrushActiveProperty, value); }
    public Brush? HighlightBrushInactive { get => (Brush?)GetValue(HighlightBrushInactiveProperty); set => SetValue(HighlightBrushInactiveProperty, value); }
    private static void ValidateHighlight(Thickness value)
    {
        foreach (double side in new[] { value.Left, value.Top, value.Right, value.Bottom })
            if (!double.IsFinite(side) || side < 0 || side > 1000) throw new ArgumentOutOfRangeException(nameof(value));
    }
    private void RefreshTabAppearance()
    {
        ValidateHighlight(HighlightThickness);
        if (_stripHighlight.Parent is null) _root.Children.Add(_stripHighlight);
        Grid.SetRow(_stripHighlight, Grid.GetRow(_header)); Grid.SetColumn(_stripHighlight, Grid.GetColumn(_header));
        var thickness = HighlightThickness;
        bool visible = IsTabStripVisible && (thickness.Left > 0 || thickness.Top > 0 || thickness.Right > 0 || thickness.Bottom > 0);
        // A zero-width retained border must not leave its previous native paint.
        // Collapse and clear its brush as well as updating the geometric thickness.
        _stripHighlight.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _stripHighlight.BorderThickness = HighlightThickness;
        _stripHighlight.BorderBrush = !visible ? null : IsGroupActive ? HighlightBrushActive ?? DockChrome.Brush(Theme.Accent) : HighlightBrushInactive ?? DockChrome.Brush(Theme.Border);
        DockChrome.Identify(_stripHighlight, "tab-highlight:" + AutomationIdPrefix);
    }
    private void UpdateHeaderActions(HeaderVisual header, AdvancedTabItem item)
    {
        bool show = AreTabEmbeddedButtonsAlwaysVisible || item.Id == _selectedId || header.IsPointerOver || header.HasKeyboardFocus || item.IsPinned || item.IsPreview;
        foreach (var button in header.Actions)
        {
            // Retain slot widths: pointer entry must not move the label or remeasure any tabs.
            button.Opacity = show ? 1 : 0;
            button.IsHitTestVisible = show;
            button.IsTabStop = show && button.Visibility == Visibility.Visible;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(button,
                show ? Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Content : Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        }
    }
}
