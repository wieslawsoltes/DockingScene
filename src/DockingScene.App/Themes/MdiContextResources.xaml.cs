using System.Diagnostics.CodeAnalysis;
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

    // The templates deliberately exercise runtime Binding against a UI-neutral
    // referenced assembly. x:DataType alone does not root reflection accessors.
    // Keep the dependency at this UI composition point, not in the model package.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(DockWindowViewModel))]
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
