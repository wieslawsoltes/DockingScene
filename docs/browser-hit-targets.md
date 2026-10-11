# Atomic browser hit-target observations

The initial live-site run after PR #2 passed all 18 standalone-tab scenarios and 19 of the 20 docking scenarios. The remaining auto-hide scenario failed in the test harness before its drag began, with `Cannot read properties of undefined (reading 'width')`.

The Playwright trace shows the exact sequence: `waitForFunction` observed `autohide-resize:toolbox`, then a separate evaluation retrieved diagnostic sequence 160 at layout revision 18 without that target. The popup was still open. A subsequent captured state showed the same resize grip again with positive bounds. Treating two separately read snapshots as one observation caused the failure; the test did not report an application exception.

Both suites now use `readHitBounds`. Its polling predicate validates finite coordinates and positive extents and returns a new plain rectangle object in the same browser tick. The runner reads that retained JSHandle result and releases it in `finally`; it does not re-read the mutable diagnostic snapshot after the predicate completes.

This does not make old coordinates permanently valid or bypass interaction validation. The same pointer actions, capture checks, resize thresholds, placement checks and timeouts still run. If a control genuinely cannot be used, the corresponding state-transition assertion still fails. No application model or DOM mutation was introduced.

Pure Node contracts run before each browser suite. They reproduce snapshot replacement and source-object mutation between predicate completion and result serialization, reject incomplete/non-finite/non-positive rectangles, preserve timeout/serialization errors and require handle disposal. These are harness fixtures, not mutations of the actual browser application. The previous failed results and trace remain available in workflow 38135650441 for comparison.
