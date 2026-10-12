using DockingScene.Controls;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private void AddIndividualMdiCommands(MenuFlyout menu)
    {
        void Add(string title, string id, bool maximize)
        {
            var item = DockChrome.Identify(new MenuFlyoutItem { Text = title, FontSize = 12, MinHeight = 26,
                IsEnabled = _site.PrimaryMdiHost?.PrimaryWindow is not null }, "mdi-option:" + id);
            item.Click += (_, _) =>
            {
                if (_site.PrimaryMdiHost?.PrimaryWindow is not { } primary) return;
                Execute(_site.Manager.UpdateWindow(primary.Id, w => maximize
                    ? w with { CanStandardMdiMaximize = !w.CanStandardMdiMaximize }
                    : w with { CanStandardMdiMinimize = !w.CanStandardMdiMinimize }));
            };
            menu.Items.Add(item);
        }
        Add("Toggle primary document maximize button", "document-maximize", true);
        Add("Toggle primary document minimize button", "document-minimize", false);
    }
}
