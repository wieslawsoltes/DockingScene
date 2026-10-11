# Docking and MDI compatibility matrix

This is a behavior-oriented independent implementation, not a copy of proprietary source. **Full Actipro WPF parity is not complete.** An implemented behavior does not imply every vendor property, event, visual state, platform integration or edge case is compatible.

Reference material: [Docking & MDI overview](https://www.actiprosoftware.com/docs/controls/wpf/docking/index), [AdvancedTabControl](https://www.actiprosoftware.com/docs/controls/wpf/docking/advanced-tab-control), [Tabbed MDI](https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/tabbed-mdi), and [Layout serialization](https://www.actiprosoftware.com/docs/controls/wpf/docking/layout-features/layout-serialization). DockingScene's independent APIs do not claim WPF binary compatibility.

| Area | Implemented | Remaining / limits |
| --- | --- | --- |
| Core model | Immutable windows/groups/splits/workspace; unique IDs; document, docked, hidden, auto-hide and floating locations | Complete vendor class/property/event surface |
| Transactions | Atomic batches, validation, closing vetoes, expected revisions, bounded undo/redo and ordered notifications | Full routed-event/lifecycle equivalence; asynchronous close orchestration |
| Docking | Tool/document placement, splits, attachment, group movement, policy, locking and live guide preview | Complete guide appearance/placement policies and linked-DockSite ownership transfers |
| Tabbed MDI | Retained documents, measured header virtualization, pointer reordering, four placements and three independently scrolling/clipped lanes | Complete templates, native multi-preview group semantics, full RTL and touch/inertial scrolling behavior |
| Overflow | None, Menu, Scroll, ScrollWithMenu, Shrink and ShrinkWithMenu; global minimum-preserving allocation and fixed horizontal extents | Exact vendor default settings and every overflow visual state |
| Standalone tabs | Lazy retained content, cancellable same-lane pointer reordering, pin/unpin, preview promotion, close buttons, middle-click checks, TabClosing and customizable MenuOpening | Cross-lane/edge-autoscroll reorder policies, system-drag-over activation, complete selector/ItemsSource surface, standalone Ctrl+Tab orchestration, full context-menu event equivalence |
| Tab presentation | Four placements, strip visibility, optional pin/close buttons, singleton tool-strip visibility, compressed-header label targets | Slanted/overlapping/curved templates, rotated content, animations, vendor brush/style/template APIs |
| Keyboard | DockSite filtered normal/pinned/preview digit access, digit 9 as last, MRU switcher, header arrows/Home/End, document-list and context-menu commands | Host-reserved shortcuts and exact keyboard/focus equivalence on all platforms |
| Standard MDI | Overlap, movement, eight resize edges/corners, minimize/maximize, cascade and horizontal/vertical/grid tiling | Native host integration, full size-to-content and minimized-window arrangement policies |
| Floating | Hosted controls with live content, movement, resizing, maximize and redocking | Native OS or detached browser windows, monitor/taskbar/owner integration |
| Auto-hide | Four rails, hover/click activation, delayed closing, pinning and resizable popups | Animation options, every focus/inactivity policy and exact restore positions |
| Content lifetime | Lazy content creation and retention across placement changes; unregister releases the DockSite cache | Native HWND interop and complete application-specific disposal integration |
| MVVM | Stable observable proxies, commands, dispatcher support and atomic one-way source synchronization | Complete ItemsSource/DataTemplateSelector surfaces and two-way application collection ownership |
| Serialization | Bounded JSON/stream input, validated unknown-window policy, cancellation, atomic manager restore and file storage | Actipro XML, lazy creation, tool-only merging, CanSerialize/custom fields, exact reopen positions and migration behaviors |
| Themes and rendering | Four palettes, original vector icons, explicit geometry and real Uno Skia desktop/browser UI | Certified pixel comparison, all vendor themes, full localization/RTL/accessibility, native hardware-GPU performance and exhaustive devices/browsers |
| Packaging | Six versioned NuGet projects, same-commit packing, isolated package-only consumers | Signed stable releases, public NuGet publication and long-term API stability guarantees |

The layout-profile model attempted alongside this increment was blocked during upload and is not present in the repository. The prior dock-position bookmark extension is also not part of this implementation. No tool-only reconciliation, CanSerialize or bookmark-anchor feature is claimed here.

## Validation interpretation

Executable suites cover contracts in Core, Layout, Serialization, MVVM, vector paths and tab geometry/allocation. Chromium browser suites cover integrated DockSite behavior and the reusable standalone tab control through real inputs. Artifacts identify exactly which scenarios passed for a particular commit. Neither compilation nor a screenshot proves the unimplemented rows.

No percentage of full compatibility is claimed: the repository does not contain a complete independently verified vendor conformance suite or denominator.

Performance and review changes are tracked in [performance-review.md](performance-review.md). Warm indexed lookup results and browser measurement counts are workload-specific, not full application speedup or native GPU claims. Content ownership is now explicit and opt-in for disposal.
