# Optional native tab images

`AdvancedTabItem.ImageSource` (inherited by `DockingWindow`) accepts an
application-owned Uno ImageSource. Set `AdvancedTabControl.HasTabImages` to true
to display it. DockSite exposes independent `DocumentWindowsHaveTabImages` and
`ToolWindowsHaveTabImages` settings. All image policies default false.
Configure a hosted window's source in `DockSite.WindowCreated`, before content is
created. A null source reserves no image slot. Sources are presentation objects,
not serialized layout metadata; the library does not dispose caller-owned images.

Images render in native 16 by 16 logical-unit controls with a four-unit label gap
and Uniform stretching. Their controls are not hit-testable, so pointer input
continues to the tab's button. Existing read-only glyphs occupy a separate column.
Unclamped horizontal measured tabs reserve 20 extra units for a present image; vertical tabs
retain their existing row height. Global image policy and source presence affect
measurement, while replacement of an existing source retains the native image,
header, measured text and layout. Virtualization realizes images only alongside
visible/overscan tab headers and never evaluates an off-screen content factory.

## Validation scope

The isolated `tests/TabImageContracts` Uno browser executable references the actual
Controls project. Ten physical-input contracts check defaults, fixture PNG pixels,
measured space, source replacement, null/restore, tab clicks on image coordinates,
four placements, both themes, strip hiding, 2,002 shared sources, independent hosted
policies, floating/undo ownership and exact revision/error status. Its dedicated
workflow is an additional merge gate; no existing application test is removed.
Read-only observations and synthetic color fixtures exist only in this test app.
The existing package-only consumer also compiles the new public APIs.

The historical application-workspace sample/upload previously blocked in PR #22
is not replayed, recovered or included. This PR delivers the reusable controls and
the separate control-contract executable, not that missing sample or its eight
unpublished tests. The main application's default interface is unchanged until a
consumer supplies a source and opts in. Current-head CI establishes pass/fail;
source definitions do not imply a runtime pass. Chromium uses SwiftShader, not a
hardware-GPU benchmark. Remote fetching, all decoder formats, accessibility,
switcher/frame/rail imagery and vendor-certified pixels remain unqualified.

The public behavior reference is Actipro's AdvancedTabControl tab-images section:
https://www.actiprosoftware.com/docs/controls/wpf/docking/advanced-tab-control
DockingScene.DocumentWindowsHaveTabImages is an adapter-specific setting; Actipro
uses TabbedMdiHost.HasTabImages for its WPF document host.

## Fixture review

The first fixture build compiled C# but did not emit a browser bundle because its
project and publish command bypassed Uno's selected-framework inner build. Using
TargetFrameworks plus an explicit browser framework matches the working app
publication path; the workflow also requires the emitted index and framework
directory before starting a browser.

The next run passed nine of ten contracts. Its first image-space assertion
compared a cold-start total with a later remeasurement (a net 16-unit change
instead of 40). Later source-null/restore measurements already showed exact
20-unit changes, and all pixel, retention, virtualization and hosted tests passed.
The fixture now uses three identical labels with identical actions and measures
the two pictured tabs against their simultaneous image-free sibling. Each must
be exactly 20 units wider; initial image-disabled widths must match. This isolates
image space from unrelated startup typography/context measurement changes. It
does not establish the specific cause of the earlier cold-start width difference
or claim to fix arbitrary late-loaded-font cache invalidation. The original
exact 20-unit null/restore test remains. Failure results now retain their native
state. No pixel tolerances or operation success criteria were relaxed.
