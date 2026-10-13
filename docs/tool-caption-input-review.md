# Tool-caption native gesture capture review

Run 38447220608 at a9463ef completed 101 host cases with three failures: the first
three new tool-caption double-click cases. The other 98 host cases passed, as did
the separate tabs and focus gates. Failure screenshots/diagnostics showed a valid
painted title, no model revision change, no rejection, and no active interaction
after release. No JavaScript/runtime error was reported.

The title subscribed to native DoubleTapped, but BeginDockDrag captured every
pointer on the ancestor docking canvas. The caption recognizer did not receive
its release. Tool-caption and grip drags now retain capture on that native title
surface while their moved/released events continue to bubble to the existing
canvas state machine. Tabs and splitter drags retain their canvas owner.

A single explicit capture owner is used consistently by normal completion,
stale-layout/manager replacement cancellation, unload, Escape, capture-loss
checks and read-only diagnostics. It is cleared before releasing capture so a
synchronous capture-loss notification cannot resurrect an obsolete session.
There is no custom double-click timer or synthetic command invocation.

The existing three physical-input failure assertions, drag/capture/keyboard
suites, permission checks and all other gates remain unchanged. This document
records a source fix, not a runtime pass; a new exact-head CI run must validate
it before merge.
