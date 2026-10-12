using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

public partial class WindowControl
{
    private readonly List<Border> _resizeGrips = [];
    public static readonly DependencyProperty IsFrameVisibleProperty = DependencyProperty.Register(nameof(IsFrameVisible),
        typeof(bool), typeof(WindowControl), new PropertyMetadata(true, (d, _) => ((WindowControl)d).ApplyFrameVisibility()));
    /// <summary>Hide the title and frame without detaching the retained content.
    /// A host hiding the frame must provide external window commands.</summary>
    public bool IsFrameVisible { get => (bool)GetValue(IsFrameVisibleProperty); set => SetValue(IsFrameVisibleProperty, value); }
    private void ApplyFrameVisibility()
    {
        if (_root.RowDefinitions.Count == 0) return;
        if (!IsFrameVisible) { CancelMovement(); DismissSystemMenu(); }
        _title.Visibility = IsFrameVisible ? Visibility.Visible : Visibility.Collapsed;
        _root.RowDefinitions[0].Height = new(IsFrameVisible ? _theme.TitleHeight : 0);
        _root.BorderThickness = new(IsFrameVisible ? 1 : 0);
        foreach (var grip in _resizeGrips) grip.Visibility = IsFrameVisible ? Visibility.Visible : Visibility.Collapsed;
    }
}
