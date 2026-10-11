using DockingScene.Layout;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class AdvancedTabControl
{
    private static void OptionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((AdvancedTabControl)sender).QueueRefresh();
    public static readonly DependencyProperty OverflowBehaviorProperty = DependencyProperty.Register(nameof(OverflowBehavior), typeof(TabOverflowBehavior), typeof(AdvancedTabControl), new PropertyMetadata(TabOverflowBehavior.ScrollWithMenu, OptionChanged));
    public static readonly DependencyProperty MinTabExtentProperty = DependencyProperty.Register(nameof(MinTabExtent), typeof(double), typeof(AdvancedTabControl), new PropertyMetadata(60d, OptionChanged));
    public static readonly DependencyProperty MaxTabExtentProperty = DependencyProperty.Register(nameof(MaxTabExtent), typeof(double), typeof(AdvancedTabControl), new PropertyMetadata(300d, OptionChanged));
    public static readonly DependencyProperty IsTabStripVisibleProperty = DependencyProperty.Register(nameof(IsTabStripVisible), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(true, OptionChanged));
    public static readonly DependencyProperty HasTabCloseButtonsProperty = DependencyProperty.Register(nameof(HasTabCloseButtons), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(true, OptionChanged));
    public static readonly DependencyProperty HasTabPinButtonsProperty = DependencyProperty.Register(nameof(HasTabPinButtons), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(false, OptionChanged));
    public static readonly DependencyProperty CanTabsPromoteProperty = DependencyProperty.Register(nameof(CanTabsPromote), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(true, OptionChanged));
    public static readonly DependencyProperty CanTabsCloseOnMiddleClickProperty = DependencyProperty.Register(nameof(CanTabsCloseOnMiddleClick), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(true));
    public TabOverflowBehavior OverflowBehavior { get => (TabOverflowBehavior)GetValue(OverflowBehaviorProperty); set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(OverflowBehaviorProperty, value); } }
    public double MinTabExtent { get => (double)GetValue(MinTabExtentProperty); set { if (!double.IsFinite(value) || value <= 0 || value > MaxTabExtent) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(MinTabExtentProperty, value); } }
    public double MaxTabExtent { get => (double)GetValue(MaxTabExtentProperty); set { if (!double.IsFinite(value) || value < MinTabExtent) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(MaxTabExtentProperty, value); } }
    public bool IsTabStripVisible { get => (bool)GetValue(IsTabStripVisibleProperty); set => SetValue(IsTabStripVisibleProperty, value); }
    public bool HasTabCloseButtons { get => (bool)GetValue(HasTabCloseButtonsProperty); set => SetValue(HasTabCloseButtonsProperty, value); }
    public bool HasTabPinButtons { get => (bool)GetValue(HasTabPinButtonsProperty); set => SetValue(HasTabPinButtonsProperty, value); }
    public bool CanTabsPromote { get => (bool)GetValue(CanTabsPromoteProperty); set => SetValue(CanTabsPromoteProperty, value); }
    public bool CanTabsCloseOnMiddleClick { get => (bool)GetValue(CanTabsCloseOnMiddleClickProperty); set => SetValue(CanTabsCloseOnMiddleClickProperty, value); }
}
