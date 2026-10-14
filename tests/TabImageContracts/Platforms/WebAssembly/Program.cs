using Uno.UI.Hosting;

namespace DockingScene.ImageContracts;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try { await UnoPlatformHostBuilder.Create().App(() => new App()).UseWebAssembly().Build().RunAsync(); }
        catch (Exception error) { App.Report(error); throw; }
    }
}
