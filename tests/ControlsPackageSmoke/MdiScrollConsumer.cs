using DockingScene.Controls;
using DockingScene.Layout;

namespace DockingScene.PackageValidation;
public static class MdiScrollConsumer
{
    public static DockSite Site() => new() { IsStandardMdiScrollingEnabled = true };
    public static StandardMdiHost Host() => new() { IsScrollingEnabled = true, AutomationIdPrefix = "consumer-scroll" };
    public static MdiScrollLayout Scroll(StandardMdiHost host) { host.ScrollTo(10, 20); host.ScrollWindowIntoView("document"); return host.ScrollLayout; }
}
