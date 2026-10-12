using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;

namespace DockingScene.PackageValidation;

public static class MdiFrameConsumer
{
    public static StandardMdiControlBar Create(DockManager manager)
    {
        var host = new StandardMdiHost { AreMaximizedWindowFramesVisible = false };
        host.Update(manager, manager.Layout.Windows.Keys.ToArray(), id => new AdvancedTabItem { Id = id, Header = id }, DockTheme.MetroLight);
        _ = host.ClosePrimaryWindowCommand.CanExecute(null);
        _ = host.RestorePrimaryWindowCommand.CanExecute(null);
        return new() { Host = host, ShowOnlyWhenMaximized = true, AutomationIdPrefix = "consumer-primary" };
    }
}
