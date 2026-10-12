using DockingScene.Controls;
using DockingScene.Mvvm;
using Microsoft.UI.Xaml;

namespace DockingScene.App;

public sealed partial class MdiContextResources : ResourceDictionary
{
    public int ProbeLoadedCount { get; private set; }
    internal void ProbeDataChanged(FrameworkElement sender, DataContextChangedEventArgs e)
    { if (e.NewValue is Action action) action(); }
    internal void ProbeLoaded(object sender, RoutedEventArgs e) => ProbeLoadedCount++;
    public MdiContextResources() => InitializeComponent();
    private static void Identify(object sender, string role)
    {
        if (sender is FrameworkElement element && element.DataContext is DockWindowViewModel window)
            DockChrome.Identify(element, "mdi-context:" + role + ":" + window.Id);
    }
    internal void NoteLoaded(object sender, RoutedEventArgs e) => Identify(sender, "note");
    internal void TrackLoaded(object sender, RoutedEventArgs e) => Identify(sender, "track");
    internal void StatusLoaded(object sender, RoutedEventArgs e) => Identify(sender, "status");
}
