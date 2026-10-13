# Standard MDI scrolling

`StandardMdiHost.IsScrollingEnabled` is an opt-in Uno dependency property, default false. `DockSite.IsStandardMdiScrollingEnabled` forwards the choice to its primary MDI host. With scrolling disabled, the previous keep-visible placement and existing application layout remain unchanged.

## Coordinates and behavior

The scrollable viewport uses actual native horizontal/vertical ScrollBar controls and retained frame/editor controls. Scrollbars reserve 16 logical units only when needed. Either bar can induce the other; the renderer-independent `MdiScrollLayout` resolves that dependency, incorporates negative window coordinates, and keeps extent offsets separate from world-space `MdiBounds`. Changes to viewport position never rewrite stored window rectangles or add undo entries. A pointer/keyboard frame edit maps its result back to world coordinates at commit, with the original revision checks.

Activation reveals an off-screen normal or minimized window. A state transition out of maximize also reveals the restored frame. `ScrollWindowIntoView(id)` reveals without activating or changing the model. `ScrollTo(horizontalOffset, verticalOffset)` accepts extent offsets and clamps them to the available range; offsets must be finite. Oversized frames align their leading edge instead of alternating between opposite edges. Extent changes preserve the same visible world origin when possible; removal of distant windows clamps obsolete offsets.

Maximized windows fill the visible viewport and remain anchored while scrolling, without overwriting their normal bounds. Minimized captions retain bottom-up row placement, but rows that overflow above y=0 now participate in scroll extents and can be reached. Existing editor scrolling remains native: the host handles unconsumed wheel input, with Shift selecting the horizontal axis. Ctrl-wheel is not consumed by the host. Scroll changes cancel old frame geometry sessions before moving their coordinate system.

## Retained layout and safety

Visible window geometry is collected into a reusable buffer. Stable inputs reuse measured extents across scrolling, activation and color-only refresh. Mapping, revealing and measuring geometry after warm-up allocate no managed objects; native UI reconciliation and application content still have their own costs. The same frame, caption buttons and editor remain alive across scroll changes and enabling/disabling the viewport. Hidden/removed windows do not contribute scrollable bounds.

The opt-in implementation uses a clipped Canvas with native scrollbar controls instead of putting a ScrollViewer around arbitrary application editors. This leaves editor scrolling independent and keeps the model coordinate system explicit. It does not add touch inertia, drag-edge autoscrolling, or a full ScrollViewer UI Automation provider. General RTL/accessibility, native desktop runtime, and all vendor-specific scroll policies require further qualification.

## Sample and validation

Window > Open scrollable MDI sample presents near, distant and negative-coordinate windows. Buttons exercise the same public APIs; F2 switches its theme and F4 scrolls during a captured gesture to verify cancellation. Diagnostics are read-only. A compact minimized-row example demonstrates overflow without instantiating normal editor content.

Thirteen geometry specifications cover empty/one-axis/both-axis layouts, negative bounds, anchoring, reveal, oversized frames, minimized rows, invalid/overflow input and 1,000 seeded layouts. A warm 10,000-query/128-frame loop requires zero managed allocated bytes, excluding input construction. Eight physical-input cases extend the preceding 145 product scenarios to 153. They require native scrollbar/wheel behavior, model/undo invariants, retained identities, scrolled movement/cancellation, maximize/restore and actual caption pixels in light/dark themes. Counts are definitions; exact-head workflow results establish execution success. All six packages advance to alpha.16.

Public reference: Actipro Standard MDI documents the opt-in IsScrollingEnabled behavior at https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/standard-mdi . This is independent Uno code, not copied vendor source or WPF binary equivalence. Browser CI uses actual Uno Skia via SwiftShader software WebGL, not hardware-GPU throughput certification.
