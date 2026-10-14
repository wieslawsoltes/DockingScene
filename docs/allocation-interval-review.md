# Isolated allocation intervals

PR #21 model run 38462636578 passed 16/17 group specifications but recorded 24
bytes in its 10,000 group measurements. The same run's 10,000 default-cache
queries recorded zero. Separately, main run 38461614550 failed the existing
MDI scroll allocation assertion; the same main source passed Validate 38461614635.
That old failure did not report the byte count. No deployment followed the failed
main run.

The benchmarks previously combined their counters, workloads and reporting in
one method. .NET runtime maintainers document that surrounding boxes may move
across GetAllocatedBytesForCurrentThread and recommend a no-inline measurement
method: https://github.com/dotnet/runtime/issues/96836. This is a plausible
measurement hazard, not proof identifying the allocator behind those failures.

The affected group/default-cache, scrolling and primary-caption tests now share
a test-only no-inline interval containing just counter reads and the fixed-count
workload. Delegates, timers, formatting and assertions are outside it. Warm-up
executes the same complete workload; the old scrolling warm-up omitted Reveal.
An empty control must report zero, and an escaping explicit array allocation must
report positive bytes. Control bytes are never subtracted from product results.

Every product interval still executes 10,000 calls and requires exactly zero
managed bytes; its output is also checked. There is no retry loop, tolerance,
GC suppression, JIT option override or runtime pinning. Runtime settings remain
default. Failures now include actual bytes, including the formerly opaque scroll
failure. Library code and all functional assertions are unchanged by this commit.

Fresh exact-head model and native CI is required before merge. A passed interval
would validate that bounded warmed workload, not establish a general claim about
allocation under every runtime, concurrent collection or GUI operation.
