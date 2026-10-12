using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Layout;

namespace DockingScene.PackageValidation;

public static class MagnetismConsumer
{
    public static DockSite Site() => new() { MagnetismSnapDistance = 5, MagnetismGapDistance = 1 };
    public static StandardMdiHost Host() => new() { MagnetismSnapDistance = 0, MagnetismGapDistance = 8 };
    public static DockRect Snap() => new WindowMagnetism(new[] { new DockRect(0,0,100,100) }).Move(new(105,0,100,100));
}
