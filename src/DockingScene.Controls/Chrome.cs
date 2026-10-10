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

public static class DockChrome
{
    public static SolidColorBrush Brush(string hex)
    {
        uint value = uint.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new(Windows.UI.Color.FromArgb(hex.Length > 7 ? (byte)(value >> 24) : (byte)255, (byte)(value >> 16), (byte)(value >> 8), (byte)value));
    }
    public static T Identify<T>(T element, string id, string? name = null) where T : FrameworkElement
    {
        AutomationProperties.SetAutomationId(element, id);
        AutomationProperties.SetName(element, name ?? id);
        return element;
    }
    public static TextBlock Text(string text, DockTheme theme, double? size = null) => new()
    {
        Text = text, FontSize = size ?? theme.FontSize, Foreground = Brush(theme.Text),
        VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
    };
    public static Button Button(string text, string id, DockTheme theme, Action action, string? glyph = null)
    {
        var button = Identify(new Button
        {
            Content = glyph is null ? Text(text, theme) : Glyph(glyph, theme.Text),
            MinWidth = 0, MinHeight = 0, Padding = new(7, 2, 7, 2), Height = 24,
            Background = Brush(theme.Background), Foreground = Brush(theme.Text),
            BorderThickness = new(0), CornerRadius = new(0), FontSize = theme.FontSize
        }, id, text);
        ToolTipService.SetToolTip(button, text);
        button.Click += (_, _) => action();
        return button;
    }
    public static FrameworkElement Glyph(string path, string color, double size = 14)
    {
        var geometry = (Geometry)Microsoft.UI.Xaml.Markup.XamlReader.Load("<PathGeometry xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Figures=\"" + path + "\" />");
        return new Microsoft.UI.Xaml.Shapes.Path { Data = geometry, Stroke = Brush(color), StrokeThickness = 1, Width = size, Height = size, Stretch = Stretch.Uniform };
    }
    public static void Position(FrameworkElement element, DockRect rect, int z = 0)
    {
        Canvas.SetLeft(element, rect.X); Canvas.SetTop(element, rect.Y);
        element.Width = Math.Max(0, rect.Width); element.Height = Math.Max(0, rect.Height);
        Canvas.SetZIndex(element, z);
    }

    /// <summary>Read-only hit-test diagnostics. Popup coordinates share the root coordinate space.
    /// Collapsed ancestors are pruned so retained but non-painted controls never become test targets.</summary>
    public static Dictionary<string, DockRect> AutomationBounds(FrameworkElement root)
    {
        var result = new Dictionary<string, DockRect>(StringComparer.Ordinal);
        var visited = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        if (root.XamlRoot is { } xamlRoot)
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
                if (popup.IsOpen && popup.Child is { } child) stack.Push(child);
        while (stack.TryPop(out var node))
        {
            if (!visited.Add(node)) continue;
            if (node is FrameworkElement element)
            {
                if (element.Visibility != Visibility.Visible || element.Opacity <= 0) continue;
                var id = AutomationProperties.GetAutomationId(element);
                if (!string.IsNullOrEmpty(id) && element.ActualWidth > 0 && element.ActualHeight > 0)
                {
                    var rect = element.TransformToVisual(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                    double left = Math.Max(0, rect.X), top = Math.Max(0, rect.Y);
                    double right = Math.Min(root.ActualWidth, rect.Right), bottom = Math.Min(root.ActualHeight, rect.Bottom);
                    if (right > left && bottom > top) result[id] = new(left, top, right - left, bottom - top);
                }
            }
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++) stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return result;
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
            Owners.Remove(visual);
            Owners.Add(visual, new(presenter));
        }
        presenter.Content = content;
    }
}
