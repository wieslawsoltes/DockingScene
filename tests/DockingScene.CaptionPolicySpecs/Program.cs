using System.Text.Json.Nodes;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Mvvm;
using DockingScene.Serialization;

int passed = 0, failed = 0;
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockManager Manager()
{
    var m = new DockManager();
    m.RegisterMany(new[] { new DockWindow("a", "Explorer", WindowKind.Tool), new DockWindow("b", "Properties", WindowKind.Tool) }).ThrowIfFailed();
    m.FloatMany(new[] { "a", "b" }, new(20, 30, 500, 400)).ThrowIfFailed();
    return m;
}
bool?[] values = [null, false, true];
string[] fields = ["hasTitleBar", "hasCloseButton", "hasOptionsButton", "hasToggleAutoHideButton"];
ToolCaptionSettings Get(DockWindow w, ToolCaptionSettings defaults) => defaults.Resolve(w);

Test("Nullable instance defaults preserve the default visible caption", () => {
    var w = new DockWindow("a", "A", WindowKind.Tool);
    Check(w.HasTitleBar is null && w.HasCloseButton is null && w.HasOptionsButton is null && w.HasToggleAutoHideButton is null);
    Check(Get(w, ToolCaptionSettings.Default) == new ToolCaptionSettings(true, true, true, true));
    var (id, title, kind) = w; Check(id == "a" && title == "A" && kind == WindowKind.Tool);
});
Test("Every explicit instance combination overrides every global default combination", () => {
    foreach (var title in values) foreach (var close in values) foreach (var options in values) foreach (var pin in values)
    for (int mask = 0; mask < 16; mask++) {
        var d = new ToolCaptionSettings((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0);
        var w = new DockWindow("a", "A", WindowKind.Tool) { HasTitleBar = title, HasCloseButton = close, HasOptionsButton = options, HasToggleAutoHideButton = pin };
        Check(Get(w, d) == new ToolCaptionSettings(title ?? d.HasTitleBar, close ?? d.HasCloseButton, options ?? d.HasOptionsButton, pin ?? d.HasToggleAutoHideButton));
    }
});
Test("Resolution is pure and rejects null metadata", () => {
    var m = Manager(); var before = m.Layout;
    _ = Get(before.Windows["a"], new(false, false, false, false)); Check(ReferenceEquals(before, m.Layout));
    try { _ = ToolCaptionSettings.Default.Resolve(null!); throw new Exception("Expected null rejection"); }
    catch (ArgumentNullException) { }
});
Test("All nullable instance values round-trip through the source-generated serializer", () => {
    var m = Manager();
    foreach (var title in values) foreach (var close in values) foreach (var options in values) foreach (var pin in values) {
        m.UpdateWindow("a", w => w with { HasTitleBar = title, HasCloseButton = close, HasOptionsButton = options, HasToggleAutoHideButton = pin }).ThrowIfFailed();
        var result = DockLayoutSerializer.Deserialize(DockLayoutSerializer.Serialize(m.Layout));
        var loaded = result.Layout ?? throw new Exception(result.Error);
        var w = loaded.Windows["a"];
        Check(w.HasTitleBar == title && w.HasCloseButton == close && w.HasOptionsButton == options && w.HasToggleAutoHideButton == pin);
    }
});
Test("Missing legacy fields, null, false and true retain distinct inheritance semantics", () => {
    var original = DockLayoutSerializer.Serialize(Manager().Layout);
    for (int combinations = 0; combinations < 256; combinations++) {
        var json = JsonNode.Parse(original)!.AsObject(); var w = json["windows"]!["a"]!.AsObject();
        var expected = new bool?[4];
        for (int i = 0; i < 4; i++) {
            int value = (combinations >> (i * 2)) & 3;
            expected[i] = value < 2 ? null : value == 3;
            if (value == 0) w.Remove(fields[i]);
            else if (value == 1) w[fields[i]] = null;
            else w[fields[i]] = value == 3;
        }
        var result = DockLayoutSerializer.Deserialize(json.ToJsonString()); var loaded = result.Layout ?? throw new Exception(result.Error);
        var a = loaded.Windows["a"];
        Check(a.HasTitleBar == expected[0] && a.HasCloseButton == expected[1] && a.HasOptionsButton == expected[2] && a.HasToggleAutoHideButton == expected[3]);
    }
});
Test("Malformed policies cannot partially restore a layout", () => {
    var m = Manager(); var before = m.Layout;
    foreach (string field in fields) {
        var json = JsonNode.Parse(DockLayoutSerializer.Serialize(before))!.AsObject();
        json["windows"]!["a"]![field] = "not-a-boolean";
        Check(!DockLayoutSerializer.Restore(m, json.ToJsonString()).Success && ReferenceEquals(before, m.Layout));
    }
});
Test("MVVM exposes all policies with stable identities, undo and inheritance reset", () => {
    var m = Manager(); using var vm = new DockWorkspaceViewModel(m); var a = vm.Find("a")!;
    a.HasTitleBar = false; a.HasCloseButton = false; a.HasOptionsButton = true; a.HasToggleAutoHideButton = false;
    Check(ReferenceEquals(a, vm.Find("a")) && Get(m.Layout.Windows["a"], new()) == new ToolCaptionSettings(false, false, true, false));
    m.Undo().ThrowIfFailed(); Check(a.HasToggleAutoHideButton is null);
    a.HasTitleBar = a.HasCloseButton = a.HasOptionsButton = null;
    Check(Get(m.Layout.Windows["a"], new()) == new ToolCaptionSettings());
});
Test("Source descriptors project overrides without overwriting placement or geometric state", () => {
    var m = new DockManager(); var item = new CaptionItem();
    using var source = new DockSourceBinding<CaptionItem>(m, new[] { item }, x => new("source", "Source", WindowKind.Tool) {
        HasTitleBar = x.Title, HasCloseButton = x.Close, HasOptionsButton = x.Options, HasToggleAutoHideButton = x.Pin });
    m.Float("source", new(60, 70, 460, 330)).ThrowIfFailed(); var location = DockTree.Locate(m.Layout, "source"); var rect = m.Layout.FloatingHosts[0].Bounds;
    item.Title = false; item.Close = false; item.Options = true; item.Pin = false; source.Synchronize().ThrowIfFailed();
    Check(Get(m.Layout.Windows["source"], new()) == new ToolCaptionSettings(false, false, true, false));
    Check(DockTree.Locate(m.Layout, "source") == location && m.Layout.FloatingHosts[0].Bounds == rect && ReferenceEquals(source.Resolve("source"), item));
    item.Title = item.Close = item.Options = item.Pin = null; source.Synchronize().ThrowIfFailed();
    Check(m.Layout.Windows["source"].HasTitleBar is null && m.Layout.Windows["source"].HasOptionsButton is null);
});
Test("Button-only policies do not invalidate cached geometry", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var bounds = new DockRect(0, 0, 1000, 700);
    var first = cache.Arrange(m.Layout, bounds);
    m.UpdateWindow("a", w => w with { HasCloseButton = false, HasOptionsButton = false, HasToggleAutoHideButton = false }).ThrowIfFailed();
    Check(ReferenceEquals(first, cache.Arrange(m.Layout, bounds)) && cache.BuildCount == 1);
    var before = m.Layout; m.UpdateWindow("a", w => w with { HasOptionsButton = false }).ThrowIfFailed(); Check(ReferenceEquals(before, m.Layout));
});
Test("Equivalent explicit and inherited title defaults reuse geometry", () => {
    foreach (bool titleDefault in new[] { false, true }) {
        var m = Manager(); var cache = new DockGeometryCache(); var bounds = new DockRect(0, 0, 1000, 700);
        var metrics = new DockMetrics { ToolWindowsHaveTitleBars = titleDefault }; var first = cache.Arrange(m.Layout, bounds, metrics);
        m.UpdateWindow("a", w => w with { HasTitleBar = titleDefault }).ThrowIfFailed();
        Check(ReferenceEquals(first, cache.Arrange(m.Layout, bounds, metrics)) && cache.BuildCount == 1);
        m.UpdateWindow("a", w => w with { HasTitleBar = !titleDefault }).ThrowIfFailed();
        Check(!ReferenceEquals(first, cache.Arrange(m.Layout, bounds, metrics)) && cache.BuildCount == 2);
    }
});
Test("Global title policy changes invalidate geometry and agree with direct layout", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var bounds = new DockRect(0, 0, 600, 380);
    var first = cache.Arrange(m.Layout, bounds); var metrics = new DockMetrics { ToolWindowsHaveTitleBars = false };
    var changed = cache.Arrange(m.Layout, bounds, metrics); var direct = DockLayoutEngine.Arrange(m.Layout, bounds, metrics);
    Check(!ReferenceEquals(first, changed) && cache.BuildCount == 2);
    Check(changed.Nodes.Count == direct.Nodes.Count && changed.Nodes.All(p => direct.Nodes[p.Key] == p.Value));
});
Test("Floating measurement resolves defaults and preserves the maximum title envelope", () => {
    var m = Manager(); string host = m.Layout.FloatingHosts[0].Id;
    var visible = new FloatingLayoutPlan(m.Layout, host); var hidden = new FloatingLayoutPlan(m.Layout, host, new() { ToolWindowsHaveTitleBars = false });
    Check(visible.ContentMeasure.Minimum.Height - hidden.ContentMeasure.Minimum.Height == new DockMetrics().TitleHeight);
    m.UpdateWindow("b", w => w with { HasTitleBar = true }).ThrowIfFailed();
    var explicitTitle = new FloatingLayoutPlan(m.Layout, host, new() { ToolWindowsHaveTitleBars = false });
    Check(explicitTitle.ContentMeasure == visible.ContentMeasure);
    m.UpdateWindow("a", w => w with { HasTitleBar = false }).ThrowIfFailed();
    Check(new FloatingLayoutPlan(m.Layout, host).ContentMeasure == visible.ContentMeasure);
});
Test("Forcing a close or pin button visible never grants missing capabilities", () => {
    var m = Manager(); m.UpdateWindow("a", w => w with { Capabilities = DockCapabilities.None, HasCloseButton = true, HasToggleAutoHideButton = true }).ThrowIfFailed();
    var before = m.Layout; var resolved = Get(before.Windows["a"], new(false, false, false, false));
    Check(resolved.HasCloseButton && resolved.HasToggleAutoHideButton);
    Check(!m.Close("a").Success && !m.AutoHide("a", DockSide.Left).Success && ReferenceEquals(before, m.Layout));
});
Test("Hidden presentation does not remove direct commands and closing vetoes remain authoritative", () => {
    var m = Manager(); m.UpdateWindow("a", w => w with { HasCloseButton = false }).ThrowIfFailed();
    var before = m.Layout;
    EventHandler<DockChangingEventArgs> veto = (_, e) => { if (e.ClosingWindowIds.Contains("a")) e.Cancel = true; };
    m.Changing += veto; Check(!m.Close("a").Success && ReferenceEquals(before, m.Layout));
    m.Changing -= veto; m.Close("a").ThrowIfFailed(); Check(DockTree.Locate(m.Layout, "a").Placement == WindowPlacement.Hidden);
});
Test("Policy changes preserve layout revisions, veto, and undo contracts", () => {
    var m = Manager(); var before = m.Layout;
    EventHandler<DockChangingEventArgs> veto = (_, e) => e.Cancel = true;
    m.Changing += veto; Check(!m.UpdateWindow("a", w => w with { HasOptionsButton = false }).Success && ReferenceEquals(before, m.Layout));
    m.Changing -= veto;
    m.UpdateWindow("a", w => w with { HasOptionsButton = false }).ThrowIfFailed(); Check(m.Layout.Revision == before.Revision + 1);
    m.Undo().ThrowIfFailed(); Check(m.Layout.Windows["a"].HasOptionsButton is null);
});
Test("Warm resolution is allocation-free across 100000 independent calls", () => {
    var w = new DockWindow("a", "A", WindowKind.Tool) { HasOptionsButton = false, HasCloseButton = true }; var defaults = new ToolCaptionSettings();
    for (int i = 0; i < 1000; i++) _ = defaults.Resolve(w);
    long bytes = GC.GetAllocatedBytesForCurrentThread(); int count = 0;
    for (int i = 0; i < 100000; i++) if (defaults.Resolve(w).HasCloseButton) count++;
    long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
    Check(count == 100000 && allocated == 0);
});
Console.WriteLine($"{passed}/{passed + failed} caption inheritance specifications passed.");
return failed == 0 ? 0 : 1;

sealed class CaptionItem
{
    public bool? Title { get; set; }
    public bool? Close { get; set; }
    public bool? Options { get; set; }
    public bool? Pin { get; set; }
}
