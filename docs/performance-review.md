# Performance and correctness review

This increment reviews the Core, Layout, Serialization, MVVM, Themes and Controls packages, the Uno sample, package consumers and browser workflows. It is a source review backed by targeted regression tests, not proof of full Actipro compatibility or a formal security/accessibility audit.

## Findings and changes

| Area | Finding | Change and evidence |
| --- | --- | --- |
| Core lookups | Each Locate/GroupOf/Find walked containers repeatedly. Renderers and MVVM multiplied that work across every window. | `DockLayoutIndex` performs one traversal per immutable snapshot, then dictionary lookups. 10,000-window equivalence, concurrency, weak-cache lifetime and seeded transition tests cover its contract. |
| Immutable transforms | No-op maps rebuilt splits/workspaces; repeated activation advanced revisions and woke observers. | Preserve unchanged node/snapshot identities; suppress no-op activation/metadata commits without clearing redo. |
| Lifecycle | MRU fallback after closing an active document could disagree with the selected tab. | Repair activation also selects the surviving MRU tab; explicit regression checks both identities. |
| Validation | Repeated HiddenWindows membership checks grew quadratically. Nonfinite chrome metrics could reach layout arithmetic. | Hash-set membership and finite input/derived-size checks retain rejection semantics. |
| MVVM | Every snapshot notified every proxy and its commands, even when metadata and placement were unchanged. | Notify only proxies whose metadata, location, active state or layout-lock state changed. A 1,000-window activation checks exactly two proxy notifications; metadata checks one. Source subscriptions are reconciled by difference. |
| Tab controls | Metadata refresh rebuilt all measurement strings, text measures and header visuals. Selection and scrolling repeated allocation searches. | Reference-identity measurement cache, id/slot maps, reuse of unchanged lane plans and paints, coalesced collection refresh and differential metadata subscriptions. |
| Standalone tabs | Hosted tabs reordered, but a reusable standalone control did not. Replacing a selected item under the same id could leave stale presented content. | Cancellable same-lane pointer/public-API reordering; revision-guarded mutation; refresh rebinds the selected item instance. |
| Captions and frames | Unchanged tool/window captions rebuilt their buttons on each projection. MDI pruning/rank checks scanned arrays repeatedly. | Chrome state caching, unchanged-bound checks, set-based pruning and indexed ranks. Effective close/resize/move capabilities are reflected in hosted frames. |
| Content lifetime | DockSite disposed content implicitly; coalesced unregister/register could preserve stale content under a reused id. | Application ownership is the default. `DisposeContentOnUnregister` explicitly opts in. Release unregister transitions before coalesced rendering, detach a whole batch before callbacks, and guard obsolete sample editor events. |
| Input | Enter could commit the MRU switcher on KeyDown, then CharacterReceived appended a newline to the previously focused editor. | Retain modal focus through Enter character dispatch and commit on KeyUp. Existing background-editor assertion plus a held-key regression enforce this. |
| Persistence | File size was checked only before ReadToEnd; malformed enum options were silently accepted; save ignored a tighter configured window budget. | Reusable bounded `DockLayoutStream.ReadAsync`, option validation and consistent save/load window budgets. Eight stream/storage regressions include unknown-length chunked input and cancellation. |
| Themes and delivery | Vector parser/icon coverage, package isolation and test gating needed to remain intact during performance work. | Existing parser, geometry, package-only consumer and real-browser suites remain mandatory. No rendering substitute or removed assertions. |

## What is measured

`DockingScene.PerformanceSpecs` compares the previous independent traversal algorithm against warm indexed lookups over 10,000 windows and 4,000 queries. It asserts result equivalence and a small allocation budget. Timing is printed for the runner that executes it; elapsed-time ratios are not treated as portable performance guarantees. The first measured run at commit `6c567cf` reported 138.500 ms / 1,952,040 B for traversal and 1.391 ms / 40 B for indexed lookup. The 40 bytes are the benchmark stopwatch; there is no per-query location allocation. Index construction is excluded from this **warm lookup** measurement and remains linear.

The browser playground exposes actual public-control workloads: load 2,002 tabs, perform 200 selections, rename one item and replace its content under the same id. Read-only counters report text measurements, header allocation plans, current realized headers and created header visuals. Browser assertions require one measurement per new tab, no new text measurements or lane plans for the 200-selection workload, one measurement for a rename/replacement and fewer than 50 realized headers in that overflowing workload. Timings are observations, not frame-time limits. The workload creates editors lazily when selected; it does not pretend 2,002 editors have already been rendered.

Diagnostics are opt-in and add work themselves. These measurements are not FPS, input-to-photon latency, energy consumption or hardware GPU throughput. CI uses Uno Skia through software SwiftShader WebGL.

## Public API additions and behavioral changes

`DockLayoutIndex.For(snapshot)` provides cached `Find`, `GroupOf`, `Locate` and `ActivationRank` access. The cache uses snapshot **reference identity**, never the revision number, and does not keep snapshots alive. Its inputs must be validated immutable layouts. Modifying records or arrays through reflection invalidates that contract.

`AdvancedTabControl.CanTabsDrag` defaults to true. `ReorderTab(id, finalIndex)` and pointer dragging move an item only inside its pinned/normal/preview lane. `TabReordering` can cancel before mutation; `TabReordered` reports the committed indexes. A changed collection invalidates an in-progress gesture. Escape and lost capture cancel. Hosted controls continue to delegate to DockSite. Cross-lane conversion and cross-site standalone transfer are not implemented by this API. The subsequent alpha.5 retained-host increment adds same-lane edge-autoscrolling.

`DockSite.DisposeContentOnUnregister` defaults to **false**. Closing/hiding keeps content. Unregister or manager replacement releases cached content and raises `WindowContentReleased`; only an explicit ownership opt-in invokes IDisposable. Factories opting in must return exclusively owned content. Application-owned content is not disposed implicitly. This deliberately corrects the earlier alpha's undocumented disposal behavior.

`DockLayoutStream.ReadAsync(stream, options, cancellationToken)` reads from the current position, consumes no more than MaximumBytes + 1 bytes, leaves the source open and propagates cancellation/I/O failures. The extra byte distinguishes an exact-size stream from an oversized one. Invalid layout data returns a rejected `LayoutLoadResult`; invalid options are programmer errors. File storage uses the same bounded reader.

## Remaining costs and review boundaries

A cache does not remove all linear work. Complete manager validation and index construction remain O(windows + nodes) for a changed snapshot. MVVM still reconciles collections; descriptor-based source synchronization still scans its source and trial batches validate intermediate commands. DockSite still computes geometry for structural projections and recreates some splitters/auto-hide auxiliary visuals. Further incremental geometry and bulk-registration work would need separate correctness coverage.

Native/detached hosts, linked-site transfer, advanced vendor-compatible persistence, exact reopen positions, complete data-source/template APIs, touch/inertia, RTL, accessibility and certified vendor pixel comparison remain unfinished. The prior blocked profile/bookmark changes are not part of this review. The persisted schema is unchanged. No desktop runtime or hardware GPU benchmark is claimed by a successful desktop compilation.

The alpha.5 continuation also reuses geometric results and auxiliary visuals; see [retained-host-review.md](retained-host-review.md). The costs described above refer to the original indexed-layout increment, not a claim that later host caching is absent.
