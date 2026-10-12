# Floating host drag correctness and retained feedback

## Behavior and public APIs

Actipro's public Docking & MDI overview describes magnetism for floating windows as well as Standard MDI. DockingScene now applies `DockSite.MagnetismSnapDistance` and `MagnetismGapDistance` to floating drag previews and pointer resizing too. Zero disables snapping; Ctrl bypasses it. The existing default distances of 5 and 1 logical units are unchanged. Keyboard Move/Size keeps its existing exact-step behavior.

The new renderer-independent `FloatingDragPlan` freezes source identities, the layout snapshot, original geometry and a self-excluding `WindowMagnetism`. Other source hosts and maximized hosts are excluded from neighbors. Optional displayed bounds account for viewport-clamped positions but must preserve normal-host extents. A plan copies those rectangles and caller identities; later application mutation cannot alter its query results.

A whole normal floating host now starts its preview at the displayed host position with the original size. Pointer release moves the existing host rather than rebuilding it through `FloatMany`. This preserves the host id, split tree, group ids, editor instances and window metadata. Moving requires Drag, not Float or Resize; detaching a subset still requires Float. Stale snapshots, locking, vetoes and invalid geometry reject atomically. A repeated value-equal move remains a no-op and real movement is one undo step.

Previously whole-host dragging derived a new size from one selected group and clamped it, then re-floated all documents. That could jump/shrink the preview and flatten an existing split layout. It also treated the moving source as a magnetic neighbor. The corrected capture revalidates source ownership after activation/capture callbacks. Plans are built only after crossing the pointer movement threshold, not during stationary tab clicks. Pointer borders use a plan for resizing while retaining their existing revision guard.

## Retained visual feedback

`DockDragOverlay` is a public reusable Uno control. It renders the preview and reuses guide visuals by side/location instead of allocating a new visual tree for every pointer event. Position and active-outline updates keep their existing appearance. Suppressing docking hides guides; releasing Ctrl restores the same controls. Cancellation detaches the overlay and clears visible feedback, but the bounded visual pool can be reused for a later drag. `DockSite.DragFeedback` exposes diagnostic counters and `FloatingWindows` provides a read-only view of realized frame controls.

The same geometry engine and Uno Skia renderer remain in use. This removes repeated preview-control/brush creation; it does not remove all allocations in docking-guide hit-testing or promise constant-time hit tests. Magnetism queries remain allocation-free after plan construction. Viewport, setting, layout or manager changes cancel old geometry; Escape and capture loss release the active plan.

## Sample and tests

Window > Open floating drag review opens a real nested DockSite with a split source host and two neighbors. The source disallows Float to distinguish whole-host movement from document detachment. Toolbar actions select snapping and gaps; F9 changes snapping during captured input. Native editors remain ordinary application content.

Fourteen model specifications cover split-root identity, size, capabilities, detach/source exclusion, settings, snapshots, no-op/undo, lock/veto, invalid inputs, mutable callers and displayed geometry. Eight physical-input browser cases cover exact gaps, original preview size, one-step movement, retained editors/frames, Ctrl/zero bypass, border resize, guide reuse, cancellation, viewport changes and stationary clicks. The new cases extend all preceding product, glyph-ink, package, popup and focus gates. Counts describe definitions until the exact source revision passes CI.

## Boundaries

All six packages advance to alpha.14. This remains hosted browser/Uno floating behavior, not native OS windows, monitor integration or full vendor templates. Native/detached hosts, linked sites, remaining contextual tab locations, size-to-content, scrolling MDI, full accessibility/RTL and complete vendor pixel equivalence are unfinished. The floating resize frame previews geometry while the separate hosted document groups retain their prior projection until commit; full live content reflow is not claimed by this increment. CI uses actual Uno Skia through SwiftShader software WebGL, not hardware-GPU measurements.

References: https://www.actiprosoftware.com/docs/controls/wpf/docking/index and https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/standard-mdi . No proprietary source or assets are copied.
