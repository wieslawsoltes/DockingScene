using System.ComponentModel;
using DockingScene.Core;
using DockingScene.Mvvm;

int passed = 0, failed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockWindow Describe(Item item) => new(item.Id, item.Title, WindowKind.Document);

Test("Unknown-length sources stop at the window budget before excess descriptor work", () => {
    var manager = new DockManager(policy: new() { MaximumWindows = 3 }); var before = manager.Layout;
    int enumerated = 0, described = 0;
    IEnumerable<Item> Source() { for (int i = 0; ; i++) { enumerated++; yield return new("d" + i); } }
    bool rejected = false;
    try { using var binding = new DockSourceBinding<Item>(manager, Source(), item => { described++; return Describe(item); }); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected && enumerated == 4 && described == 3 && ReferenceEquals(before, manager.Layout));
});
Test("A source at the exact window limit commits as one undoable transaction", () => {
    var manager = new DockManager(policy: new() { MaximumWindows = 3 }); int changes = 0;
    manager.Changed += (_, _) => changes++;
    using var binding = new DockSourceBinding<Item>(manager, new[] { new Item("a"), new Item("b"), new Item("c") }, Describe);
    Check(manager.Layout.Windows.Count == 3 && changes == 1);
    manager.Undo().ThrowIfFailed(); Check(manager.Layout.Windows.Count == 0);
});
Test("Disposal inside a descriptor rejects the uncommitted projection", () => {
    var manager = new DockManager(); var items = new List<Item> { new("a") };
    DockSourceBinding<Item>? binding = null; bool dispose = false; int calls = 0;
    binding = new(manager, items, item => { calls++; if (dispose) binding!.Dispose(); return Describe(item); });
    var before = manager.Layout; items.Add(new("b")); dispose = true;
    Check(!binding.Synchronize().Success && ReferenceEquals(before, manager.Layout));
    Check(items[0].SubscriberCount == 0 && items[1].SubscriberCount == 0);
    int count = calls; items[0].Rename("unobserved"); Check(calls == count);
});
Test("Disposal from Changed does not reattach observers after a committed success", () => {
    var manager = new DockManager(); var items = new List<Item> { new("a") };
    var binding = new DockSourceBinding<Item>(manager, items, Describe);
    manager.Changed += (_, _) => binding.Dispose();
    items.Add(new("b")); Check(binding.Synchronize().Success && manager.Layout.Windows.ContainsKey("b"));
    Check(items.All(item => item.SubscriberCount == 0));
    var before = manager.Layout; items[1].Rename("unobserved"); Check(ReferenceEquals(before, manager.Layout));
});
Test("Descriptor-side manager changes are detected without rolling back the callback", () => {
    var manager = new DockManager(); manager.Register(new("explicit", "Before", WindowKind.Tool)).ThrowIfFailed();
    var items = new List<Item> { new("a") }; bool mutate = false;
    using var binding = new DockSourceBinding<Item>(manager, items, item => {
        if (mutate) { mutate = false; manager.UpdateWindow("explicit", w => w with { Title = "Callback" }).ThrowIfFailed(); }
        return Describe(item);
    });
    items.Add(new("b")); mutate = true; var result = binding.Synchronize();
    Check(!result.Success && result.Revision == manager.Layout.Revision);
    Check(manager.Layout.Windows["explicit"].Title == "Callback" && !manager.Layout.Windows.ContainsKey("b"));
    binding.Synchronize().ThrowIfFailed(); Check(manager.Layout.Windows.ContainsKey("b"));
});
Test("Disposal from an event subscription accessor leaves no late subscription", () => {
    var manager = new DockManager(); var items = new List<Item>();
    var binding = new DockSourceBinding<Item>(manager, items, Describe);
    var late = new Item("late") { BeforeSubscribe = binding.Dispose }; items.Add(late);
    Check(binding.Synchronize().Success && manager.Layout.Windows.ContainsKey("late"));
    Check(late.SubscriberCount == 0);
    var before = manager.Layout; late.Rename("unobserved"); Check(ReferenceEquals(before, manager.Layout));
});
Console.WriteLine($"{passed}/{passed + failed} source binding review specifications passed.");
return failed == 0 ? 0 : 1;

sealed class Item(string id) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    public string Id { get; } = id;
    public string Title { get; private set; } = id;
    public Action? BeforeSubscribe { get; init; }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { BeforeSubscribe?.Invoke(); _changed += value; }
        remove { _changed -= value; }
    }
    public void Rename(string title) { Title = title; _changed?.Invoke(this, new(nameof(Title))); }
}
