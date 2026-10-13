using System.Runtime.CompilerServices;
using System.Text.Json;
using DockingScene.Controls;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace DockingScene.App;

/// <summary>A nested DockSite with actual split floating hosts. Toolbar and F9 are
/// normal input actions; diagnostics only observe the independent manager.</summary>
public sealed class FloatingDragSample : UserControl
{
    private readonly DockSite _site = new() { Theme = DockTheme.MetroLight };
    private DockManager _manager = new();
    private string _sourceId = "", _neighborId = "";
    private long _generation;
    private readonly Dictionary<string, TextBox> _editors = new(StringComparer.Ordinal);
    private string? _lastError;
    public FloatingDragSample()
    {
        RequestedTheme = ElementTheme.Light;
        var root = new Grid(); Content = root;
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = new(1,GridUnitType.Star) });
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new(4) };
        root.Children.Add(tools); root.Children.Add(_site); Grid.SetRow(_site,1);
        DockChrome.Identify(_site,"float-review:site");
        void Button(string text,string id,Action action) => tools.Children.Add(DockChrome.Button(text,"float-review:"+id,DockTheme.MetroLight,action));
        Button("Reset floating hosts","reset",Reset);
        Button("Disable snapping","off",()=>_site.MagnetismSnapDistance=0);
        Button("Enable snapping (F9)","on",()=>_site.MagnetismSnapDistance=5);
        Button("8-unit gap","gap8",()=>_site.MagnetismGapDistance=8);
        Button("1-unit gap","gap1",()=>_site.MagnetismGapDistance=1);
        Button("Undo","undo",()=>Check(_manager.Undo()));
        Button("Live layout","layout",()=>_site.IsLiveFloatingWindowLayoutEnabled=!_site.IsLiveFloatingWindowLayoutEnabled);
        Button("Light / dark","theme",()=>SetTheme(_site.Theme==DockTheme.MetroLight?DockTheme.MetroDark:DockTheme.MetroLight));
        KeyDown += (_,e) => {
            if(e.Key==VirtualKey.F2) { SetTheme(_site.Theme==DockTheme.MetroLight?DockTheme.MetroDark:DockTheme.MetroLight); e.Handled=true; }
            else if(e.Key==VirtualKey.F9) { _site.MagnetismSnapDistance=_site.MagnetismSnapDistance==0?5:0; e.Handled=true; } };
        _site.ContentFactory = model =>
        {
            var editor = DockChrome.Identify(new TextBox { AcceptsReturn=true, Text="Floating document "+model.Id,
                BorderThickness=new(0), Padding=new(8), FontSize=12, Background=DockChrome.Brush(_site.Theme.EditorBackground),
                Foreground=DockChrome.Brush(_site.Theme.Text), HorizontalAlignment=HorizontalAlignment.Stretch,
                VerticalAlignment=VerticalAlignment.Stretch },"float-review:editor:"+model.Id);
            _editors[model.Id]=editor; return editor;
        };
        _site.OperationRejected += error=>_lastError=error;
        Reset();
    }
    private void Check(DockResult result) { if(!result.Success) _lastError=result.Error; }
    private void SetTheme(DockTheme theme)
    {
        _site.Theme=theme; RequestedTheme=theme==DockTheme.MetroDark?ElementTheme.Dark:ElementTheme.Light;
        foreach(var editor in _editors.Values)
        { editor.Background=DockChrome.Brush(theme.EditorBackground); editor.Foreground=DockChrome.Brush(theme.Text); }
    }
    private void Reset()
    {
        _lastError=null; _editors.Clear(); SetTheme(DockTheme.MetroLight); _site.IsLiveFloatingWindowLayoutEnabled=true;
        var m = new DockManager();
        m.RegisterMany(new[]{"float-a","float-b","float-c","float-d"}.Select(id=>new DockWindow(id,id,WindowKind.Document))).ThrowIfFailed();
        m.FloatMany(new[]{"float-a","float-b"},new(20,40,260,220)).ThrowIfFailed();
        m.Dock("float-b","float-a",DockSide.Bottom).ThrowIfFailed();
        m.Float("float-c",new(330,40,260,220)).ThrowIfFailed();
        m.Float("float-d",new(650,60,240,200)).ThrowIfFailed();
        foreach(string id in new[]{"float-a","float-b"}) m.UpdateWindow(id,w=>w with {Capabilities=DockCapabilities.All & ~DockCapabilities.Float}).ThrowIfFailed();
        m.Activate("float-a").ThrowIfFailed();
        _sourceId=DockTree.Locate(m.Layout,"float-a").HostId!;
        _neighborId=DockTree.Locate(m.Layout,"float-c").HostId!;
        _manager=m; _site.Manager=m; _site.MagnetismSnapDistance=5; _site.MagnetismGapDistance=1; _generation++;
    }
    public void WriteDiagnostics(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("floatingReview");
        writer.WriteNumber("generation",_generation); writer.WriteNumber("revision",_manager.Layout.Revision);
        writer.WriteString("sourceId",_sourceId); writer.WriteString("neighborId",_neighborId); writer.WriteString("error",_lastError);
        writer.WriteBoolean("interaction",_site.IsInteractionActive); writer.WriteBoolean("keyboardScope",_site.HasCapturedGestureKeyboardScope); writer.WriteNumber("snap",_site.MagnetismSnapDistance); writer.WriteNumber("gap",_site.MagnetismGapDistance);
        writer.WriteBoolean("liveLayout",_site.IsLiveFloatingWindowLayoutEnabled);
        writer.WriteNumber("layoutPlans",_site.FloatingLayoutPlanBuildCount);
        writer.WriteNumber("layoutArranges",_site.FloatingLayoutArrangeCount);
        writer.WriteNumber("activeLayoutPlans",_site.ActiveFloatingLayoutPlanCount);
        writer.WriteNumber("fullLayoutBuilds",_site.GeometryBuildCount);
        writer.WriteString("theme",_site.Theme.Name);
        writer.WriteNumber("plans",_site.FloatingDragPlanBuildCount); writer.WriteNumber("queries",_site.FloatingMagnetismQueryCount);
        writer.WriteNumber("feedbackCreated",_site.DragFeedback.VisualCreationCount); writer.WriteNumber("feedbackUpdates",_site.DragFeedback.UpdateCount);
        writer.WriteNumber("feedbackIdentity",_site.DragFeedback.PreviewIdentity); writer.WriteNumber("guideCount",_site.DragFeedback.VisibleGuideCount);
        writer.WriteString("previewTarget",_site.DragPreview?.Target?.TargetId);
        void Rect(string name,DockRect rect)
        { writer.WriteStartObject(name); writer.WriteNumber("x",rect.X); writer.WriteNumber("y",rect.Y); writer.WriteNumber("width",rect.Width); writer.WriteNumber("height",rect.Height); writer.WriteEndObject(); }
        if(_site.DragPreview is { } preview) Rect("preview",preview.FloatingBounds);
        if(_site.RawFloatingDragBounds is { } raw) Rect("raw",raw);
        writer.WriteStartObject("hosts");
        foreach(var host in _manager.Layout.FloatingHosts)
        {
            writer.WriteStartObject(host.Id); Rect("bounds",host.Bounds);
            writer.WriteString("rootId",host.Root.Id); writer.WriteNumber("rootIdentity",RuntimeHelpers.GetHashCode(host.Root));
            writer.WriteStartArray("nodes"); foreach(var node in DockTree.Descendants(host.Root)) writer.WriteStringValue(node.Id); writer.WriteEndArray();
            if(_site.FloatingWindows.TryGetValue(host.Id,out var frame))
            {
                Rect("frame",frame.Bounds); writer.WriteBoolean("moving",frame.IsMoving); writer.WriteBoolean("magnetic",frame.HasPointerMagnetism);
                writer.WriteNumber("frameIdentity",RuntimeHelpers.GetHashCode(frame));
                writer.WriteStartObject("buttons"); foreach(var pair in frame.CaptionButtonIdentities) writer.WriteNumber(pair.Key,pair.Value); writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject(); writer.WriteStartObject("editors");
        foreach(var pair in _editors) writer.WriteNumber(pair.Key,RuntimeHelpers.GetHashCode(pair.Value));
        writer.WriteEndObject();
        writer.WriteStartObject("contents");
        foreach(var (id,editor) in _editors)
        {
            if(!editor.IsLoaded) continue;
            writer.WriteStartObject(id);
            var rect=editor.TransformToVisual(_site).TransformBounds(new Windows.Foundation.Rect(0,0,editor.ActualWidth,editor.ActualHeight));
            Rect("editor",new(rect.X,rect.Y,rect.Width,rect.Height));
            for(DependencyObject? parent=editor;parent is not null;parent=VisualTreeHelper.GetParent(parent))
                if(parent is DockContainer group)
                {
                    var r=group.TransformToVisual(_site).TransformBounds(new Windows.Foundation.Rect(0,0,group.ActualWidth,group.ActualHeight));
                    Rect("group",new(r.X,r.Y,r.Width,r.Height)); writer.WriteString("groupId",group.Group?.Id); break;
                }
            writer.WriteEndObject();
        }
        writer.WriteEndObject(); writer.WriteStartObject("splitters");
        foreach(var (id,rect) in DockChrome.AutomationBounds(_site))
            if(id.StartsWith("splitter:",StringComparison.Ordinal)) Rect(id[9..],rect);
        writer.WriteEndObject(); writer.WriteEndObject();
    }
}
