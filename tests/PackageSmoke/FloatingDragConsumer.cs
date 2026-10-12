using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;

internal static class FloatingDragConsumer
{
    [ModuleInitializer]
    internal static void Validate()
    {
        var manager=new DockManager();manager.Register(new("floating-consumer","Floating consumer",WindowKind.Document)).ThrowIfFailed();
        manager.Float("floating-consumer").ThrowIfFailed();var host=manager.Layout.FloatingHosts[0];
        var plan=new FloatingDragPlan(manager.Layout,new[]{"floating-consumer"},new(0,0,100,100));
        plan.Commit(manager,plan.InitialBounds with {X=30}).ThrowIfFailed();
        if(manager.Layout.FloatingHosts[0].Id!=host.Id || !ReferenceEquals(manager.Layout.FloatingHosts[0].Root,host.Root))
            throw new InvalidOperationException("Packaged floating drag lost host identity.");
    }
}
