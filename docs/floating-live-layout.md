# Live floating layout

## Behavior

Hosted floating frame pointer resizing and keyboard Move/Size now reflow the actual retained document/tool containers, editors and splitters before mouse or Enter release. Previously only the outer frame moved/resized; its sibling content stayed at the committed location until the model operation finished. The model remains unchanged throughout the provisional gesture. A successful completion is one transaction; Escape restores both frame and content geometry without adding history.

`DockSite.IsLiveFloatingWindowLayoutEnabled` defaults to `true`. Turning it off selects frame-only feedback for expensive application content. Changing this option cancels an active gesture. Snap settings, viewport changes, stale model/manager ownership, changed theme metrics, unload and capture cancellation release old plans. A color-only theme refresh preserves the live geometry and retains editors. Whole-host caption docking drags still use the existing translucent docking preview; this feature concerns frame gestures and does not pretend to implement instant provisional tab detachment.

## Reusable measured plan

`FloatingLayoutPlan` is UI-independent and measures only the requested floating host. It freezes each node's constraints, tree indices, split ratios and DPI metrics. Its inputs and output contract are:

```csharp
var plan = new FloatingLayoutPlan(snapshot, hostId, metrics);
var nodes = new DockRect[plan.NodeCount];
var splitters = new DockRect[plan.SplitterCount];
plan.Arrange(displayedFrameBounds, nodes, splitters);
```

`GetNodeId(index)` identifies the node rectangle and `GetSplitterIndex(index)` returns that node's splitter slot, or -1. The caller retains its buffers; two concurrent users must supply separate buffers. The immutable plan performs no model mutation, viewport coercion or application callback. The same constrained split arithmetic as `DockLayoutEngine` handles minimum shortages, maximum excess, fractional DPI, nested workspaces and mixed tool/document groups. Input identities, size arithmetic, metrics, excessive depth, buffer sizes and overlapping output spans are validated. Construction is bounded to 32,768 nodes/members and depth 64.

The control adapter captures existing container/splitter references once, then updates their native geometry. It does not recreate editor content, rebuild the global layout, or allocate immutable replacement snapshots for each pointer event. Plan arrangement is allocation-free after construction; native control property setters, text layout and diagnostics are outside that claim. This is not a hardware-GPU/FPS benchmark or a claim that layout of arbitrary application content is free.

## Validation

Twelve new model specifications include 1,000 seeded direct comparisons with `DockLayoutEngine`, fractional DPI, undersized/maximized/displayed geometry, concurrent use, bounded invalid input and a warm 20,000-arrangement allocation check. Eight additional real-input browser scenarios exercise live horizontal/vertical resizing, raw-pointer reversal, single-transaction commit/undo, Escape rollback, keyboard Move/Size, unchanged/reentrant completion, frame-only mode, color-only refresh and settings/viewport cancellation. They observe actual arranged editor/group/splitter bounds, stable identities, plan/full-layout counters, and painted pixels.

All earlier scenarios and the separate 100-cycle focus gate remain required. Source test counts are definitions until that exact revision's results pass. Diagnostics remain read-only and disabled during normal application use. Local source inspection is not substituted for actual Uno WebAssembly execution.

## Reference and boundaries

Public reference: https://www.actiprosoftware.com/docs/controls/wpf/docking/docking-window-features/floating-dock-hosts distinguishes hosted WindowControl behavior and the performance tradeoff of live versus highlight-only drag feedback. This increment is an independent hosted-layout implementation, not Actipro source reuse or exact WPF API compatibility.

Native OS/detached hosts, instant floated-tab detachment, linked sites, monitor/taskbar integration, size-to-content, additional contextual-tab positions and complete accessibility/RTL/pixel parity remain unfinished. CI renders real Uno Skia with SwiftShader software WebGL, not hardware-GPU measurement.

## Reentrant completion ownership

An application can start another keyboard movement from `BoundsChanged`. Each frame gesture now carries a monotonic completion version, so the old completion's cleanup cannot remove the newer gesture's plan or revision token. The sample's F3 shortcut arms one follow-on keyboard move to demonstrate this event composition. A physical-keyboard regression commits both moves, verifies the second remains live, then undoes the two separate transactions. Reset and unload remove the optional sample subscription.
