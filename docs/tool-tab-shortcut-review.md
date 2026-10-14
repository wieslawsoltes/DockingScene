# Single-tool visibility shortcut review

At head 0f251a3, run 38543070113 built and packaged successfully and passed every
model suite. The caption runner passed 19/20 cases. Its failure diagnostic shows
the sample toolbar button retained keyboard focus, the floating frame was in a
valid captured live resize, and the single-tool policy remained Show after F4.
No exception or model rejection was recorded. The failing assertion is retained.

The sample previously registered a KeyboardAccelerator lazily when its policies
menu was first opened. It now registers one sample-scoped routed KeyDown handler
at construction, following the input pattern used by the existing floating drag
sample. Keyboard focus can remain on the toolbar while the frame owns pointer
capture. No root-global shortcut, model diagnostic command or synthetic invocation
is added. Handled events and other keys are ignored; unloading the sample removes
it from the input route. Opening menus cannot accumulate another handler.

A new physical-input case exercises F4 before the first policies menu, repeated
menu openings and successive policy round trips with stable model revision and
retained controls. The original captured-resize cancellation case is unchanged.
No timeouts, success criteria or browser gates were relaxed. Current-head CI must
validate this source fix before merge; the failure alone does not identify which
Uno accelerator lifecycle behavior prevented the previous event from firing.

Input reference: https://learn.microsoft.com/windows/apps/develop/input/keyboard-accelerators
