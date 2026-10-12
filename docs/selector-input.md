# Native selector popup input

The consolidated MDI candidate `89d187d` passed the 28 docking and 32 standalone-tab scenarios, but its new twelve-cycle inspector test failed on the second immediate popup-key sequence. The screenshot and managed diagnostic state showed a focused ComboBoxItem and an open popup still on the old item. Completing focus on DropDownOpened alone did not fix the failure.

DockComboBox now gives the detached popup a short-lived preview-key route to the native ComboBox methods. The original KeyRoutedEventArgs are passed once, with the native handled flag preventing a duplicate bubble action. Selection, committed-versus-always behavior, Escape cancellation, type search and scrolling remain native ComboBox behavior. No JavaScript key injection, test delay or direct manager mutation is introduced. Editable ComboBox input is left unchanged. Popup-close, unload and template replacement remove the bridge.

The original immediate-key assertion and the twelve-cycle regression remain required; final workflow results determine whether the correction is sufficient. ForwardedKeyCount is a read-only performed-work counter, not key/text capture.

Primary implementation reference: Uno ComboBox's OnKeyDownPrivate, PopupKeyDown and TryMoveKeyboardFocus in src/Uno.UI/UI/Xaml/Controls/ComboBox at upstream revision 951b7b464b8372daa95dae6eeedf9ac6c8109b90. This integration is not an assertion of complete native accessibility or every platform's selector conformance.
