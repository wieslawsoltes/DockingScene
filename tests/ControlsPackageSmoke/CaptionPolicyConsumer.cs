using DockingScene.Controls;
using DockingScene.Core;

namespace DockingScene.PackageValidation;

public static class CaptionPolicyConsumer
{
    public static DockSite Create() => new() { ToolWindowsHaveTitleBars = false };
    public static ToolCaptionSettings Resolve()
    {
        // Retain the original three-argument constructor and deconstruction.
        var options = new ToolCaptionOptions(false, false, false) { HasTitleBar = false };
        var (close, menu, pin) = options;
        return options.Resolve(new DockWindow("instance", "Instance", WindowKind.Tool)
        { HasTitleBar = true, HasOptionsButton = true, HasCloseButton = close, HasToggleAutoHideButton = null });
    }
}
