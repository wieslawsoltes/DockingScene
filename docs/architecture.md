# Architecture and contracts

## Dependency direction

`Core` owns model identity and transactions. `Layout` consumes immutable Core snapshots and computes rectangles. `Serialization` consumes Core and encodes layout metadata. `Mvvm` observes Core and adapts application collections. `Themes` contains independent presentation data and vector-path parsing. `Controls` composes Core, Layout and Themes using Uno primitives. The sample App adds content, persistence choices and diagnostics.

There is no JavaScript docking implementation hidden behind the Uno sample. JavaScript in browser validation observes the running managed application; normal commands execute in the .NET engine.

## Transactions, cancellation and events

`DockManager.Layout` is an immutable snapshot. Every mutating command builds a candidate, performs bounded structural validation, repairs activation, validates the complete result and raises `Changing`. Only an accepted result advances the revision. Invalid input and ordinary capability failures return `DockResult` without partially changing the model.

```csharp
manager.Changing += (_, e) =>
{
    if (e.ClosingWindowIds.Contains("unsaved-document"))
    {
        e.Cancel = true;
        e.Reason = "Save or explicitly discard the document first.";
    }
};

var revision = manager.Layout.Revision;
manager.Transaction("Reconfigure workspace", new Func<DockManager, DockResult>[]
{
    m => m.Float("editor"),
    m => m.AutoHide("explorer", DockSide.Right)
}, revision).ThrowIfFailed();
```

A batch uses a private trial manager. A rejected intermediate operation cancels the whole batch. The real manager emits one event, records one undo entry and advances one revision. Closing-window detection applies to restore, undo, preview replacement and batches, not just explicit `Close` commands.

`Changing` is synchronous and runs within the engine's transaction gate. Reentrant mutation during that event is rejected. It is intended for immediate policy checks, not asynchronous dialogs or long work. Applications should complete asynchronous save/discard prompts before issuing a command and can use an expected revision to reject stale decisions.

`Changed` notifications are queued in commit order and dispatched outside the transaction gate. An observer may issue another command without causing later observers to see reversed revisions. Observer exceptions are reported through `ObserverFaulted` and cannot turn a committed command into a false failure. Error sinks are isolated from one another.

Undo/redo journals are updated before observers are notified. A new structural command clears redo. Activation is not recorded as a structural history entry. The default history limit is 100; retained immutable snapshots are bounded by the configured history limit.

## Validation boundaries

The validator walks trees iteratively, checks depth before descending, rejects duplicate/cyclic node identities, and enforces window and node budgets. Floating/MDI bounds must be finite, positive and have finite right/bottom edges. Window identity is unique; each registered window has exactly one location. Documents cannot occupy tool-only containers. A primary layout has at most one workspace; nesting uses another DockSite, not recursively nested primary workspace nodes.

Restoring a caller-created layout validates structure before invoking activation repair or recursive tree transforms. JSON decoding additionally limits input bytes and parser depth. These guards do not constitute a security review of every Uno/Skia dependency.

## MVVM and content ownership

```csharp
using var workspace = new DockWorkspaceViewModel(manager,
    action => dispatcher.TryEnqueue(() => action()));

workspace.Find("editor")!.Title = "Updated.cs";
workspace.Find("editor")!.IsReadOnly = true;
workspace.Find("editor")!.FloatCommand.Execute(null);
```

`DockWorkspaceViewModel` maintains read-only observable collections for all windows, documents and tools. Window proxy identity remains stable through moves, floating, hiding and reopening. `IsOpen`, `IsActive`, title, capabilities and other metadata write through to validated engine commands. A closing veto leaves the bound model open and reports the rejected result.

The optional dispatcher callback must schedule actions on the consumer UI dispatcher. The default callback executes inline, useful for UI-independent consumers and tests. Direct engine events may be raised on the calling thread. The engine is synchronized, but Uno visuals and observable collections must still be used on their owning dispatcher.

`DockSourceBinding<T>` synchronizes application-owned collections and item property notifications using a single batch. It never takes ownership of explicitly registered windows. Duplicate IDs and collisions are rejected. A removal veto leaves the manager unchanged even though the external collection may already have changed; the binding reports `SynchronizationFailed`. Reconcile the source or call `Synchronize` after resolving the conflict. Two-way automatic removal of application items following a user close is not implemented.

The application owns content and view-model lifetime. The controls cache actual content lazily by window ID and reparent the same visual through a single-presenter lease. Hiding does not destroy the content. Unregistering removes the control cache entry. Application content and view models are not serialized. The MVVM adapters do not dispose their content. DockSite invokes `IDisposable.Dispose` on factory-created content when its window is unregistered; factories must account for that ownership rule when returning shared objects. Dispose MVVM adapters and source bindings when their application owner is finished with them.

## Layout and interaction

The layout package computes constrained splits independently of rendering. Splitter gestures preview geometry and commit once on release, guarded by the captured revision. Tab headers use measured Uno text widths and `TabStripMetrics` binary searches for virtualization and insertion positions; the model never guesses insertion based on character count. Header keyboard navigation is scoped to headers so caret movement inside editors is not consumed.

Hosted floating frames and standard MDI windows are controls inside a DockSite. They are not separate OS windows. Floating-to-docked moves reuse the same window identity and content. Escape restores provisional movement. Native desktop window hosting, multi-monitor coordinates and linked-site ownership transfer need a separate host adapter and remain unfinished.

Auto-hide popups use the same window content as docked groups. Their inner resize edge commits `AutoHideSize`; model locking and resize capabilities reject inappropriate changes. Hover activation and delayed closing are UI behavior, not serialized document content.

## Persistence

`DockLayoutSerializer` writes versioned JSON with source-generated metadata and immutable model types. Unknown windows can be preserved, ignored or rejected using `LayoutLoadOptions`. Deserialization validates the complete result; restoring uses the manager transaction boundary. `FileDockLayoutStore` writes through a temporary file and an atomic move under a semaphore.

This format is DockingScene's own schema. Actipro XML import/export, tool-only reconciliation, delayed creation during load, per-window `CanSerialize`, extensible custom serializer fields and exact reopen-position compatibility are not implemented.

## Rendering

The controls use Uno layout, input, text and Skia drawing. `VectorPath` parses bounded SVG path data into neutral segments; `GlyphGeometry` maps those segments into explicit Uno path figures. This avoids runtime reflection/XAML path conversion in trimmed browser builds. No Direct3D, Vulkan, WebGPU or hardware-GPU throughput claim is made by the software-rendered Chromium CI run.
