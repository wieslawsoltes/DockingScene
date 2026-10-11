# Review follow-up: bulk registration, selection and modal input

This continues the package-level review in `performance-review.md`. It adds targeted production fixes and executable regressions. It does not certify full vendor conformance, native hosts, all screen readers, every browser or pixel parity.

## Atomic bulk registration

`DockManager.RegisterMany(windows, open, targetId, side, expectedRevision)` registers the input in one engine transaction, avoiding validation and copying of every intermediate registered-window snapshot. A nonempty accepted batch creates one revision, one notification and one undo entry. Invalid metadata, duplicate/existing ids, cancellation and stale revisions leave the prior state unchanged. Enumeration is bounded by the remaining registry capacity, so an unlimited enumerable is rejected after at most capacity plus one elements.

Open batches contain one window kind and activate the first input item; closed batches may mix documents and tools. Empty batches preserve identity and redo. The new API does not silently change the ownership or synchronization contract of DockSourceBinding. Use it explicitly for bulk registration rather than assuming all source operations are now batched efficiently.

```csharp
manager.RegisterMany(
    Enumerable.Range(0, 1000).Select(i =>
        new DockWindow($"document-{i}", $"Document {i}", WindowKind.Document)),
    expectedRevision: manager.Layout.Revision).ThrowIfFailed();
```

The specification suite checks 10,000 windows, undo/redo and bounded enumeration. Its 500-window allocation benchmark compares individual registration with the public batch API; both produce all requested metadata. Timing is observational and environment-dependent. The allocation budget, not a machine-specific elapsed time, is the regression gate.

## Coalesced headers without skipped content callbacks

Tab selection and selected-content creation remain synchronous. Only header presentation for intermediate selections is queued and coalesced. Keyboard header navigation flushes presentation before requesting focus. Renames and same-id replacements retain their one-header measurement behavior. The large sample still creates 200 editors during 200 different selections; it no longer needs to construct the off-screen headers for every unpainted intermediate state.

Factories are application callbacks. A selection made inside a factory supersedes the outer request. A factory exception preserves the previously selected item and content. Recursive creation of the same item throws a controlled InvalidOperationException rather than overflowing the stack. These behaviors are exercised with visible buttons operating actual Uno controls, not JavaScript model mutation.

## Modal input and gesture cancellation

The previous switcher scope registered key handlers before Loaded but could leave focus in the old editor until Loaded. CharacterReceived is independent of a handled KeyDown. The switcher now takes actual focus immediately after attachment and again when handling Enter, without requiring IsLoaded. A fallback applies layout only if normal focus acquisition fails. Enter confirmation still completes on release so its character dispatch cannot become document input. Read-only diagnostics distinguish an attached input scope from actual focus ownership.

The new regression repeats twelve immediate editor/F8/Enter transitions. The held-key case checks that focus is physically inside the overlay before release. Existing editor newline behavior remains tested after dismissal.

Docking, splitter and auto-hide gestures now cancel when their captured model revision becomes obsolete. Auto-hide resizing starts only after successful capture. Standalone window movement is cancelled on unload. Standard MDI adds identical frame chrome to both minimum and maximum content constraints, preventing inverted fixed-size limits.

## Input and storage guards

WindowGeometry rejects nonfinite inputs, malformed edges, invalid resize directions, inverted constraints, unknown MDI modes and duplicate/empty MDI ids. Tiling computes dimension ratios before multiplication to avoid avoidable intermediate overflow.

DockLayoutStream accepts well-formed UTF-8, optionally with a UTF-8 BOM counted in the byte budget. Invalid byte sequences are rejected instead of silently changing titles or identities. The source stream remains caller-owned. File-store saves check cancellation before serialization, and the semaphore is released even if temporary-file cleanup throws.

## Remaining scope

The index still costs linear time and memory per changed immutable snapshot. Complete candidate validation remains mandatory. Some DockSite auxiliary controls are recreated, and geometry is not an incremental GPU solver. Native/detached windows, linked sites, advanced vendor serialization/reopen semantics, comprehensive ItemsSource/template behavior, edge-autoscrolling, touch inertia, accessibility/RTL and certified visual parity remain unfinished. No prior blocked profile or bookmark extension was retried.
