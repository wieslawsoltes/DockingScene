# Isolated browser gates

The Uno build still runs every model specification, compiles desktop, publishes WebAssembly, packages all six libraries and validates the isolated package consumers. It produces one browser-test-site artifact and records its exact checkout SHA. Browser jobs check out that same SHA and verify the artifact's build-info.json before testing.

Docking, standalone tabs, host/Metro/MDI and the existing 100-cycle focus investigation run on separate GitHub runners. They do not compete for one runner's CPU or software GPU, and a slow focus investigation no longer delays the start of unrelated tests. fail-fast is disabled. No scenario, key sequence, pointer action, pixel assertion or per-assertion timeout is removed or relaxed. This is scheduling parallelism, not a measured application rendering speedup.

Pages depends on BOTH the build and the complete browser matrix. An uploaded Pages artifact cannot deploy after a failed or skipped matrix member. After deployment, four independent live jobs verify the exact served SHA and rerun all suites, including 100 focus cycles. Their artifacts are named browser-validation-{docking,tabs,host,focus} and deployed-browser-validation-{docking,tabs,host,focus}. Build logs and isolated-package outputs remain separate artifacts.

The per-job time budget covers installation and a single suite rather than accumulating every suite in one job. Timings are reported by Actions; no wall-clock improvement is claimed before comparing actual runs. Local runtime validation is not claimed by this workflow change.

The aggregate required check retains the original name `build`. It runs after compilation and every browser matrix member, and fails if either dependency failed, skipped or was cancelled. Deployment also requires this aggregate. Node 22 remains explicitly pinned in the compile job as well as the browser jobs.

The minimized-restore scenario retains all original assertions and now repeats them eight times without an added delay, protecting coherent hit rectangles during rapid state changes.
