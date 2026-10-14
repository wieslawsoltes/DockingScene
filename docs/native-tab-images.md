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
Horizontal measured tabs reserve 20 extra units for a present image; vertical tabs
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
