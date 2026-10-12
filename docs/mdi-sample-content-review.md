# Standalone MDI editor layout review

Review of the preceding alpha.12 independent-host screenshot showed an editor rendered as a small input box in the corner of an otherwise large document. The sample-created AdvancedTabItem did not stretch its Content; DockSite-created document containers already did. The standalone sample now uses one factory that stretches both container and native multiline TextBox, removes the input-box border, applies the sample's editor palette/padding, and enables automatic scrolling. Both the existing independent-host and new magnetic-layout examples use it.

This is a sample-composition correction, not a change to reusable AdvancedTabItem defaults or a claim of full vendor text-editor parity. Diagnostics expose actual editor extents so the magnetism browser check also verifies that the editor fills its content area while retaining its instance through movement.
