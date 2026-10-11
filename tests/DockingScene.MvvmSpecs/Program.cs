using System.Collections.ObjectModel;
using System.ComponentModel;
using DockingScene.Core;
using DockingScene.Mvvm;

int passed = 0, failed = 0;
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
DockManager New()
{
    var manager = new DockManager(); manager.Register(new("document", "Document", WindowKind.Document)).ThrowIfFailed();
    manager.Register(new("tool", "Tool", WindowKind.Tool)).ThrowIfFailed(); return manager;
}
Test("Projection splits document and tool collections", () => {
    using var model = new DockWorkspaceViewModel(New()); Check(model.Windows.Count == 2 && model.Documents.Count == 1 && model.ToolWindows.Count == 1);
});
Test("Metadata binding writes through to the manager", () => {
    using var model = new DockWorkspaceViewModel(New()); var window = model.Find("document")!;
    window.Title = "Updated"; window.IsModified = true;
    Check(model.Manager.Layout.Windows["document"].Title == "Updated"); Check(window.IsModified);
});
Test("Engine metadata changes notify existing view model", () => {
    using var model = new DockWorkspaceViewModel(New()); var window = model.Find("document")!; int notifications = 0;
    window.PropertyChanged += (_, _) => notifications++;
    model.Manager.UpdateWindow("document", value => value with { Title = "Engine" }).ThrowIfFailed();
    Check(ReferenceEquals(window, model.Find("document"))); Check(window.Title == "Engine" && notifications > 0);
});
Test("Closing retains the content and the window view model", () => {
    using var model = new DockWorkspaceViewModel(New()); var window = model.Find("document")!; var content = new object(); window.Content = content;
    window.IsOpen = false; Check(!window.IsOpen); window.IsOpen = true;
    Check(ReferenceEquals(window, model.Find("document"))); Check(ReferenceEquals(window.Content, content));
});
Test("Close veto leaves bound IsOpen unchanged and reports failure", () => {
    using var model = new DockWorkspaceViewModel(New());
    model.Manager.Changing += (_, e) => { if (!e.ClosingWindowIds.IsEmpty) e.Cancel = true; };
    model.Find("document")!.IsOpen = false;
    Check(model.Find("document")!.IsOpen); Check(model.LastResult is { Success: false });
});
Test("Activation binding changes the active window", () => {
    using var model = new DockWorkspaceViewModel(New()); model.Find("document")!.IsActive = true;
    Check(model.ActiveWindow?.Id == "document"); Check(!model.Find("tool")!.IsActive);
});
Test("Capabilities update command enablement", () => {
    using var model = new DockWorkspaceViewModel(New()); var window = model.Find("document")!;
    Check(window.FloatCommand.CanExecute(null)); window.Capabilities &= ~DockCapabilities.Float;
    Check(!window.FloatCommand.CanExecute(null)); window.FloatCommand.Execute(null); Check(window.Placement == WindowPlacement.Document);
});
Test("Layout lock disables structural commands", () => {
    using var model = new DockWorkspaceViewModel(New()); model.IsLayoutLocked = true;
    Check(!model.Find("document")!.FloatCommand.CanExecute(null)); Check(model.Find("document")!.CloseCommand.CanExecute(null));
});
Test("Undo and redo commands project geometry state", () => {
    using var model = new DockWorkspaceViewModel(New()); var window = model.Find("document")!;
    window.FloatCommand.Execute(null); Check(window.Placement == WindowPlacement.Floating);
    model.UndoCommand.Execute(null); Check(window.Placement == WindowPlacement.Document);
    model.RedoCommand.Execute(null); Check(window.Placement == WindowPlacement.Floating);
});
Test("Unregistered proxies reject writes", () => {
    using var model = new DockWorkspaceViewModel(New()); var window = model.Find("document")!;
    model.Manager.Unregister(window.Id).ThrowIfFailed(); Check(!window.IsRegistered); Check(model.Documents.Count == 0);
    try { window.Title = "Invalid"; throw new Exception("Write accepted"); } catch (InvalidOperationException) { }
});
Test("Dispatcher callback can defer observable collection changes", () => {
    var queue = new Queue<Action>(); using var model = new DockWorkspaceViewModel(New(), action => queue.Enqueue(action));
    model.Manager.Register(new("new", "New", WindowKind.Document)).ThrowIfFailed(); Check(model.Find("new") is null);
    while (queue.TryDequeue(out var action)) action(); Check(model.Find("new") is not null);
});
Test("Disposal ignores queued updates and prevents writes", () => {
    var queue = new Queue<Action>(); var manager = New(); var model = new DockWorkspaceViewModel(manager, action => queue.Enqueue(action));
    manager.Close("document").ThrowIfFailed(); model.Dispose(); var before = manager.Layout;
    while (queue.TryDequeue(out var action)) action();
    try { model.IsLayoutLocked = true; throw new Exception("Write accepted"); } catch (ObjectDisposedException) { }
    Check(ReferenceEquals(before, manager.Layout)); Check(!model.UndoCommand.CanExecute(null));
});
Test("Independent source bindings preserve explicit registrations", () => {
    var manager = New(); var documents = new ObservableCollection<Item> { new("a", "A"), new("b", "B") };
    using var binding = new DockSourceBinding<Item>(manager, documents, item => new(item.Id, item.Title, WindowKind.Document));
    Check(manager.Layout.Windows.Count == 4); documents.Clear(); Check(manager.Layout.Windows.Count == 2);
    Check(manager.Layout.Windows.ContainsKey("tool"));
});
Test("Source metadata changes preserve floating placement", () => {
    var manager = New(); var items = new ObservableCollection<Item> { new("a", "A") };
    using var binding = new DockSourceBinding<Item>(manager, items, item => new(item.Id, item.Title, WindowKind.Document));
    manager.Float("a").ThrowIfFailed(); items[0].Title = "Changed";
    Check(manager.Layout.Windows["a"].Title == "Changed"); Check(DockTree.Locate(manager.Layout, "a").Placement == WindowPlacement.Floating);
});
Test("Duplicate source ids do not leak partial registrations", () => {
    var manager = New(); var items = new ObservableCollection<Item> { new("a", "A") };
    using var binding = new DockSourceBinding<Item>(manager, items, item => new(item.Id, item.Title, WindowKind.Document));
    var before = manager.Layout; int failures = 0; binding.SynchronizationFailed += _ => failures++;
    items.Add(new("a", "Duplicate")); Check(ReferenceEquals(before, manager.Layout)); Check(failures == 1);
    items.RemoveAt(1); Check(binding.LastResult.Success);
});
Test("Close veto rejects entire source removal batch", () => {
    var manager = New(); var items = new ObservableCollection<Item> { new("a", "A"), new("b", "B") };
    using var binding = new DockSourceBinding<Item>(manager, items, item => new(item.Id, item.Title, WindowKind.Document));
    manager.Changing += (_, e) => { if (e.ClosingWindowIds.Contains("b")) e.Cancel = true; };
    var before = manager.Layout; items.Clear(); Check(!binding.LastResult.Success); Check(ReferenceEquals(before, manager.Layout));
});
Test("Source collision never takes ownership of explicit windows", () => {
    var manager = New(); var items = new ObservableCollection<Item>();
    using var binding = new DockSourceBinding<Item>(manager, items, item => new(item.Id, item.Title, WindowKind.Document));
    var before = manager.Layout; items.Add(new("document", "Collision"));
    Check(!binding.LastResult.Success); Check(ReferenceEquals(before, manager.Layout));
});
Test("Source bindings expose the original content identity", () => {
    var manager = New(); var item = new Item("a", "A");
    using var binding = new DockSourceBinding<Item>(manager, new[] { item }, value => new(value.Id, value.Title, WindowKind.Document));
    Check(ReferenceEquals(item, binding.Resolve("a")));
});
Test("Source binding disposal unsubscribes without closing windows", () => {
    var manager = New(); var items = new ObservableCollection<Item> { new("a", "A") };
    var binding = new DockSourceBinding<Item>(manager, items, item => new(item.Id, item.Title, WindowKind.Document)); binding.Dispose();
    var before = manager.Layout; items[0].Title = "Unobserved"; items.Clear(); Check(ReferenceEquals(before, manager.Layout));
});
Test("Source batch is a single undoable transaction", () => {
    var manager = New(); var before = manager.Layout;
    using var binding = new DockSourceBinding<Item>(manager, new[] { new Item("a", "A"), new Item("b", "B") }, item => new(item.Id, item.Title, WindowKind.Document));
    Check(manager.Layout.Revision == before.Revision + 1); manager.Undo().ThrowIfFailed(); Check(manager.Layout.Windows.Count == before.Windows.Count);
});
Console.WriteLine($"{passed}/{passed + failed} MVVM specifications passed.");
return failed == 0 ? 0 : 1;

sealed class Item(string id, string title) : INotifyPropertyChanged
{
    private string _title = title;
    public string Id { get; } = id;
    public string Title { get => _title; set { _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
