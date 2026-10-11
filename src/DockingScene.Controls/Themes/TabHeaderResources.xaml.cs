using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

/// <summary>Compiled, directly loaded templates for tab buttons. Does not depend
/// on application resource merging or runtime XAML parsing.</summary>
public sealed partial class TabHeaderResources : ResourceDictionary
{
    public TabHeaderResources() => InitializeComponent();
}
