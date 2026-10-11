using Uno.UI.Hosting;

namespace DockingScene.App;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            App.ReportStartup("Building WebAssembly host");
            var host = UnoPlatformHostBuilder.Create().App(() => new App()).UseWebAssembly().Build();
            App.ReportStartup("Starting WebAssembly host");
            await host.RunAsync();
            App.ReportStartup("WebAssembly host started");
        }
        catch (Exception error) { App.ReportStartup("WebAssembly host failed", error); throw; }
    }
}
