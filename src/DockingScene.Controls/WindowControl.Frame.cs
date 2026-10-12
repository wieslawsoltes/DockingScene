using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

public partial class WindowControl
{
    private bool _appliedFrameVisible = true;
    public static readonly DependencyProperty IsFrameVisibleProperty = DependencyProperty.Register(nameof(IsFrameVisible), typeof(bool), typeof(WindowControl),
        new PropertyMetadata(true, (d, _) => ((WindowControl)d).ApplyFrameVisibility()));
    /// <summary>Hide the caption, border and resize grips without replacing content.
    /// An external command surface must remain available for a frame-free window.</summary>
    public bool IsFrameVisible { get => (bool)GetValue(IsFrameVisibleProperty); set => SetValue(IsFrameVisibleProperty, value); }
    private void ApplyFrameVisibility()
    {
        bool visible = IsFrameVisible;
        if (!visible && _appliedFrameVisible) { CancelMovement(); DismissSystemMenu(); }
        _appliedFrameVisible = visible;
        if (_root.RowDefinitions.Count == 0) return;
        _root.RowDefinitions[0].Height = new(visible ? _theme.TitleHeight : 0);
        _root.BorderThickness = new(visible ? 1 : 0);
        _title.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        foreach (var grip in _root.Children.OfType<Border>()) grip.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
