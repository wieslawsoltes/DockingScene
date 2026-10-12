using DockingScene.Controls;
using DockingScene.Mvvm;
using Microsoft.UI.Xaml;

namespace DockingScene.App;

public sealed partial class MdiContextResources : ResourceDictionary
{
    public MdiContextResources() => InitializeComponent();
    private static void Identify(object sender, string role)
    {
        if (sender is FrameworkElement element && element.DataContext is DockWindowViewModel window)
            DockChrome.Identify(element, "mdi-context:" + role + ":" + window.Id);
    }
    private void NoteLoaded(object sender, RoutedEventArgs e) => Identify(sender, "note");
    private void TrackLoaded(object sender, RoutedEventArgs e) => Identify(sender, "track");
    private void StatusLoaded(object sender, RoutedEventArgs e) => Identify(sender, "status");
}
