using System.Text.Json;
using Microsoft.UI.Xaml;

namespace DockingScene.App;

public sealed partial class App : Application
{
    private Window? _window;
    public App()
    {
        ReportStartup("Application constructor");
        UnhandledException += (_, e) => ReportStartup("Unhandled application exception", e.Exception);
        try { InitializeComponent(); ReportStartup("Application resources initialized"); }
        catch (Exception error) { ReportStartup("Application resources failed", error); throw; }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            ReportStartup("Creating application window");
            _window = new Window { Title = "DockingScene — Docking & MDI" };
            ReportStartup("Creating docking workspace");
            var view = new WorkspaceView();
            view.EnableTabDemo();
            view.EnableHostReview();
            view.Loaded += (_, _) => ReportStartup("Workspace loaded");
            _window.Content = view;
            ReportStartup("Activating application window");
            _window.Activate();
            ReportStartup("Application window activated");
        }
        catch (Exception error) { ReportStartup("Application launch failed", error); throw; }
    }
    internal static void ReportStartup(string stage, Exception? error = null)
    {
        Console.WriteLine("DockingScene: " + stage + (error is null ? "" : "\n" + error));
#if BROWSER_WASM
        Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.dockingSceneStartup = {stage:" + JsonSerializer.Serialize(stage) + ",error:" + JsonSerializer.Serialize(error?.ToString()) + "}; console.info('DockingScene startup', globalThis.dockingSceneStartup);");
#endif
    }
}
