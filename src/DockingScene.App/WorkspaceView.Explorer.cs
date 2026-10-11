using DockingScene.Controls;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private FrameworkElement Explorer()
    {
        var tree = new TreeView
        {
            SelectionMode = TreeViewSelectionMode.Single, FontSize = 12,
            Foreground = DockChrome.Brush(_theme.Text),
            CanDragItems = false, CanReorderItems = false, AllowDrop = false,
            ItemTemplate = (DataTemplate)new ExplorerResources()["SourceTreeNodeContent"]
        };
        // RootNodes mode passes TreeViewNode to ItemTemplate. The stock template
        // formats Content as text; a ContentPresenter is required for vector labels.
        // Keep native expansion, selection, keyboard navigation and virtualization.
        tree.Resources["TreeViewItemMinHeight"] = 22d;
        tree.Resources["TreeViewItemPadding"] = new Thickness(0);
        tree.Resources["TreeViewItemPresenterPadding"] = new Thickness(0);
        tree.Resources["TreeViewItemPresenterMargin"] = new Thickness(0);
        var style = new Style { TargetType = typeof(TreeViewItem) };
        style.Setters.Add(new Setter(Control.MinHeightProperty, 22d));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        tree.ItemContainerStyle = style;
        var targets = new Dictionary<TreeViewNode, string>();
        var labels = new List<(TextBlock Text, Microsoft.UI.Xaml.Shapes.Path Icon)>();
        tree.RegisterPropertyChangedCallback(Control.ForegroundProperty, (_, _) =>
        {
            // Collapsed node labels are not descendants of the realized tree.
            // Update them too, so expanding after a theme change cannot reveal
            // stale colors. No label or icon is recreated by this callback.
            foreach (var label in labels) { label.Text.Foreground = tree.Foreground; label.Icon.Stroke = tree.Foreground; }
        });
        object Label(string text, string glyph, string id)
        {
            var row = DockChrome.Identify(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Height = 22, VerticalAlignment = VerticalAlignment.Center }, "source-tree:" + id);
            var icon = DockChrome.Identify(DockChrome.Glyph(glyph, _theme.Text, 13), "source-tree-icon:" + id);
            var caption = DockChrome.Identify(DockChrome.Text(text, _theme), "source-tree-text:" + id, text);
            labels.Add((caption, icon)); row.Children.Add(icon); row.Children.Add(caption); return row;
        }
        var root = new TreeViewNode { Content = Label("Solution 'DockingScene' (7 projects)", DockGlyphs.Folder, "solution"), IsExpanded = true };
        foreach (string package in new[] { "Core", "Layout", "Serialization", "Mvvm", "Themes", "Controls", "App" })
        {
            var project = new TreeViewNode { Content = Label("DockingScene." + package, DockGlyphs.Folder, package), IsExpanded = package is "Core" or "Controls" };
            foreach (string name in package switch
            {
                "Core" => new[] { "DockManager.cs", "DockTree.cs", "Model.cs", "DockLayoutValidator.cs" },
                "Controls" => new[] { "DockSite.cs", "AdvancedTabControl.cs", "StandardMdiHost.cs", "WindowControl.cs" },
                "Layout" => new[] { "DockLayoutEngine.cs", "WindowGeometry.cs", "TabStripMetrics.cs", "DockGuides.cs" },
                "Serialization" => new[] { "DockLayoutSerializer.cs" }, "Mvvm" => new[] { "DockWorkspaceViewModel.cs", "DockSourceBinding.cs" },
                "Themes" => new[] { "DockTheme.cs", "VectorPath.cs" }, _ => new[] { "App.xaml", "MainWindow.xaml" }
            })
            {
                var file = new TreeViewNode { Content = Label(name, DockGlyphs.Document, name) };
                project.Children.Add(file); targets[file] = name.EndsWith(".xaml", StringComparison.Ordinal) ? "main" : "code";
            }
            root.Children.Add(project);
        }
        tree.RootNodes.Add(root);
        tree.ItemInvoked += (_, e) => { if (e.InvokedItem is TreeViewNode node && targets.TryGetValue(node, out var id)) Execute(_site.Manager.Open(id)); };
        return tree;
    }
}
