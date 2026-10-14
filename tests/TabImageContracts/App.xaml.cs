using System.Text.Json;
using Microsoft.UI.Xaml;

namespace DockingScene.ImageContracts;

public sealed partial class App : Application
{
    private Window? _window;
    public App()
    {
        UnhandledException += (_, e) => Report(e.Exception);
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { _window = new Window { Content = new ImageFixture() }; _window.Activate(); }
        catch (Exception error) { Report(error); throw; }
    }
    internal static void Report(Exception error) => Uno.Foundation.WebAssemblyRuntime.InvokeJS(
        "globalThis.imageContractError=" + JsonSerializer.Serialize(error.ToString()));
}
