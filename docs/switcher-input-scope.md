# Switcher input scope and fast-key handling

The first live run at `50a2b8e` passed the corrected auto-hide test and all 18 standalone-tab scenarios, but the MRU Enter test timed out. Its screenshot showed the switcher still open with **Solution Explorer** selected rather than the initial **Output** selection. The active application window remained Welcome. No application exception was recorded.

The launcher button was outside DockSite. Focus was deferred from the switcher's Loaded handler through the dispatcher. During that interval, Enter could activate the still-focused launcher a second time, causing `ShowSwitcher(1)` to advance the provisional selection instead of committing it. This is a control input race, distinct from the diagnostic-snapshot race fixed in PR #3.

## Correction

DockSite attaches a temporary input scope to its existing XamlRoot content **before** adding the switcher visual. PreviewKeyDown handles Enter, Escape and Up/Down using the tunneling route, before an external button or editor can handle the key. PreviewKeyUp handles Ctrl release. The bubbling fallback skips a key that the active scope already handled, preventing double navigation.

The switcher takes focus directly during Loaded rather than placing focus work behind another dispatcher item. Pointer selection, Commit and Cancel release the scope before notifying consumers. Explicit dismissal, manager reset and owner unload also remove the handlers. A completed switcher ignores additional commit/cancel calls, avoiding reentrant duplicate activation.

This uses the existing Uno Skia/WinUI preview event model; it does not intercept browser DOM events or inject focus through JavaScript. Reference behavior: [UIElement.PreviewKeyDown](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.previewkeydown) and [Uno focus management](https://platform.uno/docs/articles/features/focus-management.html).

## Regression coverage

The existing MRU Enter and Escape scenarios are unchanged. Three additional real-input scenarios check six immediate Enter openings and six immediate Escape openings **without waiting for overlay geometry or focus**, one-step Up/Down movement, scope detachment, ordinary editor newline input after dismissal, and an F8/Enter switch that must not edit the background document.

`switcherSelection` and `switcherInputScoped` are read-only observations in the opt-in sample diagnostics. They are not hooks for changing the model. Native desktop, cross-browser and screen-reader behavior still require additional validation; these tests do not certify every platform's focus system or complete Actipro parity.

## Enter character dispatch follow-up

A later accelerated projection run exposed an extra newline in the previously focused editor after committing Enter on KeyDown. CharacterReceived may follow a handled KeyDown independently. The switcher now retains focus and its modal scope through Enter-down and commits on Enter-up. A held-key browser regression checks both the open scope before release and unchanged background text after completion. Cancellation and direct programmatic Commit keep their existing behavior.
