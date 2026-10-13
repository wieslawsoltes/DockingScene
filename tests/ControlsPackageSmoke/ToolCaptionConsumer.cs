using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;

namespace DockingScene.PackageValidation;
public static class ToolCaptionConsumer
{
    public static DockSite Create() => new()
    {
        ToolWindowsHaveCloseButtons = false, ToolWindowsHaveOptionsButtons = false,
        ToolWindowsHaveToggleAutoHideButtons = true,
        FloatingToolWindowContainerTitleBarDoubleClickMode = FloatingWindowTitleBarDoubleClickMode.Dock,
        FloatingWindowTitle = "Consumer", FloatingWindowTitleDelimiter = " | "
    };
    public static void Update(ToolWindowContainer view, DockGroup group, DockLayout layout)
        => view.Update(group, Array.Empty<AdvancedTabItem>(), layout, DockTheme.MetroLight,
            captionOptions: new ToolCaptionOptions(false, true, false));
}
