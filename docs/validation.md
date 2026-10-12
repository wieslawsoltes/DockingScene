# Validation

## Model and package gates

Every `tests/*Specs/*.csproj` executable returns a nonzero exit code on failure. Both workflows enumerate all matching projects under shell `pipefail`, so logging cannot hide a failed process. Nineteen suites cover Core, transactions, invalid inputs, Layout, MVVM, source binding, serialization, vectors, tab geometry, bulk registration, performance, MDI state and primary commands. The new `MdiCommandSpecs` adds twelve named cases and 500 cached/uncached minimized-layout comparisons. Test logs for the exact source revision are authoritative; seeded loops are not additional browser scenarios.

The Uno workflow compiles desktop, publishes the actual WebAssembly application and packs all six libraries from one source revision. A fresh NuGet cache restores a plain .NET application that executes all five non-UI packages and a separate Uno consumer that compiles the controls surface without project references. The latter includes `StandardMdiControlBar`, frame visibility and primary-window commands. Desktop compilation and package consumption are not native UI runtime tests.

## Browser gates

The distribution is served under `/DockingScene/`. Three independent Chromium sessions use physical pointer and keyboard events with read-only diagnostics. JavaScript does not invoke manager commands or replace the renderer. All suites run even if an earlier suite fails, and any failure blocks distribution.

| Suite | Named scenarios | Coverage |
| --- | ---: | --- |
| Docking | 28 | Startup, editor text/caret, docking/floating, MDI, policies, history, persistence, auto-hide, switcher, themes and canvas/native-input boundary |
| Standalone tabs | 32 | Three lanes, six overflow modes, shrinking/fixed extents, physical header targets, menu/close/pin behavior, factories, content lifetime and 2,002-tab work counts |
| Host, Metro and MDI | 47 | Focus restoration, retained geometry/chrome/pixels, edge scrolling, compact native views, MDI movement/state/menus, frame-free content, scoped external commands and minimized-plan reuse |

There are **107 named browser scenarios**, including the previous 99, one repeated selector case and seven keyboard-window cases. These counts are test definitions, not a claim that an arbitrary revision passed them. The exact workflow's result files and exit codes establish execution success.

The separate **100-cycle focus investigation** remains a build gate. It exercises repeated native editor/body input through the packaged canvas focus boundary. The helper contracts cover trusted-pointer scoping, separate documents, idempotence, disposal and atomic hit-target reads. This investigation is not counted as another 100 named product scenarios.

## New frame and command regressions

Real external-button input must target the host's primary document even after a tool gains activation. Hiding a maximized frame must return at least the caption height to the actual editor, remove caption hit targets and preserve its content instance and normal bounds. External system-menu restore, light/dark changes and repeated frame transitions must retain caption/control-bar identities. Locking must disable restore without disabling otherwise permitted close. Populated minimized-caption plans must not rebuild during ten document/tool activation changes. Closing and releasing a host must remove obsolete targets and disable external commands.

Existing PNG assertions still inspect real rendered tab, tool-caption, status, hover and MDI caption colors. New frame-free light/dark screenshots are retained. Screenshots and selected pixels do not establish complete vendor visual parity.

## Live deployment

After a successful main build, Pages deploys that exact artifact. The post-deployment job verifies `build-info.json` against the source commit and reruns the browser suites on the live URL. Deployment success and post-deployment verification are separate results. Source snapshots, packages, logs, screenshots and local-server/live traces are retained as workflow artifacts. A feature branch is not published merely because it compiles.

## Diagnostic and performance boundaries

`?diagnostics=1` exposes synthetic editor text/caret/identity, focus automation ids, model metadata, clipped hit bounds and performed-work counters. It is off in normal sessions. Do not enable it around confidential content. Geometry respects ancestors and explicit clips but not sibling occlusion, so tests require actual input-driven state transitions rather than treating rectangles as proof of correct interaction.

Work-count tests reject redundant computation without machine-specific timing thresholds. Warm lookup benchmarks exclude index construction; minimized-plan reuse does not remove all host reconciliation. Browser counters are not FPS, input-to-photon latency, power measurements or hardware-GPU results. Diagnostics themselves add overhead.

CI renders real Uno Skia through **ANGLE/SwiftShader software WebGL**. Native desktop runtime, detached windows, multiple monitors, touch/stylus/inertia, screen readers, RTL, other browsers and certified vendor pixel comparison still need separate validation. See `parity.md`, `maximized-mdi.md`, `mdi-review.md` and the preceding performance/Metro/focus review documents for implementation history and remaining scope.

## Consolidated presentation and keyboard increments

PR #9's fourteen MdiPresentationSpecs are retained alongside PR #10's twelve MdiCommandSpecs, not replaced. Selector review adds twelve immediate popup-key sequences as one named browser case. WindowKeyboardSpecs adds fourteen model cases and 500 seeded resize sequences. The host browser suite adds seven keyboard-window cases; the combined source defines 107 named browser cases plus the existing 100 focus cycles. Final workflow result files, not these definitions, establish pass counts.
