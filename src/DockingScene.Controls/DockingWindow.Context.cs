using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockingWindow
{
    public static readonly DependencyProperty StandardMdiTitleBarContextContentTemplateProperty = DependencyProperty.Register(
        nameof(StandardMdiTitleBarContextContentTemplate), typeof(DataTemplate), typeof(DockingWindow), new PropertyMetadata(null));
    /// <summary>Contextual controls placed between the Standard MDI caption and
    /// system buttons. The template receives this window's DataContext, except
    /// UIElement values, which are intentionally replaced with null.</summary>
    public DataTemplate? StandardMdiTitleBarContextContentTemplate
    {
        get => (DataTemplate?)GetValue(StandardMdiTitleBarContextContentTemplateProperty);
        set => SetValue(StandardMdiTitleBarContextContentTemplateProperty, value);
    }
}
