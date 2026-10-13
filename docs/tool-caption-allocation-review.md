# Floating caption query allocation review

At input-fix revision dbfe7a7, the independent Validate job failed the strict
10,000-query zero-allocation caption case while the same model suite passed in
that revision's Uno build. The original failure message did not include the
observed bytes. No allocation allowance or retry-based pass was introduced.

Review also found every caption query re-scanned the complete floating-host
array and all members of its selected host even when the immutable layout had
not changed. The result now has an additional weak, reference-identity-keyed
snapshot projection. One cold construction resolves all hosts with one shared
activation index; warm queries perform a weak-table read and a dictionary read.
Construction is excluded from the existing allocation measurement, as before.
The projection value holds primary window metadata, not its source layout.
New metadata or activation snapshots get independent projections, including
snapshots that happen to have equal revision numbers. The map is immutable after
construction, so concurrent readers cannot see partially populated results.

The strict zero-byte assertion remains. Its failure now reports actual bytes and
selected ID; additional cases cover equal-revision isolation, simultaneous host
queries and unknown-host rejection. This is an algorithmic cache improvement,
not a claim that the intermittent failure's runtime allocator was identified.
A fresh exact-head model and native-browser run remains required before merge.
