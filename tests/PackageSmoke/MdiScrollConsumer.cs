using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;

internal static class MdiScrollConsumer
{
    [ModuleInitializer]
    internal static void Validate()
    {
        var target = new DockRect(-200, -150, 100, 100);
        var layout = MdiScrollLayout.Measure([target], new(600, 400)).Reveal(target);
        if (layout.ToDisplay(target).X < 0 || layout.ToWorld(layout.ToDisplay(target)) != target)
            throw new InvalidOperationException("Packaged MDI scroll geometry failed.");
    }
}
