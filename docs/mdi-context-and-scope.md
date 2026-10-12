# Contextual MDI titles and host-scoped arrangement

## Public references and delivered surface

The public Actipro [contextual content reference](https://www.actiprosoftware.com/docs/controls/wpf/docking/docking-window-features/contextual-content) describes interactive buttons/status content within docking titles and passes the window DataContext into its template, except UIElement values. The [Standard MDI reference](https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/standard-mdi) documents contextual titles and host-specific cascade/tiling. This increment independently implements that Standard MDI location, not the other four tab/tool/auto-hide locations or the full WPF API.

`DockingWindow.StandardMdiTitleBarContextContentTemplate` is an Uno dependency property. Use `DockSite.WindowCreated` to configure its DataContext and template before the editor is materialized. The hook runs once per container, not on every projection. `WindowControl.TitleBarContextContent` and `TitleBarContextContentTemplate` also support standalone consumers. `DockContextPresenter` is a public reusable control with a data/template pair and one retained template root. Its `Update` applies the pair atomically; direct non-templated content and UIElement data supplied to a template remain distinct operations.

A template change creates a new root. Data, activation, caption state and theme changes reuse the existing root, preserving native control state. With a template, UIElement data is replaced by null instead of being adopted as another editor parent. Clearing/releasing the owner clears bindings and content. Context pointer input activates its frame without capturing movement, and its double-clicks do not invoke title double-click behavior. Caption text remains the drag surface. Context is constrained and clipped, reserving a usable title region and system actions. It is hidden for minimized/frame-free captions but retained across those state transitions.

The sample Window menu adds editable caption controls (a native TextBox bound to Description and a local-state ToggleButton), a status-template variant, clear/visual-context demonstrations, and a separate two-host consumer. Templates are compiled XAML in the sample, not application-specific code embedded in the reusable library. Tests use physical pointer/keyboard input, with read-only diagnostic identity counters.

## Host arrangement correctness

Previously `StandardMdiHost.Arrange` called a global helper that collected all primary workspace documents. A standalone host could therefore rearrange another host's windows and could issue layout changes even after ReleaseContent. The new overload `WindowGeometry.ArrangeMdi(manager, hostedIds, area, arrangement, titleHeight, expectedRevision)` validates and copies the requested members, checks current revision/capabilities, uses one immutable builder, and commits once. Hidden, unknown, duplicate and ordinary tool-group IDs reject atomically. Floating document-group members are supported without changing the enclosing floating host bounds.

`StandardMdiHost.Arrange`, `Cascade`, `TileHorizontally` and `TileVertically` now use this scoped overload and immediately project their result. Released hosts reject. The existing global helper retains its primary-workspace behavior by selecting those IDs explicitly. Empty scope does not change MDI mode. Identical arrangements preserve snapshot identity, undo/redo and notifications. A caller indexer that changes the manager causes rejection instead of overwriting newer data.

## Performance and validation limits

The geometry operation performs one batch rather than one per-document trial transaction. The test workload includes 1,000 documents, a single notification and one undo. This is an operation-count guarantee, not an FPS or hardware-GPU measurement. Template-build and identity assertions reject redundant control creation. Native renderer paint, focus and existing large-tab tests remain required.

All six packages advance together to `0.1.0-alpha.11`. The candidate adds twelve model specifications and seven browser scenarios, for 21 model suites and 118 product scenarios plus the separate 100-cycle editor-focus gate. Exact-head workflow results determine whether they passed. Desktop compilation is not native desktop runtime certification. CI renders actual Uno Skia through SwiftShader software WebGL. Full Actipro template/API equivalence, native/detached windows, linked sites, scrolling MDI extents, size-to-content, all contextual locations, full RTL/accessibility and complete vendor pixel comparisons remain unfinished. Previously excluded profile/bookmark changes are not included.

### Reentrant context callbacks

The compiled sample template handlers are internal because Uno emits template helpers outside the resource dictionary class. A new callback regression replaces a data/template pair during DataContextChanged and releases an MDI host during contextual-template creation. The presenter must never load the obsolete root; the released host must not attach the returned editor. Snapshot/frame checks surround application factories, content creation and context binding. These cases preserve normal template retention and do not weaken existing input assertions.
