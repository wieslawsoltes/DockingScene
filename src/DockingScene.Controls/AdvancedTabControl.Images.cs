using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

public partial class AdvancedTabItem
{
    public static readonly DependencyProperty ImageSourceProperty = Property(nameof(ImageSource), typeof(ImageSource), null);
    /// <summary>Application-owned tab image. Not serialized with layout metadata.
    /// The same source may be shared by many tabs without creating their content.</summary>
    public ImageSource? ImageSource { get => (ImageSource?)GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
}

public partial class AdvancedTabControl
{
    public static readonly DependencyProperty HasTabImagesProperty = DependencyProperty.Register(
        nameof(HasTabImages), typeof(bool), typeof(AdvancedTabControl), new PropertyMetadata(false, OptionChanged));
    public bool HasTabImages { get => (bool)GetValue(HasTabImagesProperty); set => SetValue(HasTabImagesProperty, value); }
    private const double TabImageSize = 16, TabImageGap = 4;
    private bool ShowsImage(AdvancedTabItem item) => HasTabImages && item.ImageSource is not null;
    private double ImageExtent(AdvancedTabItem item) => ShowsImage(item) ? TabImageSize + TabImageGap : 0;
    private Image? CreateHeaderImage(AdvancedTabItem item)
    {
        if (!ShowsImage(item)) return null;
        return DockChrome.Identify(new Image
        {
            Source = item.ImageSource, Width = TabImageSize, Height = TabImageSize,
            Stretch = Stretch.Uniform, Margin = new(0, 0, TabImageGap, 0),
            VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
        }, "tab-image:" + item.Id);
    }
    private static void UpdateHeaderImage(HeaderVisual header, AdvancedTabItem item)
    {
        // Source-only changes preserve the image, header and measured text. Presence
        // changes are part of HeaderKey and therefore rebuild only that header.
        if (header.Image is { } image && !ReferenceEquals(image.Source, item.ImageSource)) image.Source = item.ImageSource;
    }
}

public partial class DockSite
{
    public static readonly DependencyProperty DocumentWindowsHaveTabImagesProperty = DependencyProperty.Register(
        nameof(DocumentWindowsHaveTabImages), typeof(bool), typeof(DockSite), new PropertyMetadata(false, TabsChanged));
    public static readonly DependencyProperty ToolWindowsHaveTabImagesProperty = DependencyProperty.Register(
        nameof(ToolWindowsHaveTabImages), typeof(bool), typeof(DockSite), new PropertyMetadata(false, TabsChanged));
    public bool DocumentWindowsHaveTabImages { get => (bool)GetValue(DocumentWindowsHaveTabImagesProperty); set => SetValue(DocumentWindowsHaveTabImagesProperty, value); }
    public bool ToolWindowsHaveTabImages { get => (bool)GetValue(ToolWindowsHaveTabImagesProperty); set => SetValue(ToolWindowsHaveTabImagesProperty, value); }
}
