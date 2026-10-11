# Validation

## Model and package gates

All `tests/*Specs/*.csproj` executables return a nonzero exit code on failure. CI runs every matching project under `pipefail`; logging cannot hide a failed process. There are fourteen suites:

| Suite | Main coverage |
| --- | --- |
| Specs | Engine, capabilities, lifecycle, history and 2,500 seeded state transitions |
| LayoutSpecs | Geometry, splits, guides, MDI, serialization and storage |
| ParitySpecs | Invalid/null/deep/cyclic layouts and metadata boundaries |
| TransactionSpecs | Atomicity, rollback, close vetoes, revisions, previews and observer ordering |
| MvvmSpecs | Stable projections, metadata, dispatcher/disposal and source synchronization |
| VectorSpecs | Built-in icons, bounded SVG grammar, curves/arcs, numeric syntax and locale |
| TabSpecs | Measured offsets, visible intervals, insertion and scrolling |
| TabLayoutSpecs | Three lanes, six overflow modes, constraints, filtered digits and seeded allocations |
| PerformanceSpecs | Indexed lookup equivalence, weak lifetime, structural sharing, no-op changes and work/allocation checks |
| SerializationSpecs | Bounded UTF-8 streams, byte/window budgets, cancellation and malformed data |
| BulkSpecs | Bounded atomic batch registration, capabilities, cancellation and allocation observations |
| GeometryReviewSpecs | Resize constraints, numerical boundaries and 1,000 seeded resize cases |
| GeometryCacheSpecs | Cache input equivalence/invalidation, 300 comparisons with uncached results, and finite minimum-compression arithmetic |
| EdgeScrollSpecs | Direction, speed, elapsed time, boundaries, invalid inputs and extreme finite numbers |

Current suite definitions contain 2,727 baseline assertions and 190 named focused specifications. Seeded loops supplement these summaries; they are not additional browser scenarios. The logs for the exact commit are authoritative for execution results.

The Uno workflow compiles desktop, publishes the actual WebAssembly application, and packs all six libraries from the same source revision. Two separate consumers restore the generated NuGet packages into a fresh cache: a plain .NET application executes the five non-UI packages, and a Uno library compiles the controls APIs without project references. Desktop compilation and package consumption are not native UI runtime tests.

## Browser gates

The workflow serves the actual distribution under `/DockingScene/`. Three independent Chromium sessions use physical pointer and keyboard events. The model is never mutated through JavaScript test hooks. All three run even if an earlier suite fails; any failure prevents distribution.

**Docking: 26 scenarios.** Existing startup, complete multiline text, dirty/preview lifecycle, content identity, persistence, tabbed/standard MDI, lock, drag/reorder/float/redock, splitter, auto-hide, pinned closing, themes and reset behavior remain covered. Captured drags must acquire and release actual pointer ownership. Switcher checks include immediate keys, held Enter, repeated editor-to-switcher confirmations, scope cleanup, background-text preservation and cancellation when another command changes the layout.

**Standalone tabs, performance and lifetime: 32 scenarios.** These cover lazy content, bounded header realization, independent lanes, all overflow policies, fixed/shrunk widths, physical selection of every compressed header, four placements, pinning, closing/menu callbacks, filtered digits, application versus owned content, reentrant factories, same-id replacement and coalesced unregister/register. The 2,002-tab workload checks one measure per new header, no new measures or lane plans for 200 selections, and one affected header measure for rename/replacement. PNG pixel checks inspect real rendered host-tab colors rather than DOM properties.

**Retained host and edge scrolling: 11 scenarios.** Twelve reset/tab-click cycles type immediately without a second content click, then also verify editor-body clicking; another check preserves caret selection across tabs. Twenty activations require zero new geometry builds and zero new auxiliary controls, including unchanged individual identities and correct PNG tab paint. Further cases cover repeated keyboard edits of one retained splitter, color-only themes, horizontal/vertical drag scrolling, cancellation and disabled scrolling. A child-directed focus transfer must remain outside the document and accept subsequent toolbar keyboard input.

These 69 named browser scenarios do not represent separate platforms or exhaustive vendor conformance tests. Screenshots and traces preserve successful states and failures. Report actual result counts from the final revision, not an earlier passing subset.

## Live deployment

After a successful main-branch model/build/package/browser gate, Pages deploys that artifact. The post-deployment job verifies `build-info.json` against the exact source commit and reruns all three suites against the live URL. A successful upload is not the same as a successful live verification. Source snapshots, all packages, logs, screenshots and both local-server/live traces are retained as workflow artifacts.

## Diagnostic and performance interpretation

`?diagnostics=1` enables read-only synthetic-editor text/caret/identity observations, window metadata, focus automation IDs, clipped hit bounds and performed-work counters. It is off in normal sessions. Downstream applications must not enable it around confidential content. Bounds respect ancestor viewports and explicit clips, but not sibling occlusion; tests therefore require input-driven state transitions. The harness captures each hit rectangle atomically instead of re-reading another mutable snapshot.

Work-count assertions reject redundant computation without assuming a specific runner speed. Native lookup timing compares a defined warm workload and excludes index construction. Browser counters and timings are not FPS, input-to-photon latency, power measurements or hardware GPU results. Diagnostics themselves add overhead.

CI uses the actual Uno Skia renderer through **ANGLE/SwiftShader software WebGL**. Native desktop runtime, multiple monitors, detached windows, touch/stylus/inertia, screen readers, RTL, other browsers and vendor pixel comparison still require separate validation. See `parity.md`, `performance-review.md` and `retained-host-review.md` for implemented contracts and remaining scope.
