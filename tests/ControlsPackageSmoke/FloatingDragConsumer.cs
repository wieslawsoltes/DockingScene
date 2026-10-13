using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;

namespace DockingScene.PackageValidation;

public static class FloatingDragConsumer
{
    public static DockDragOverlay CreateOverlay()
    {
        var overlay=new DockDragOverlay();
        overlay.Update(new([],null,new(0,0,200,100)),DockTheme.MetroLight);overlay.ClearPreview();return overlay;
    }
    public static IReadOnlyDictionary<string,WindowControl> Frames(DockSite site)=>site.FloatingWindows;
    public static void EnableLiveLayout(DockSite site) => site.IsLiveFloatingWindowLayoutEnabled = true;
    public static FloatingDragPlan Plan(DockLayout layout,string id)=>new(layout,new[]{id},new(0,0,200,100));
}
