# Retained host geometry, focus and drag scrolling

This continuation reviews the docking host's remaining repeated layout/visual work and the main-branch typing failure after PR #5. It adds independent reusable layout helpers and control behavior, not a replacement renderer or a claim of complete Actipro parity.

## Failure evidence and focus correction

Main workflow 38156647264 compiled desktop/WebAssembly, passed the model and package gates, then failed one of 58 browser scenarios. The trace places the failure before F8: after a reset and editor selection, typed text never reached the editor. The captured state had no focused control. That is different from the earlier switcher's Enter-character dispatch issue.

`AdvancedTabControl.FocusSelectedContent` restores a weakly remembered focused descendant or finds an eligible control in the already-created selected content. It runs synchronously on a simple tab pointer release, not during background projection. Selected-content pointer input also restores missing focus at the content boundary, including clicks on blank editor space. It does not reclaim focus deliberately transferred by a child to a toolbar or popup. A required template/layout flush is limited to explicit focus requests. Existing content factories are not invoked for unrelated windows, and background render passes never steal focus.

Previously loaded but now detached tab controls skip queued refreshes. This prevents a stale presenter from reclaiming a visual that another group has just acquired. The sample also removes obsolete editor/diagnostic references on release without removing a newer same-id instance. The initial focus-only commit passed all 58 existing browser cases; the final gate adds twelve immediate tab-click/typing cycles and a caret/selection restoration check.

## Reusable geometry cache

`DockGeometryCache.Arrange` wraps `DockLayoutEngine.Arrange` for validated immutable snapshots. Its single entry retains only the latest input and result, with public build/reuse counts and a `Clear` operation. It is synchronized for callers sharing the cache.

Cache inputs are the viewport, all `DockMetrics`, tree topology and group membership/order, split ratios, floating host bounds/maximize state, auto-hide edge presence, and window minimum/maximum/titlebar constraints. Activation, selected tab, titles, read-only/dirty state and other non-geometric metadata do not require new rectangles. References to unchanged subtrees and immutable arrays avoid unnecessary comparisons. Changing a real geometry input recomputes through the existing engine; a failed arrangement does not replace the last valid cache entry.

The input contract matches the engine's validated immutable layout contract. This helper is not a substitute for structural validation. Shape/metadata comparisons may still be linear when snapshots differ; the optimization removes redundant measuring, arrangement and geometry-result allocation rather than guaranteeing constant-time complete rendering.

Thirteen executable cases cover all invalidation families, failed-call preservation and clearing. Three hundred deterministic transitions compare cached node rectangles, splitters, rails and warnings against the uncached engine. An additional extreme-finite-size case protects proportional minimum-size shrinking from multiplication overflow: divide the finite minimum ratio before multiplying by the available extent.

## Retained auxiliary controls

DockSite retains splitter buttons, auto-hide rail buttons, auto-hide resize grips and empty-workspace presenters by stable, namespaced identity. Each projection marks used entries and removes only obsolete ones. Manager replacement clears these controls and the geometry cache.

Retained splitter callbacks read the current geometry from their button rather than capturing the first ratio. This preserves keyboard focus through successive edits and keeps pointer operations guarded by the existing revision/capability checks. Rail captions are measured from real Uno text and cached by title/theme, replacing character-count estimates. Resize grip callbacks re-resolve their current group and policy before acquiring a pointer. Application-owned workspace content continues to use the existing single-presenter lease.

The new browser workload switches active tabs twenty times and requires zero new geometry builds and zero new auxiliary controls, checking individual instance identities as well as counters. It also exercises successive splitter keyboard edits and color-only theme changes. These are work-count assertions, not a claim that all rendering is allocation-free.

## Standalone edge scrolling

`CanTabsAutoScroll` defaults to true. During an owned standalone drag, the tab lane scrolls when the pointer reaches its leading or trailing edge. `TabEdgeScroll.NextOffset` provides the independent axis calculation: proportional edge speed, viewport/content bounds, finite input validation and elapsed-time integration. Delayed ticks are capped at 100 milliseconds to avoid a large jump.

A timer runs only while an active edge drag can scroll. Pointer release, Escape, capture loss, unload, changed collection revision or disabling drag/scrolling stops it. A temporary root preview-key scope keeps Escape available even when scrolling virtualizes away the originally focused header. It is detached when the gesture ends. The insertion marker is recomputed as hidden tabs enter the viewport. Existing measurement caches and tab identities are retained. No cross-lane pin/preview conversion or cross-site transfer is introduced; hosted DockSite dragging remains under its own docking controller.

Ten executable cases cover direction, speed, constraints, invalid numbers, elapsed-time equivalence and extreme finite inputs. Real pointer cases cover commit to a hidden destination, Escape/timer cleanup, disabled scrolling and the vertical axis.

## Validation and boundaries

The existing 58 browser scenarios, including actual PNG tab-paint checks, remain unchanged. A third browser session adds eleven focus/host/edge-scroll scenarios. All three suites run before deployment and against the deployed revision, with failures blocking distribution. Fourteen model specification executables and both isolated NuGet consumers remain required. Package consumers compile `FocusSelectedContent` and `CanTabsAutoScroll` and execute the geometry/edge helpers without source project references.

Diagnostic capture remains opt-in and read-only. Added observations are focus automation IDs, geometry build/reuse counts, auxiliary identities and edge-scroll steps. Test inputs still use actual pointer and keyboard events; no script changes the managed model.

CI uses the real Uno Skia renderer through SwiftShader software WebGL. Neither these work counts nor desktop compilation certify hardware GPU performance, native desktop runtime, accessibility, RTL, touch/inertial scrolling or vendor pixel parity. Full candidate validation, metadata reconciliation, some host projection and complex layout changes still cost work. Native/detached hosts, linked sites, full template/data-source APIs and advanced vendor-compatible persistence remain unfinished. Prior blocked profile/bookmark changes are not retried or included.

## Expanded-run findings

The first retained-host run passed eight of ten new scenarios, but Escape did not cancel after the source header was virtualized away; focus had moved to a toolbar button outside the header's key route. The drag now owns a short-lived root preview-key scope rather than relying on that disappearing header. The existing repeated editor-body click scenario also exposed the focus precondition again, so content pointer focus handling and the twelve-cycle regression were extended without removing the second click or waiting for focus before typing. The retained activation test additionally samples actual PNG tab colors after repeated changes. Final-head workflow artifacts, not an earlier partial run, determine release readiness.

A child-focus handoff scenario opens a real sample document whose button focuses an external toolbar button. The test checks that subsequent Space input opens the toolbar menu rather than returning to the document. Disabling `CanTabsDrag` also cancels an in-progress standalone gesture immediately, including its timer and root Escape handler.

## Retained tab button paint

The next expanded run passed all 58 original scenarios and all focus/scroll checks, but the new repeated-activation PNG sample exposed a selected tab whose native button background remained gray while its text and close button reflected the selected state. Tab buttons now use a compiled, directly loaded template whose fill remains bound to the owning tab's Background. Pointer/pressed/disabled states affect content opacity instead of replacing that fill. Native Button keyboard, pointer and automation behavior is retained; no rendering cache or visual-state assertion is disabled. This resource lives in the reusable Controls package and does not require an application to merge a dictionary or invoke runtime XAML parsing.
