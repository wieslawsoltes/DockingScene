# Native popup readiness correction

The 7560683 host run passed 46/47 scenarios. The one failure records Home, five Down keys and Enter while the selector itself has focus and ContainerFromIndex returns null. The native method consumes navigation but cannot move to an item that has not been realized; Enter consequently has no focused item to commit. Routing to the correct method alone was insufficient.

DockComboBox now retains the original non-text navigation KeyRoutedEventArgs during this opening interval. Their original routes are marked handled. Once native layout creates the selected container, an opening-scoped LayoutUpdated callback establishes native item focus and dispatches those original events once, in order, through base.OnKeyDown/OnKeyUp. No new event object, synthetic browser input, custom selection algorithm, timer or test delay is introduced. Normal ready-popup input still takes the immediate native route.

Close, unload, retemplating, changed item collections/sources, editing mode, disablement or a transferred focus owner invalidate pending input. Storage is limited to 256 key transitions; a template that never realizes items cancels its opening rather than accumulating input indefinitely. A subsequent text/modifier interaction abandons pending navigation so it cannot run under a different native input context. Editable selectors keep native behavior.

All existing 107 product scenarios and 100 focus cycles remain mandatory, especially the twelve metadata-edit/new-document/immediate-popup-key sequences and original inspector case. This correction requires final-head browser verification; the previous 106/107 pass is not a release result. The sample's opt-in navigation trace captures only navigation/focus metadata, not document text, and is disabled in normal sessions.

Primary source: Uno ComboBox.custom.cs TryMoveKeyboardFocus and ComboBox.partial.mux.cs PopupKeyDown at upstream commit 951b7b464b8372daa95dae6eeedf9ac6c8109b90. Those methods require actual ComboBoxItem containers for navigation and Enter commit. This integration does not claim every platform's selector behavior or full Actipro docking parity.
