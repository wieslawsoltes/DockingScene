using System.Windows.Input;
using DockingScene.Core;
using DockingScene.Themes;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public sealed partial class StandardMdiHost
{
    private PrimaryCommand? _minimizePrimary, _maximizePrimary, _restorePrimary, _closePrimary;
    private MdiPrimaryWindowState _notifiedPrimary, _cachedPrimary;
    private DockLayout? _primarySnapshot;
    private string[]? _primaryIds;
    private DockCapabilities _primaryAllowed;
    private bool _primaryCanMinimize, _primaryCanMaximize;
    public static readonly DependencyProperty AreMaximizedWindowFramesVisibleProperty = DependencyProperty.Register(
        nameof(AreMaximizedWindowFramesVisible), typeof(bool), typeof(StandardMdiHost), new PropertyMetadata(true, OptionsChanged));
    public bool AreMaximizedWindowFramesVisible { get => (bool)GetValue(AreMaximizedWindowFramesVisibleProperty); set => SetValue(AreMaximizedWindowFramesVisibleProperty, value); }
    public ICommand MinimizePrimaryWindowCommand => _minimizePrimary ??= new(this, PrimaryAction.Minimize);
    public ICommand MaximizePrimaryWindowCommand => _maximizePrimary ??= new(this, PrimaryAction.Maximize);
    public ICommand RestorePrimaryWindowCommand => _restorePrimary ??= new(this, PrimaryAction.Restore);
    public ICommand ClosePrimaryWindowCommand => _closePrimary ??= new(this, PrimaryAction.Close);
    public DockTheme PresentationTheme => _theme;
    public DockWindow? PrimaryWindow
    {
        get
        {
            if (!_active || _manager is null) return null;
            var layout = _manager.Layout; var state = ResolvePrimary(layout);
            return state.Id is { } id ? layout.Windows[id] : null;
        }
    }
    /// <summary>Raised after a host projection or release. Subscribers must detach
    /// when their external toolbar or application owner is unloaded.</summary>
    public event EventHandler? ProjectionChanged;
    private MdiPrimaryWindowState ResolvePrimary(DockLayout layout)
    {
        if (!_active || _manager is null) return default;
        var allowed = _manager.Policy.AllowedCapabilities;
        bool minimize = CanWindowsMinimize, maximize = CanWindowsMaximize;
        if (ReferenceEquals(_primarySnapshot, layout) && ReferenceEquals(_primaryIds, _ids) && _primaryAllowed == allowed &&
            _primaryCanMinimize == minimize && _primaryCanMaximize == maximize) return _cachedPrimary;
        var result = MdiPrimaryWindow.Resolve(layout, _ids, allowed, minimize, maximize);
        _primarySnapshot = layout; _primaryIds = _ids; _primaryAllowed = allowed;
        _primaryCanMinimize = minimize; _primaryCanMaximize = maximize; _cachedPrimary = result;
        return result;
    }
    private enum PrimaryAction { Minimize, Maximize, Restore, Close }
    private static bool AllowsPrimary(MdiPrimaryWindowState state, PrimaryAction action) => action switch
    {
        PrimaryAction.Minimize => state.CanMinimize,
        PrimaryAction.Maximize => state.CanMaximize,
        PrimaryAction.Restore => state.CanRestore,
        _ => state.CanClose
    };
    private void ExecutePrimary(PrimaryAction action)
    {
        if (!_active || _manager is null) return;
        var manager = _manager; var before = manager.Layout; var state = ResolvePrimary(before);
        if (state.Id is not { } id || !AllowsPrimary(state, action)) return;
        Submit(manager.Transaction("PrimaryMdi" + action, new Func<DockManager, DockResult>[] { m => action switch
        {
            PrimaryAction.Minimize => m.SetMdiState(id, MdiWindowState.Minimized),
            PrimaryAction.Maximize => m.SetMdiState(id, MdiWindowState.Maximized),
            PrimaryAction.Restore => m.SetMdiState(id, MdiWindowState.Normal),
            _ => m.Close(id)
        } }, before.Revision));
    }
    private void NotifyProjection()
    {
        if (!_active) { _primarySnapshot = null; _primaryIds = null; }
        var state = _active && _manager is not null ? ResolvePrimary(_manager.Layout) : default;
        if (state != _notifiedPrimary)
        {
            _notifiedPrimary = state;
            _minimizePrimary?.Notify(); _maximizePrimary?.Notify(); _restorePrimary?.Notify(); _closePrimary?.Notify();
        }
        ProjectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private sealed class PrimaryCommand(StandardMdiHost owner, PrimaryAction action) : ICommand
    {
        public bool CanExecute(object? parameter) => owner._active && owner._manager is not null && AllowsPrimary(owner.ResolvePrimary(owner._manager.Layout), action);
        public void Execute(object? parameter) => owner.ExecutePrimary(action);
        public event EventHandler? CanExecuteChanged;
        public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
