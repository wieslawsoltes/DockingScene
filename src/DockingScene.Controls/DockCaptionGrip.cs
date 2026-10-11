using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace DockingScene.Controls;

/// <summary>Reusable two-row Metro title-bar grip. Two retained dashed vector lines
/// cover any width without allocating one control or figure per dot.</summary>
public sealed class DockCaptionGrip : UserControl
{
    private readonly Line[] _lines = [new(), new()];
    public DockCaptionGrip()
    {
        var canvas = new Canvas { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)) };
        Content = canvas; IsTabStop = false;
        foreach (var line in _lines)
        {
            line.StrokeThickness = 1;
            line.StrokeDashArray = new DoubleCollection { 1, 3 };
            line.IsHitTestVisible = false; canvas.Children.Add(line);
        }
        SizeChanged += (_, _) => ArrangeLines();
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => { foreach (var line in _lines) line.Stroke = Foreground; });
        Loaded += (_, _) => { foreach (var line in _lines) line.Stroke = Foreground; ArrangeLines(); };
    }
    private void ArrangeLines()
    {
        double middle = Math.Floor(ActualHeight / 2) + .5;
        for (int i = 0; i < _lines.Length; i++)
        {
            var line = _lines[i]; line.X1 = i * 2; line.X2 = Math.Max(line.X1, ActualWidth - 2);
            line.Y1 = line.Y2 = Math.Max(.5, middle - 2 + i * 4);
        }
    }
}
