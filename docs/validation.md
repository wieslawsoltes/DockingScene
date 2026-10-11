# Validation

## Executable specifications

Every `tests/*Specs/*.csproj` program returns a nonzero exit code on failure. CI runs all matching projects with shell `pipefail`; a failed test process cannot be hidden by `tee`.

| Suite | Coverage |
| --- | --- |
| DockingScene.Specs | Engine, docking, capabilities, lifecycle, serialization and history |
| DockingScene.LayoutSpecs | Split constraints, geometry, guide targeting, MDI and storage |
| DockingScene.ParitySpecs | Null/deep/cyclic layouts, overflow and invalid metadata |
| DockingScene.TransactionSpecs | Batches, rollback, stale revisions, cancellation, ordered/reentrant observers, previews and directional closing |
| DockingScene.MvvmSpecs | Stable proxies, commands, metadata, dispatch, disposal, source collections and cancellation |
| DockingScene.VectorSpecs | Built-in icons, SVG grammar, curves/arcs, numeric syntax, locale and budgets |
| DockingScene.TabSpecs | Measured offsets, binary visible ranges, insertion positions, scrolling and drag index correction |
| DockingScene.TabLayoutSpecs | Three-lane allocation, six overflow modes, minimum/fixed extents, large strips, filtered digits and 1,000 seeded allocation/fit checks |

The summaries in each artifact are authoritative for that revision. The 1,000 new allocations comprise 500 mixed constrained cases and 500 feasible-fit cases; they supplement 13 named tab-layout specifications rather than representing 1,000 UI tests.

## Integrated docking browser suite

The workflow publishes the same `net10.0-browserwasm` application distributed to users and serves it under `/DockingScene/`. Chromium waits for the splash to disappear and uses real pointer and keyboard input.

The 23 docking scenarios cover startup, initially unmodified multiline documents, content identity through close/reopen, persistence restore, standard/tabbed MDI, locking, editor caret behavior, preview promotion, keep-open, tab reordering, floating/redocking, splitters, auto-hide resizing/pinning, pinned close-others, switcher cancel/commit, themes and reset. Captured gestures must own a real pointer capture and release it on outside-site mouse-up or Escape. Redocking requires a floating source rather than treating a no-op as a pass.

Three switcher regressions also cover twelve immediate Enter/Escape openings without waiting for focus, one-step arrow navigation, input-scope cleanup and absence of background editor changes. The original twenty docking assertions remain in place. See [switcher input scope](switcher-input-scope.md) and [atomic test geometry](browser-hit-targets.md) for the two different live-check failures and their corrections.

## Standalone tab browser suite

A second fresh browser session opens **Tab options → Open standalone tab playground**. Its 18 scenarios exercise actual reusable controls, including:

- Lazy creation and header virtualization; independent lane scrolling; menu access to hidden tabs.
- Global shrinking with feasible minima; all 20 compressed headers selected through their physical label targets; fixed extents under overconstraint; all six overflow affordance sets.
- Hidden strip space reclamation and content identity through four placements; standalone pin/unpin; closing vetoes and middle-click CanClose checks.
- Application-added MenuOpening actions; singleton tool-strip visibility; normal/pinned/preview keyboard filtering and digit 9 selecting the last item.
- Exception-free execution and exact deployed commit identity.

Assertions observe the real application through read-only opt-in diagnostics. They do not invoke manager commands or replace the renderer with test stubs. Both suites run even if one fails, and either failure blocks distribution. Failed scenarios capture screenshots and JSON; traces capture the complete input session.

## Diagnostics and privacy

Normal browser sessions do not run periodic diagnostic capture. `?diagnostics=1` enables window metadata, bounds and synthetic sample-editor text/caret/identity observations. Downstream applications must not enable these observations around confidential content.

Bounds are clipped by ancestor viewports, tab-lane clips and explicit rectangle clips. They do not perform sibling-occlusion analysis. Tests therefore require actual input-driven state transitions instead of treating diagnostic rectangles as proof of correct interaction.

## Package and deployed-site gates

All six alpha NuGet packages are built from one source snapshot. A fresh package cache restores a plain .NET consumer that executes the five UI-independent packages, followed by a separate Uno consumer that compiles the Controls surface, including tab options and events, without ProjectReferences.

The distribution workflow repeats model contracts before publishing. After a green main build and both browser suites, Pages deploys the artifact. A separate job checks `build-info.json` against the source commit and reruns both suites against the live URL. Source snapshots, packages, contract logs, screenshots and local/live traces are retained as workflow artifacts.

## Limits

Chromium uses **ANGLE/SwiftShader software WebGL**. It exercises the actual Uno Skia renderer but is not a hardware-GPU benchmark. Desktop targets compile in CI; native desktop interaction, Windows/macOS runtime behavior, detached windows, multi-monitor integration, touch/stylus/inertial scrolling, screen readers, RTL, Firefox/WebKit and vendor visual comparison still require additional validation.

A green build certifies only the exercised contracts and scenarios at its exact revision, not full Actipro compatibility. Keep the outstanding matrix in `parity.md` alongside release evidence.
