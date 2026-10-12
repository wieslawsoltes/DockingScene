# Native selector popup input

The consolidated MDI candidate `89d187d` passed the 28 docking and 32 standalone-tab scenarios, but its new twelve-cycle inspector test failed on the second immediate popup-key sequence. The screenshot and managed diagnostic state showed a focused ComboBoxItem and an open popup still on the old item. Completing focus on DropDownOpened alone did not fix the failure.

DockComboBox now gives the detached popup a short-lived preview-key route to the native ComboBox methods. The original KeyRoutedEventArgs are passed once, with the native handled flag preventing a duplicate bubble action. Selection, committed-versus-always behavior, Escape cancellation, type search and scrolling remain native ComboBox behavior. No JavaScript key injection, test delay or direct manager mutation is introduced. Editable ComboBox input is left unchanged. Popup-close, unload and template replacement remove the bridge.

The original immediate-key assertion and the twelve-cycle regression remain required; final workflow results determine whether the correction is sufficient. ForwardedKeyCount is a read-only performed-work counter, not key/text capture.

Primary implementation reference: Uno ComboBox's OnKeyDownPrivate, PopupKeyDown and TryMoveKeyboardFocus in src/Uno.UI/UI/Xaml/Controls/ComboBox at upstream revision 951b7b464b8372daa95dae6eeedf9ac6c8109b90. This integration is not an assertion of complete native accessibility or every platform's selector conformance.

## Root routing follow-up

At aa9a7e, all twelve selector stress cycles passed, but the later original inspector case still failed with a focused ComboBoxItem and zero forwarded keys. The popup-local route was not reached. The opening scope now also attaches to the owning XamlRoot, verifies that the focused item belongs to this selector, and forwards each original event at most once across both routes and the normal control path. Close/unload/template changes remove both subscriptions. It cannot take keys from another selector or pane. No custom selection algorithm or fabricated key is used.

The same run passed 45/47 host cases. The keyboard-reset test predicate threw while its next diagnostic snapshot had not yet published the editor, although the failure capture showed the exact expected text `keyboard\nreleased`. Its existing wait now null-checks the not-yet-published editor before comparing the exact same text. The immediate typing sequence and timeout are unchanged.

## Native navigation state investigation

The root-scoped 6ac042d run passed 46/47 host scenarios, including the eight rapid minimize/restore cycles. Its remaining inspector case recorded all seven forwarded navigation keys but still selected the old item. Opt-in navigation diagnostics now record key kind, handled state, selected/focused indices and adjacent-container availability around the native method. They do not record text or change selection, and are disabled outside diagnostic sample sessions. The twelve-cycle regression also repeats the original metadata-edit/read-only setup before creating its new document, rather than testing only a fresh selector. This is an investigation, not a claimed final correction.
