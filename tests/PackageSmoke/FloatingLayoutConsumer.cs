using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;

internal static class FloatingLayoutConsumer
{
    [ModuleInitializer]
    internal static void Validate()
    {
        var manager = new DockManager();
        manager.Register(new("live-consumer", "Live consumer", WindowKind.Document)).ThrowIfFailed();
        manager.Float("live-consumer").ThrowIfFailed();
        var host = manager.Layout.FloatingHosts[0];
        var plan = new FloatingLayoutPlan(manager.Layout, host.Id);
        var nodes = new DockRect[plan.NodeCount]; var splitters = new DockRect[plan.SplitterCount];
        plan.Arrange(new(20, 40, 300, 200), nodes, splitters);
        if (nodes[0] != new DockRect(21, 64, 298, 175))
            throw new InvalidOperationException("Packaged floating layout geometry differs.");
    }
}
