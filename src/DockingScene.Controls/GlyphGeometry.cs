using DockingScene.Themes;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace DockingScene.Controls;

public static partial class DockChrome
{
    private static PathGeometry BuildGlyphGeometry(string data)
    {
        var geometry = new PathGeometry();
        PathFigure? figure = null;
        Point current = default;
        foreach (var segment in VectorPath.Parse(data))
        {
            var end = new Point(segment.X, segment.Y);
            if (segment.Kind == VectorSegmentKind.Move)
            {
                figure = new PathFigure { StartPoint = end, IsFilled = false }; geometry.Figures.Add(figure); current = end; continue;
            }
            if (segment.Kind == VectorSegmentKind.Close)
            {
                if (figure is not null) figure.IsClosed = true;
                current = end; figure = null; continue;
            }
            if (figure is null) { figure = new PathFigure { StartPoint = current, IsFilled = false }; geometry.Figures.Add(figure); }
            switch (segment.Kind)
            {
                case VectorSegmentKind.Line: figure.Segments.Add(new LineSegment { Point = end }); break;
                case VectorSegmentKind.Quadratic: figure.Segments.Add(new QuadraticBezierSegment { Point1 = new(segment.Control1X, segment.Control1Y), Point2 = end }); break;
                case VectorSegmentKind.Cubic: figure.Segments.Add(new BezierSegment { Point1 = new(segment.Control1X, segment.Control1Y), Point2 = new(segment.Control2X, segment.Control2Y), Point3 = end }); break;
                case VectorSegmentKind.Arc: figure.Segments.Add(new ArcSegment { Point = end, Size = new Size(segment.RadiusX, segment.RadiusY), RotationAngle = segment.Rotation, IsLargeArc = segment.LargeArc, SweepDirection = segment.Sweep ? SweepDirection.Clockwise : SweepDirection.Counterclockwise }); break;
            }
            current = end;
        }
        return geometry;
    }
}
