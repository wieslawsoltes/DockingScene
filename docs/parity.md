# Docking and MDI compatibility matrix

This is a behavior-oriented independent implementation, not a copy of proprietary source. **Full Actipro WPF parity is not complete.** A feature being implemented here does not imply every Actipro property/event, visual state, platform integration or edge case is compatible.

Reference material reviewed: [Actipro Docking & MDI overview](https://www.actiprosoftware.com/docs/controls/wpf/docking/index), [Tabbed MDI](https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/tabbed-mdi), and [Layout serialization](https://www.actiprosoftware.com/docs/controls/wpf/docking/layout-features/layout-serialization). The independent APIs below intentionally do not claim binary compatibility with WPF assemblies.

| Area | Implemented | Remaining / limits |
| --- | --- | --- |
| Core model | Immutable windows/groups/splits/workspace; unique IDs; docked, document, hidden, auto-hide and floating locations | Actipro's complete class/property/event surface |
| Transactions | Atomic batches, validation, close vetoes, expected revisions, bounded undo/redo, ordered notifications | Full Actipro routed-event/lifecycle equivalence; asynchronous close orchestration |
| Docking | Tool/document placement, splitting, attachment, group movement, capability policy, layout lock, live guide preview | All guide appearance options, exact placement policies and complete linked-DockSite transfers |
| Tabbed MDI | Real retained documents, measured header virtualization, pointer reordering, overflow list, four placements, keyboard navigation | Sticky pinned/preview lanes, all overflow strategies, multi-preview parity, complete template customization |
| Preview/pinned tabs | Keep-open, pin/unpin, modification promotion, atomic preview replacement, directional close preserving pinned tabs | Exact visual/keyboard parity for every Actipro tab variant |
| Standard MDI | Overlapping frames, movement, eight resize edges/corners, minimize/maximize, cascade, horizontal/vertical/grid tiling | Native host integration, all size-to-content and minimized-arrangement policies |
| Floating | Hosted controls with live content, movement, resizing, maximize and redocking | Separate native OS windows, browser detached windows, monitor/taskbar/owner/activation integration |
| Auto-hide | Four rails, hover/click activation, delayed closing, pinning, resizable popups | Complete animation options, all focus and inactivity policies, exact group restore positions |
| Content lifetime | Lazy creation and retained content across placement changes; unregister releases control cache | Full native HWND interop and application-specific disposal integration |
| MVVM | Observable proxies, commands, stable identities, optional dispatcher, atomic one-way source synchronization | Full ItemsSource/DataTemplateSelector surface on every standalone control and two-way application collection ownership |
| Serialization | Bounded JSON, unknown-window policy, validation, atomic manager restore and file storage | Actipro XML, lazy window creation, tool-only merging, CanSerialize/custom fields, all legacy migration behaviors |
| Themes and icons | Four palettes, original vector icons, explicit geometry construction, high-contrast palette | Certified pixel comparison, all vendor themes, complete RTL/localization and accessibility audit |
| Rendering | Real Uno Skia desktop/browser application and actual browser pointer/keyboard tests | Native hardware-GPU performance measurement, platform-wide visual equivalence, exhaustive browser/device coverage |
| Packaging | Six reusable NuGet projects with UI-independent engine/layout/MVVM/persistence packages | Signed stable releases, public NuGet publication, long-term API compatibility guarantees |

## Validation interpretation

Executable specification suites cover contracts in Core, Layout, Serialization, MVVM, vector paths and measured tabs. Chromium browser scenarios cover a meaningful subset of the integrated Uno control behavior. The browser results artifact identifies exactly which scenarios passed on a particular commit. Neither a passing build nor a screenshot proves the unimplemented rows above.

No numerical percentage of compatibility is claimed: there is no complete, independently verified denominator or vendor conformance suite in this repository.
