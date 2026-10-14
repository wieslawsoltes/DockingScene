# Cancellable validation aggregate

The superseded PR #23 model-only Uno run 38539586647 cancelled its old browser
jobs after a newer commit arrived, but its `if: always()` aggregate remained
queued. The current-head run 38541317529 was pending in the same concurrency
group. The observed run/job states are consistent with the documented fact that
`always()` remains true during cancellation; they do not measure GitHub's wider
runner-queue latency.

The aggregate required check retains the name `build`, its dependencies on both
compilation and the browser matrix, and both exact success checks. Its job-level
condition is now `${{ !cancelled() }}`. It still runs after a failed or skipped
dependency in an uncancelled workflow and rejects anything except two successes.
It does not keep obsolete cancelled runs alive solely to obtain a runner for this
tiny aggregate. A five-minute execution timeout bounds an unexpected hung gate;
this is not a queue-time guarantee. Step-level always-upload diagnostic actions
remain unchanged.

All five pre-deployment and live suites, their assertions/timeouts, fail-fast
settings, and deployment dependencies remain. No branch protection, permissions,
required-check names or test success criteria are relaxed. Cancellation is not
claimed as completed validation and no cancelled candidate is merged here.

`tests/ci/aggregate-gate.contract.mjs` reads the actual YAML shell block and runs
all 16 combinations of success/failure/cancelled/skipped build and browser
results under bash pipefail. Only success/success may return zero. It also checks
the required name, condition, dependencies and both five-suite matrices. The
Uno workflow runs this contract before model validation. It does not emulate
GitHub scheduling or claim proof of live cancellation until a relevant run is
observed. Existing runs retain the workflow definitions they started with; this
change does not retroactively edit or force-cancel them.

Primary references:
- https://docs.github.com/actions/reference/evaluate-expressions-in-workflows-and-actions#always
- https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-cancellation
