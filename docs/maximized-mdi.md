# Maximized MDI frames and primary-window commands

The independent Uno control library supports the maximized-frame visibility and external window-command pattern described by the [Actipro Standard MDI documentation](https://www.actiprosoftware.com/docs/controls/wpf/docking/workspace-mdi-features/standard-mdi). This is an added behavioral subset, not WPF binary compatibility or certified vendor pixel parity.

## Frame-free content

`StandardMdiHost.AreMaximizedWindowFramesVisible` defaults to true. Set it to false to remove a maximized window's caption, border and resize grips. The content receives the reclaimed space, but its object identity and normal restore bounds do not change. Normal and minimized windows retain their frames. Re-enabling frames reuses the same caption buttons and editor; no new template or content factory is required. `WindowControl.IsFrameVisible` also exposes this behavior for standalone consumers.

An application hiding the frame must provide external window commands. The sample's Window menu offers Show/Hide maximized window frames and installs a retained command bar at the right of its menu row. The showcase bar displays commands in all MDI states; library consumers can use the default `ShowOnlyWhenMaximized = true` for the conventional maximized-only presentation.

## Reusable external command bar

```csharp
var host = new StandardMdiHost
{
    AreMaximizedWindowFramesVisible = false
};
var commands = new StandardMdiControlBar
{
    Host = host,
    ShowOnlyWhenMaximized = true,
    AutomationIdPrefix = "application-mdi"
};
// Call host.Update(manager, documentIds, contentFactory, theme) to project a snapshot.
// Place host in the workspace and commands in an application menu/ribbon row.
```

The command bar retains its system icon, title and state/close buttons. A hidden caption's system menu can be anchored to the external icon through `WindowControl.ShowSystemMenu(anchor)`. The control follows its host's presentation theme without recreating buttons. Host subscriptions attach and detach with its loaded lifetime. Changing Host removes the old subscription and replaces command references; a released host has no executable primary-window actions.

Applications using their own ribbon can bind to `MinimizePrimaryWindowCommand`, `MaximizePrimaryWindowCommand`, `RestorePrimaryWindowCommand` and `ClosePrimaryWindowCommand`. `PrimaryWindow` identifies the current target and `ProjectionChanged` notifies external presentations. Subscribers own their event lifetime.

## Targeting and policy

Primary commands are scoped to the host's supplied document ids. An active member wins; otherwise the most recently activated open member is used. Caller order breaks missing-history ties. A focused tool outside the host is never chosen, and hidden/unregistered members are skipped.

Command execution resolves the latest snapshot again, checks UI options and effective capabilities, then commits through an expected-revision manager transaction. Closing vetoes and undo semantics therefore remain in the existing engine. Layout locking disables minimize/maximize/restore while permitted closing remains available. Disabling maximize/minimize affordances does not prevent restoring an existing maximized/minimized window. Public programmatic engine commands remain distinct from presentation options.

## Minimized-layout performance

`MinimizedWindowLayoutCache` retains one result keyed by ordered minimized ids, viewport, caption size and spacing. Equivalent activation or metadata changes reuse the exact geometry object. Changed order, membership or geometric inputs recompute through `MinimizedWindowLayout.Arrange`. The caller's id array is copied only when a new plan is built; a failed input does not replace the previous valid result. Clear releases retained ids and geometry without resetting lifetime counters. The cache is synchronized, but callers must not mutate an input collection during Arrange.

The host now uses this cache and exposes `MinimizedLayoutBuildCount` and `MinimizedLayoutReuseCount`. Window chrome signatures are value types rather than one allocated record per UpdateChrome call. Other host reconciliation and command-target resolution can still scan members; this is not an allocation-free or constant-time complete-render claim.

## Validation scope

Twelve UI-independent specifications exercise primary targeting, state/policy availability, hidden members, distinct hosts, mutable caller arrays, cache invalidation, failed-input preservation and 500 cached/uncached minimized-layout comparisons. The isolated Uno package consumer compiles the new host/frame/bar surface.

Eight additional physical-input browser cases cover external command targeting while a tool is active, reclaimed content space, system-menu restore, retained content/caption/bar identity across theme and frame changes, lock behavior, populated minimized-plan reuse, target-specific close and released-host disablement. They extend the existing 91 cases to 99 named scenarios; the separate 100-cycle native/canvas focus stress test remains unchanged. Final workflow artifacts—not these planned assertions—determine whether a revision passed.

All six packages advance together to 0.1.0-alpha.8. CI renders the actual Uno Skia browser application with SwiftShader software WebGL. This does not certify hardware GPU throughput, native desktop runtime, detached hosts, linked sites, full templates/data sources, scrollable MDI overflow, RTL/accessibility or full Actipro compatibility. Previously blocked profile/bookmark work is not included or retried.
