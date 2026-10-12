using System.Runtime.CompilerServices;
using DockingScene.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.Controls;

public static partial class DockChrome
{
    private static readonly ConditionalWeakTable<FrameworkElement, PositionTransform> Positions = new();

    // Moving a retained frame/container changes its composition transform, not
    // just its arrange offset. The pinned Skia renderer can otherwise replay a
    // collapsed child picture at the old placement while hit testing has moved.
    // Width/height still drive normal layout; existing consumer transforms remain
    // inside the placement transform instead of being discarded or multiplied in.
    internal static void PositionComposed(FrameworkElement element, DockRect rect, int z = 0)
    {
        var position = Positions.GetValue(element, static _ => new PositionTransform());
        position.Attach(element);
        Canvas.SetLeft(element, 0); Canvas.SetTop(element, 0);
        element.Width = Math.Max(0, rect.Width); element.Height = Math.Max(0, rect.Height);
        position.Translation.X = rect.X; position.Translation.Y = rect.Y;
        Canvas.SetZIndex(element, z);
    }

    private sealed class PositionTransform
    {
        internal readonly TranslateTransform Translation = new();
        private Transform? _installed;
        internal void Attach(FrameworkElement element)
        {
            if (ReferenceEquals(element.RenderTransform, _installed) && _installed is not null) return;
            var original = element.RenderTransform;
            if (original is null) _installed = Translation;
            else
            {
                var combined = new TransformGroup();
                combined.Children.Add(original); combined.Children.Add(Translation);
                _installed = combined;
            }
            element.RenderTransform = _installed;
        }
    }
}
