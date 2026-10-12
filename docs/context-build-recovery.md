# Context presenter build recovery

The 2e92292 candidate failed desktop compilation before browser execution. Its direct-visual presenter used an impossible type pattern (the pinned Uno DataTemplate.LoadContent already returns UIElement) and called a nonexistent ContentLease.Detach method. The correction uses the actual return type and validates ownership instead of silently stealing a template root from another parent.

The presenter now clears unused plain content on entry into template mode. A local null DataContext is explicitly set even when the detached template root currently reads null: this prevents inherited visual data reappearing during attachment. Version checks surround application callbacks and visual attachment without allocating a closure on every projection. Nested data/template updates share a depth counter.

All preceding tests remain required, including actual Description writeback, visual-data suppression, retained toggle state, reentrant host release, exact highlight pixels, and the 100-cycle focus test. Runtime success must be taken from the current head's CI, not the earlier passing model subset. The local execution tools were unavailable during this recovery; no new local build, working-tree audit or browser result is claimed.
