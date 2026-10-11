using DockingScene.Controls;
using DockingScene.Core;

namespace DockingScene.PackageValidation;

public static class ControlConsumer
{
    public static DockSite Create(DockManager manager) => new()
    {
        Manager = manager,
        ContentFactory = window => new Microsoft.UI.Xaml.Controls.TextBox
        {
            AcceptsReturn = true, Text = window.Title
        }
    };
}
