using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;

internal static class MagnetismConsumer
{
    [ModuleInitializer]
    internal static void Validate()
    {
        var plan = new WindowMagnetism(new[] { new DockRect(0,0,100,200) });
        if (plan.Move(new(105,50,100,100)).X != 101)
            throw new InvalidOperationException("Package-only magnetism consumer failed.");
    }
}
