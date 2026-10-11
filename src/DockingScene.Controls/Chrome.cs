using System.Globalization;
using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace DockingScene.Controls;

public static partial class DockChrome
{
    public static SolidColorBrush Brush(string hex)
    {
        uint value = uint.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new(Windows.UI.Color.FromArgb(hex.Length > 7 ? (byte)(value >> 24) : (byte)255, (byte)(value >> 16), (byte)(value >> 8), (byte)value));
    }
    public static T Identify<T>(T element, string id, string? name = null) where T : FrameworkElement
    {
        AutomationProperties.SetAutomationId(element, id); AutomationProperties.SetName(element, name ?? id); return element;
    }
    public static TextBlock Text(string text, DockTheme theme, double? size = null) => new()
    {
        Text = text, FontSize = size ?? theme.FontSize, Foreground = Brush(theme.Text), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
    };
    public static Button Button(string text, string id, DockTheme theme, Action action, string? glyph = null)
    {
        var button = Identify(new Button
        {
            Content = glyph is null ? Text(text, theme) : Glyph(glyph, theme.Text), MinWidth = 0, MinHeight = 0, Padding = new(7, 2, 7, 2), Height = 24,
            Background = Brush(theme.Background), Foreground = Brush(theme.Text), BorderThickness = new(0), CornerRadius = new(0), FontSize = theme.FontSize
        }, id, text);
        ToolTipService.SetToolTip(button, text); button.Click += (_, _) => action(); return button;
    }
    public static FrameworkElement Glyph(string path, string color, double size = 14) => new Microsoft.UI.Xaml.Shapes.Path
    {
        Data = BuildGlyphGeometry(path), Stroke = Brush(color), StrokeThickness = 1, Width = size, Height = size, Stretch = Stretch.Uniform
    };
    public static void Position(FrameworkElement element, DockRect rect, int z = 0)
    {
        Canvas.SetLeft(element, rect.X); Canvas.SetTop(element, rect.Y);
        element.Width = Math.Max(0, rect.Width); element.Height = Math.Max(0, rect.Height); Canvas.SetZIndex(element, z);
    }
    /// <summary>Read-only hit rectangles, clipped by ancestor scrolling viewports and
    /// explicit rectangle clips. This does not attempt sibling-occlusion analysis.</summary>
    public static Dictionary<string, DockRect> AutomationBounds(FrameworkElement root)
    {
        var result = new Dictionary<string, DockRect>(StringComparer.Ordinal);
        var visited = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<(DependencyObject Node, DockRect Clip)>();
        var viewport = new DockRect(0, 0, root.ActualWidth, root.ActualHeight);
        stack.Push((root, viewport));
        if (root.XamlRoot is { } xamlRoot)
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
                if (popup.IsOpen && popup.Child is { } child) stack.Push((child, viewport));
        while (stack.TryPop(out var entry))
        {
            var node = entry.Node; var clip = entry.Clip;
            if (!visited.Add(node) || clip.Width <= 0 || clip.Height <= 0) continue;
            if (node is FrameworkElement element)
            {
                if (element.Visibility != Visibility.Visible || element.Opacity <= 0) continue;
                var transform = element.TransformToVisual(root);
                var rect = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                if (element.Clip is RectangleGeometry explicitClip)
                {
                    var r = transform.TransformBounds(explicitClip.Rect);
                    clip = Intersect(clip, new(r.X, r.Y, r.Width, r.Height));
                }
                var painted = Intersect(clip, new(rect.X, rect.Y, rect.Width, rect.Height));
                var id = AutomationProperties.GetAutomationId(element);
                if (!string.IsNullOrEmpty(id) && painted.Width > 0 && painted.Height > 0) result[id] = painted;
                if (element is ScrollViewer) clip = painted;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                stack.Push((VisualTreeHelper.GetChild(node, i), clip));
        }
        return result;
    }
    private static DockRect Intersect(DockRect a, DockRect b)
    {
        double x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
        return new(x, y, Math.Max(0, Math.Min(a.Right, b.Right) - x), Math.Max(0, Math.Min(a.Bottom, b.Bottom) - y));
    }

}

/// <summary>A visual can have only one presenter. Reparenting retains the actual editor/control instance.</summary>
internal static class ContentLease
{
    private static readonly ConditionalWeakTable<UIElement, WeakReference<ContentPresenter>> Owners = new();
    public static void Set(ContentPresenter presenter, object? content)
    {
        if (ReferenceEquals(presenter.Content, content)) return;
        if (presenter.Content is UIElement old) Owners.Remove(old);
        presenter.Content = null;
        if (content is UIElement visual)
        {
            if (Owners.TryGetValue(visual, out var reference) && reference.TryGetTarget(out var previous)) previous.Content = null;
            Owners.Remove(visual); Owners.Add(visual, new(presenter));
        }
        presenter.Content = content;
    }
}
