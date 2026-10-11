using System.Collections.Immutable;

namespace DockingScene.Core;

/// <summary>Validated immutable transactions, cancellable lifecycle changes and ordered notifications.
/// Content and dispatchers belong to adapters, never to this UI-independent engine.</summary>
public sealed partial class DockManager
{
    private readonly object _gate = new();
    private readonly List<DockLayout> _undo = [];
    private readonly List<DockLayout> _redo = [];
    private readonly Queue<DockChangedEventArgs> _notifications = new();
    private DockLayout _layout;
    private bool _changing;
    private bool _dispatching;
    public DockPolicy Policy { get; }
    public DockLayout Layout { get { lock (_gate) return _layout; } }
    public bool CanUndo { get { lock (_gate) return _undo.Count > 0; } }
    public bool CanRedo { get { lock (_gate) return _redo.Count > 0; } }
    public event EventHandler<DockChangingEventArgs>? Changing;
    public event EventHandler<DockChangedEventArgs>? Changed;
    /// <summary>Reports observer exceptions after a committed change, without misreporting that change as failed.</summary>
    public event Action<Exception>? ObserverFaulted;

    public DockManager(DockLayout? layout = null, DockPolicy? policy = null)
    {
        Policy = policy ?? new();
        if (Policy.HistoryLimit < 0 || Policy.MaximumWindows < 1 || (Policy.AllowedCapabilities & ~DockCapabilities.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(policy));
        _layout = layout ?? new();
        DockLayoutValidator.Validate(_layout, Policy.MaximumWindows);
    }
    public bool Allows(string id, DockCapabilities capability)
    {
        var layout = Layout;
        return layout.Windows.TryGetValue(id, out var window) && (window.Capabilities & Policy.AllowedCapabilities & capability) == capability;
    }
    private void Require(DockLayout layout, IEnumerable<string> ids, DockCapabilities capability, bool structural = true)
    {
        if (structural && layout.IsLayoutLocked) throw new InvalidOperationException("The layout is locked.");
        foreach (var id in ids)
            if (!layout.Windows.TryGetValue(id, out var window) || (window.Capabilities & Policy.AllowedCapabilities & capability) != capability)
                throw new InvalidOperationException("Window does not allow " + capability + ": " + id);
    }
    private DockResult Apply(string command, Func<DockLayout, DockLayout> change, long? expectedRevision = null,
        ImmutableArray<string> closing = default, bool recordHistory = true, Action<DockLayout>? commitJournal = null)
    {
        bool drain = false;
        DockResult result;
        lock (_gate)
        {
            if (_changing) return new(false, _layout.Revision, "Reentrant changes during Changing are not allowed.");
            if (expectedRevision is { } revision && revision != _layout.Revision) return new(false, _layout.Revision, "Stale layout revision.");
            var before = _layout;
            _changing = true;
            try
            {
                var candidate = change(before);
                if (ReferenceEquals(candidate, before) && commitJournal is null) return new(true, before.Revision);
                // Validate structure before any tree-based activation repair, including for Restore.
                DockLayoutValidator.Validate(candidate, Policy.MaximumWindows, validateActivation: false);
                var after = DockTree.RepairActivation(candidate) with { Revision = checked(before.Revision + 1) };
                DockLayoutValidator.Validate(after, Policy.MaximumWindows);
                var hiddenBefore = before.HiddenWindows.ToHashSet(StringComparer.Ordinal);
                var hiddenAfter = after.HiddenWindows.ToHashSet(StringComparer.Ordinal);
                var closed = before.Windows.Keys.Where(id => !hiddenBefore.Contains(id) &&
                    (!after.Windows.ContainsKey(id) || hiddenAfter.Contains(id))).ToImmutableArray();
                if (!closing.IsDefault) closed = closed.Concat(closing.Where(id => before.Windows.ContainsKey(id) && !hiddenBefore.Contains(id))).Distinct(StringComparer.Ordinal).ToImmutableArray();
                var changing = new DockChangingEventArgs(command, before, after, closed);
                Changing?.Invoke(this, changing);
                if (changing.Cancel) return new(false, before.Revision, changing.Reason ?? "Change cancelled.");
                if (commitJournal is not null) commitJournal(before);
                else if (recordHistory)
                {
                    if (Policy.HistoryLimit > 0) { _undo.Add(before); TrimHistory(_undo); }
                    _redo.Clear();
                }
                _layout = after;
                _notifications.Enqueue(new(command, before, after));
                if (!_dispatching) { _dispatching = true; drain = true; }
                result = new(true, after.Revision);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or OverflowException or NullReferenceException or KeyNotFoundException)
            { result = new(false, before.Revision, error.Message); }
            finally { _changing = false; }
        }
        if (drain) DrainNotifications();
        return result;
    }
    private void DrainNotifications()
    {
        while (true)
        {
            DockChangedEventArgs notification;
            lock (_gate)
            {
                if (_notifications.Count == 0) { _dispatching = false; return; }
                notification = _notifications.Dequeue();
            }
            foreach (var observer in Changed?.GetInvocationList() ?? [])
            {
                try { ((EventHandler<DockChangedEventArgs>)observer)(this, notification); }
                catch (Exception error)
                {
                    foreach (var handler in ObserverFaulted?.GetInvocationList() ?? [])
                        try { ((Action<Exception>)handler)(error); } catch { /* An error sink cannot prevent later observers from running. */ }
                }
            }
        }
    }
    private void TrimHistory(List<DockLayout> history)
    {
        while (history.Count > Policy.HistoryLimit) history.RemoveAt(0);
    }
    public DockResult Undo() => NavigateHistory(_undo, _redo, "Undo");
    public DockResult Redo() => NavigateHistory(_redo, _undo, "Redo");
    private DockResult NavigateHistory(List<DockLayout> source, List<DockLayout> destination, string command) => Apply(command,
        _ => source.Count > 0 ? source[^1] : throw new InvalidOperationException("No history available."),
        recordHistory: false, commitJournal: before => { source.RemoveAt(source.Count - 1); destination.Add(before); TrimHistory(destination); });

    /// <summary>Executes several ordinary commands as one validated, cancellable, undoable transaction.
    /// A rejected intermediate command aborts the whole batch and no trial notifications escape.</summary>
    public DockResult Transaction(string command, IEnumerable<Func<DockManager, DockResult>> operations, long? expectedRevision = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(operations);
        return Apply(command, before =>
        {
            var trial = new DockManager(before, Policy with { HistoryLimit = 0 });
            foreach (var operation in operations)
            {
                if (operation is null) throw new ArgumentException("A transaction cannot contain a null operation.");
                operation(trial).ThrowIfFailed();
            }
            return trial.Layout;
        }, expectedRevision);
    }
    public DockResult Restore(DockLayout layout, long? expectedRevision = null) => Apply("Restore", _ => layout, expectedRevision);
    public DockResult SetLayoutLocked(bool locked) => Apply("SetLayoutLocked", layout => layout with { IsLayoutLocked = locked });
}
