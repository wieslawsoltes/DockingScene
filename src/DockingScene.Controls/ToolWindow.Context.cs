using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public sealed partial class ToolWindow
{
    private DockContextPresenter? _titleContext;
    internal DockContainer? TitleContextHost { get; set; }
    public static readonly DependencyProperty ToolWindowContainerTitleBarContextContentTemplateProperty = DependencyProperty.Register(
        nameof(ToolWindowContainerTitleBarContextContentTemplate), typeof(DataTemplate), typeof(ToolWindow), new PropertyMetadata(null, TitleTemplateChanged));
    public DataTemplate? ToolWindowContainerTitleBarContextContentTemplate
    {
        get => (DataTemplate?)GetValue(ToolWindowContainerTitleBarContextContentTemplateProperty);
        set => SetValue(ToolWindowContainerTitleBarContextContentTemplateProperty, value);
    }
    public ToolWindow() => DataContextChanged += (_, _) => UpdateTitleContext();
    private static void TitleTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ToolWindow)d).UpdateTitleContext();
    private void UpdateTitleContext()
    {
        if (_titleContext is not null)
        {
            var template = ToolWindowContainerTitleBarContextContentTemplate;
            _titleContext.Update(template is null ? null : DataContext, template);
        }
        TitleContextHost?.RefreshToolTitleContext();
    }
    internal DockContextPresenter? GetTitleContext()
    {
        var template = ToolWindowContainerTitleBarContextContentTemplate;
        if (template is null) return null;
        _titleContext ??= new DockContextPresenter { VerticalAlignment = VerticalAlignment.Center, Margin = new(3, 0, 3, 0) };
        _titleContext.Update(DataContext, template);
        return _titleContext;
    }
}
