using System.Windows.Input;
using DockingScene.Core;

namespace DockingScene.Mvvm;

/// <summary>A reusable ICommand that preserves the result of a cancellable docking operation.</summary>
public sealed class DockCommand(Func<DockResult> execute, Func<bool>? canExecute = null) : ICommand
{
    public DockResult? LastResult { get; private set; }
    public event EventHandler? CanExecuteChanged;
    public event Action<DockResult>? Executed;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        LastResult = execute(); Executed?.Invoke(LastResult.Value);
    }
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
