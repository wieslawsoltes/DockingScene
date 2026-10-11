using Microsoft.UI.Xaml;
using DockingScene.Core;

namespace DockingScene.Controls;

public partial class DockSite
{
    public static readonly DependencyProperty AreDocumentTabEmbeddedButtonsAlwaysVisibleProperty = DependencyProperty.Register(nameof(AreDocumentTabEmbeddedButtonsAlwaysVisible), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public static readonly DependencyProperty HasToolTabCloseButtonsProperty = DependencyProperty.Register(nameof(HasToolTabCloseButtons), typeof(bool), typeof(DockSite), new PropertyMetadata(true, TabsChanged));
    public static readonly DependencyProperty HasDocumentTabStripHighlightProperty = DependencyProperty.Register(nameof(HasDocumentTabStripHighlight), typeof(bool), typeof(DockSite), new PropertyMetadata(false, TabsChanged));
    public bool AreDocumentTabEmbeddedButtonsAlwaysVisible { get => (bool)GetValue(AreDocumentTabEmbeddedButtonsAlwaysVisibleProperty); set => SetValue(AreDocumentTabEmbeddedButtonsAlwaysVisibleProperty, value); }
    public bool HasToolTabCloseButtons { get => (bool)GetValue(HasToolTabCloseButtonsProperty); set => SetValue(HasToolTabCloseButtonsProperty, value); }
    public bool HasDocumentTabStripHighlight { get => (bool)GetValue(HasDocumentTabStripHighlightProperty); set => SetValue(HasDocumentTabStripHighlightProperty, value); }
    public IReadOnlyDictionary<string, int[]> CaptionButtonIdentities => _groups.Values.Where(g => g.Group is not null)
        .ToDictionary(g => g.Group!.Id, g => g.CaptionButtonIdentities.ToArray());
    private void ApplyTabAppearance(AdvancedTabControl tabs)
    {
        tabs.AreTabEmbeddedButtonsAlwaysVisible = tabs.IsToolGroup || AreDocumentTabEmbeddedButtonsAlwaysVisible;
        tabs.HasTabCloseButtons = !tabs.IsToolGroup || HasToolTabCloseButtons;
        tabs.HighlightThickness = !tabs.IsToolGroup && HasDocumentTabStripHighlight ? tabs.Placement switch
        {
            TabPlacement.Bottom => new(0, 1, 0, 0),
            TabPlacement.Left => new(0, 0, 1, 0),
            TabPlacement.Right => new(1, 0, 0, 0),
            _ => new(0, 0, 0, 1)
        } : new(0);
    }
}
