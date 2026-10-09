using Microsoft.UI.Xaml;
namespace DockingScene.App;
public sealed partial class App : Application
{
    private Window? _window;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "DockingScene — Docking & MDI", Content = new WorkspaceView() };
        _window.Activate();
    }
}
