using System.Runtime.CompilerServices;
using DockingScene.Core;

internal static class ToolCaptionConsumer
{
    [ModuleInitializer]
    internal static void Validate()
    {
        var m = new DockManager(); m.Register(new("tool-caption", "Caption", WindowKind.Tool)).ThrowIfFailed();
        m.Float("tool-caption").ThrowIfFailed();
        m.InvokeToolCaptionDoubleClick(DockTree.GroupOf(m.Layout, "tool-caption")!.Id).ThrowIfFailed();
        if (!m.Layout.FloatingHosts[0].IsMaximized || FloatingHostCaption.GetTitle(m.Layout, m.Layout.FloatingHosts[0].Id, "App") != "Caption")
            throw new InvalidOperationException("Packaged tool caption policy failed.");
    }
}
