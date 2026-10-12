using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingScene.Controls;

/// <summary>Reusable, non-interactive docking feedback. At most nine guide visuals
/// and one preview are created; pointer updates move/repaint retained controls.</summary>
public sealed class DockDragOverlay : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly Border _preview = new() { BorderThickness = new(2), Opacity = .28 };
    private readonly Dictionary<(DockSide Side, bool Outer), GuideVisual> _guides = new();
    private DockTheme? _theme;
    private long _generation;
    public long UpdateCount { get; private set; }
    public int VisualCreationCount => 1 + _guides.Count;
    public int PreviewIdentity => RuntimeHelpers.GetHashCode(_preview);
    public int VisibleGuideCount { get; private set; }
    public DockRect? PreviewBounds { get; private set; }
    public DockDragOverlay()
    {
        IsTabStop = false; IsHitTestVisible = false;
        Content = _canvas; _canvas.Children.Add(_preview); ClearPreview();
    }
    public void Update(DockDragPreview preview, DockTheme theme)
    {
        ArgumentNullException.ThrowIfNull(preview); ArgumentNullException.ThrowIfNull(theme);
        if(preview.Guides.IsDefault || preview.Guides.Length>9) throw new ArgumentException("A docking preview supports at most nine guides.",nameof(preview));
        var rect = preview.Target?.Preview ?? preview.FloatingBounds;
        ValidateRect(rect); int seen=0;
        foreach(var guide in preview.Guides)
        {
            if(guide is null || !Enum.IsDefined(guide.Side) || guide.IsOuter && guide.Side==DockSide.Center)
                throw new ArgumentException("Invalid docking guide.",nameof(preview));
            int bit=1<<((int)guide.Side+(guide.IsOuter?5:0));
            if((seen&bit)!=0) throw new ArgumentException("Duplicate docking guide location.",nameof(preview));
            seen|=bit; ValidateRect(guide.Bounds);
        }
        // All caller-supplied geometry is checked before altering the projection.
        UpdateCount++; _generation++;
        if (_theme != theme)
        {
            _theme = theme;
            _preview.Background = DockChrome.Brush(theme.Accent); _preview.BorderBrush = _preview.Background;
        }
        _preview.Visibility = Visibility.Visible; DockChrome.Position(_preview, rect);
        PreviewBounds = rect; VisibleGuideCount = 0;
        foreach (var guide in preview.Guides)
        {
            var key = (guide.Side, guide.IsOuter);
            if (!_guides.TryGetValue(key, out var visual))
            {
                visual = new(guide.Side); _guides.Add(key, visual); _canvas.Children.Add(visual.Frame);
            }
            visual.Generation = _generation; VisibleGuideCount++;
            visual.Update(guide, guide == preview.Target, theme);
        }
        foreach (var visual in _guides.Values)
            if (visual.Generation != _generation) visual.Frame.Visibility = Visibility.Collapsed;
    }
    private static void ValidateRect(DockRect rect)
    {
        if(!double.IsFinite(rect.X) || !double.IsFinite(rect.Y) || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height) ||
            !double.IsFinite(rect.Right) || !double.IsFinite(rect.Bottom) || rect.Width<0 || rect.Height<0)
            throw new ArgumentException("Invalid docking feedback rectangle.");
    }
    public void ClearPreview()
    {
        _preview.Visibility = Visibility.Collapsed; PreviewBounds = null; VisibleGuideCount = 0;
        foreach (var visual in _guides.Values) visual.Frame.Visibility = Visibility.Collapsed;
    }
    private sealed class GuideVisual
    {
        internal readonly Border Frame = new();
        private readonly Grid _body = new();
        private readonly Border _outline = new() { BorderThickness = new(1), Margin = new(5) };
        private readonly Border _area = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        private DockTheme? _theme;
        private bool _active;
        internal long Generation;
        internal GuideVisual(DockSide side)
        {
            Frame.Child = _body; _body.Children.Add(_outline); _body.Children.Add(_area);
            var area = DockGuides.Preview(new(5,7,18,16), side);
            _area.Margin = new(area.X,area.Y,0,0); _area.Width = Math.Max(4,area.Width); _area.Height = Math.Max(4,area.Height);
        }
        internal void Update(DockGuide guide, bool active, DockTheme theme)
        {
            if (_theme != theme || _active != active)
            {
                _theme = theme; _active = active;
                _body.Background = DockChrome.Brush(theme.GuideBackground); _outline.BorderBrush = DockChrome.Brush(theme.Border);
                _area.Background = DockChrome.Brush(theme.Accent);
                Frame.BorderBrush = active ? _area.Background : _outline.BorderBrush;
                Frame.BorderThickness = new(active ? 2 : 1);
            }
            Frame.Visibility = Visibility.Visible; DockChrome.Position(Frame,guide.Bounds,2);
        }
    }
}
