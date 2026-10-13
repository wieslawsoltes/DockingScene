using DockingScene.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.PackageValidation;

public static class TabImageConsumer
{
    public static AdvancedTabControl Create(ImageSource image)
    {
        var tabs = new AdvancedTabControl { HasTabImages = true };
        tabs.Items.Add(new AdvancedTabItem { Id = "image", Header = "Image", ImageSource = image });
        return tabs;
    }
    public static DockSite CreateSite() => new() { DocumentWindowsHaveTabImages = true, ToolWindowsHaveTabImages = true };
    public static void ConfigureWindow(DockingWindow window, ImageSource image) => window.ImageSource = image;
}
