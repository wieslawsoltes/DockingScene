using System.Text.Json.Nodes;
using DockingScene.Core;
using DockingScene.Layout;
using DockingScene.Mvvm;
using DockingScene.Serialization;

int passed = 0, failed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockManager Manager()
{
    var manager = new DockManager();
    manager.RegisterMany(new[] { new DockWindow("a", "A", WindowKind.Document), new DockWindow("b", "B", WindowKind.Document) }).ThrowIfFailed();
    manager.Activate("a").ThrowIfFailed();
    return manager;
}
MdiPrimaryWindowState Resolve(DockManager m) => MdiPrimaryWindow.Resolve(m.Layout, new[] { "a", "b" });

Test("Default window policies preserve existing caption and primary actions", () => {
    var m = Manager(); var state = Resolve(m);
    Check(m.Layout.Windows["a"].CanStandardMdiMinimize && m.Layout.Windows["a"].CanStandardMdiMaximize);
    Check(state.CanMinimize && state.CanMaximize && state.CanClose && !state.CanRestore);
});
Test("Window and host policies intersect independently for every combination", () => {
    var m = Manager();
    for (int flags = 0; flags < 16; flags++) {
        bool min = (flags & 1) != 0, max = (flags & 2) != 0, hostMin = (flags & 4) != 0, hostMax = (flags & 8) != 0;
        m.UpdateWindow("a", w => w with { CanStandardMdiMinimize = min, CanStandardMdiMaximize = max }).ThrowIfFailed();
        var state = MdiPrimaryWindow.Resolve(m.Layout, new[] { "a" }, canMinimize: hostMin, canMaximize: hostMax);
        Check(state.CanMinimize == (min && hostMin) && state.CanMaximize == (max && hostMax));
    }
});
Test("One document's policies never restrict another document", () => {
    var m = Manager();
    m.UpdateWindow("a", w => w with { CanStandardMdiMinimize = false, CanStandardMdiMaximize = false }).ThrowIfFailed();
    Check(!Resolve(m).CanMinimize && !Resolve(m).CanMaximize);
    m.Activate("b").ThrowIfFailed(); Check(Resolve(m).CanMinimize && Resolve(m).CanMaximize);
});
Test("Resize capability remains authoritative when presentation policies allow actions", () => {
    var m = Manager();
    m.UpdateWindow("a", w => w with { Capabilities = DockCapabilities.Drag | DockCapabilities.Close }).ThrowIfFailed();
    var state = Resolve(m); Check(!state.CanMinimize && !state.CanMaximize && state.CanClose);
});
Test("Direct model transitions ignore UI flags and retain restored bounds", () => {
    var m = Manager(); var bounds = m.Layout.Windows["a"].MdiBounds;
    m.UpdateWindow("a", w => w with { CanStandardMdiMinimize = false, CanStandardMdiMaximize = false }).ThrowIfFailed();
    foreach (var state in new[] { MdiWindowState.Minimized, MdiWindowState.Maximized, MdiWindowState.Normal }) {
        m.SetMdiState("a", state).ThrowIfFailed(); Check(m.Layout.Windows["a"].MdiBounds == bounds);
    }
});
Test("Restore remains possible with both UI actions hidden, unless layout is locked", () => {
    var m = Manager();
    m.UpdateWindow("a", w => w with { CanStandardMdiMinimize = false, CanStandardMdiMaximize = false }).ThrowIfFailed();
    m.SetMdiState("a", MdiWindowState.Maximized).ThrowIfFailed();
    var state = Resolve(m); Check(state.CanRestore && !state.CanMaximize && !state.CanMinimize);
    m.SetLayoutLocked(true).ThrowIfFailed(); Check(!Resolve(m).CanRestore && Resolve(m).CanClose);
});
Test("Policies round-trip through bounded layout serialization", () => {
    var m = Manager();
    m.UpdateWindow("a", w => w with { CanStandardMdiMinimize = false, CanStandardMdiMaximize = false }).ThrowIfFailed();
    var result = DockLayoutSerializer.Deserialize(DockLayoutSerializer.Serialize(m.Layout));
    Check(result.Success && !result.Layout!.Windows["a"].CanStandardMdiMinimize && !result.Layout.Windows["a"].CanStandardMdiMaximize);
    Check(result.Layout.Windows["b"].CanStandardMdiMaximize);
});
Test("Earlier JSON without window policy fields loads with enabled defaults", () => {
    var json = JsonNode.Parse(DockLayoutSerializer.Serialize(Manager().Layout))!.AsObject();
    foreach (var pair in json["windows"]!.AsObject()) {
        pair.Value!.AsObject().Remove("canStandardMdiMinimize");
        pair.Value.AsObject().Remove("canStandardMdiMaximize");
    }
    var result = DockLayoutSerializer.Deserialize(json.ToJsonString());
    Check(result.Success && result.Layout!.Windows.Values.All(w => w.CanStandardMdiMinimize && w.CanStandardMdiMaximize));
});
Test("MVVM policy setters write through and preserve proxy identity", () => {
    var m = Manager(); using var vm = new DockWorkspaceViewModel(m);
    var a = vm.Find("a")!; a.CanStandardMdiMinimize = false; a.CanStandardMdiMaximize = false;
    Check(ReferenceEquals(a, vm.Find("a")) && !m.Layout.Windows["a"].CanStandardMdiMinimize && !Resolve(m).CanMaximize);
    m.Undo().ThrowIfFailed(); Check(a.CanStandardMdiMaximize && !a.CanStandardMdiMinimize);
});
Test("Source binding projects both policies without changing source ownership", () => {
    var m = new DockManager(); var item = new PolicyItem();
    using var binding = new DockSourceBinding<PolicyItem>(m, new[] { item },
        x => new("source", "Source", WindowKind.Document) { CanStandardMdiMinimize = x.Minimize, CanStandardMdiMaximize = x.Maximize });
    item.Minimize = item.Maximize = false; binding.Synchronize().ThrowIfFailed();
    Check(ReferenceEquals(item, binding.Resolve("source")));
    Check(!m.Layout.Windows["source"].CanStandardMdiMinimize && !m.Layout.Windows["source"].CanStandardMdiMaximize);
});
Test("Policy changes reuse geometric results and value-equal updates stay no-ops", () => {
    var m = Manager(); var cache = new DockGeometryCache(); var viewport = new DockRect(0,0,1000,700);
    var geometry = cache.Arrange(m.Layout, viewport);
    m.UpdateWindow("a", w => w with { CanStandardMdiMaximize = false }).ThrowIfFailed();
    Check(ReferenceEquals(geometry, cache.Arrange(m.Layout, viewport)));
    var before = m.Layout;
    m.UpdateWindow("a", w => w with { CanStandardMdiMaximize = false }).ThrowIfFailed();
    Check(ReferenceEquals(before, m.Layout) && cache.BuildCount == 1);
});
Test("Both public primary-state APIs use the same individual policy result", () => {
    var m = Manager();
    m.UpdateWindow("a", w => w with { CanStandardMdiMinimize = false }).ThrowIfFailed();
    var oldApi = MdiPrimaryState.Create(m.Layout, new[] { "a", "b" }); var state = Resolve(m);
    Check(oldApi.Window!.Id == state.Id && oldApi.CanMinimize == state.CanMinimize && oldApi.CanMaximize == state.CanMaximize);
});
Console.WriteLine($"{passed}/{passed + failed} individual MDI policy specifications passed.");
return failed == 0 ? 0 : 1;

sealed class PolicyItem
{
    public bool Minimize { get; set; } = true;
    public bool Maximize { get; set; } = true;
}
