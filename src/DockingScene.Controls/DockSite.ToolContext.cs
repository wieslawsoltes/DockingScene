using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public partial class DockSite
{
    public static readonly DependencyProperty ToolWindowsTitleBarContextContentAlignmentProperty = DependencyProperty.Register(
        nameof(ToolWindowsTitleBarContextContentAlignment), typeof(ContextContentAlignment), typeof(DockSite),
        new PropertyMetadata(ContextContentAlignment.Far, (d, _) => ((DockSite)d).QueueRender()));
    public ContextContentAlignment ToolWindowsTitleBarContextContentAlignment
    {
        get => (ContextContentAlignment)GetValue(ToolWindowsTitleBarContextContentAlignmentProperty);
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); SetValue(ToolWindowsTitleBarContextContentAlignmentProperty, value); }
    }
    public IEnumerable<(string WindowId, DockContextPresenter Presenter)> ToolTitleContexts
    {
        get
        {
            foreach (var view in _groups.Values)
                if (view.Group?.SelectedId is { } id && view.TitleBarContextPresenter is { } presenter) yield return (id, presenter);
            if (_autoPopup?.Group?.SelectedId is { } autoId && _autoPopup.TitleBarContextPresenter is { } autoPresenter) yield return (autoId, autoPresenter);
        }
    }
}
