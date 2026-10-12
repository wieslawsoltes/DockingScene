using System.ComponentModel;
using System.Windows.Input;
using DockingScene.Core;
using Microsoft.UI.Xaml;

namespace DockingScene.Controls;

public sealed partial class StandardMdiHost : INotifyPropertyChanged
{
    private DockLayout? _primarySnapshot;
    private string[]? _primaryIds;
    private (DockCapabilities Allowed, bool Minimize, bool Maximize, bool Frames) _primaryOptions;
    private MdiPrimaryState _primaryState, _publishedPrimaryState;
    private PrimaryCommand? _minimizeCommand, _maximizeCommand, _restoreCommand, _closeCommand;
    public static readonly DependencyProperty AreMaximizedWindowFramesVisibleProperty = DependencyProperty.Register(
        nameof(AreMaximizedWindowFramesVisible), typeof(bool), typeof(StandardMdiHost), new PropertyMetadata(true, OptionsChanged));
    public bool AreMaximizedWindowFramesVisible { get => (bool)GetValue(AreMaximizedWindowFramesVisibleProperty); set => SetValue(AreMaximizedWindowFramesVisibleProperty, value); }
    public DockWindow? PrimaryWindow => ResolvePrimaryState().Window;
    public bool IsExternalMaximizedUIRequired => ResolvePrimaryState().RequiresExternalChrome;
    public ICommand MinimizePrimaryWindowCommand => _minimizeCommand ??= new(this, PrimaryAction.Minimize);
    public ICommand MaximizePrimaryWindowCommand => _maximizeCommand ??= new(this, PrimaryAction.Maximize);
    public ICommand RestorePrimaryWindowCommand => _restoreCommand ??= new(this, PrimaryAction.Restore);
    public ICommand ClosePrimaryWindowCommand => _closeCommand ??= new(this, PrimaryAction.Close);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? PrimaryWindowChanged;

    private MdiPrimaryState ResolvePrimaryState()
    {
        if (!_active || _manager is null) return default;
        var layout = _manager.Layout;
        var options = (Allowed: _manager.Policy.AllowedCapabilities, Minimize: CanWindowsMinimize, Maximize: CanWindowsMaximize, Frames: AreMaximizedWindowFramesVisible);
        if (!ReferenceEquals(layout, _primarySnapshot) || !ReferenceEquals(_primaryIds, _ids) || options != _primaryOptions)
        {
            _primaryState = MdiPrimaryState.Create(layout, _ids, options.Allowed,
                options.Minimize, options.Maximize, options.Frames);
            _primarySnapshot = layout; _primaryIds = _ids; _primaryOptions = options;
        }
        return _primaryState;
    }
    private void PublishPrimaryState()
    {
        var state = ResolvePrimaryState(); var previous = _publishedPrimaryState;
        _publishedPrimaryState = state;
        _minimizeCommand?.Refresh(); _maximizeCommand?.Refresh(); _restoreCommand?.Refresh(); _closeCommand?.Refresh();
        if (previous.Window != state.Window) PropertyChanged?.Invoke(this, new(nameof(PrimaryWindow)));
        if (previous.RequiresExternalChrome != state.RequiresExternalChrome)
            PropertyChanged?.Invoke(this, new(nameof(IsExternalMaximizedUIRequired)));
        if (previous != state) PrimaryWindowChanged?.Invoke();
    }
    private enum PrimaryAction { Minimize, Maximize, Restore, Close }
    private bool CanExecutePrimary(PrimaryAction action)
    {
        var state = ResolvePrimaryState();
        return action switch { PrimaryAction.Minimize => state.CanMinimize, PrimaryAction.Maximize => state.CanMaximize,
            PrimaryAction.Restore => state.CanRestore, _ => state.CanClose };
    }
    private void ExecutePrimary(PrimaryAction action)
    {
        if (!CanExecutePrimary(action) || _manager is not { } manager || ResolvePrimaryState().Window is not { } window) return;
        var revision = manager.Layout.Revision;
        Submit(manager.Transaction(action + "PrimaryWindow", [m => action switch
        {
            PrimaryAction.Minimize => m.SetMdiState(window.Id, MdiWindowState.Minimized),
            PrimaryAction.Maximize => m.SetMdiState(window.Id, MdiWindowState.Maximized),
            PrimaryAction.Restore => m.SetMdiState(window.Id, MdiWindowState.Normal),
            _ => m.Close(window.Id)
        }], revision));
    }
    private sealed class PrimaryCommand(StandardMdiHost owner, PrimaryAction action) : ICommand
    {
        private bool _last = owner.CanExecutePrimary(action);
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => owner.CanExecutePrimary(action);
        public void Execute(object? parameter) => owner.ExecutePrimary(action);
        public void Refresh()
        {
            bool next = CanExecute(null); if (next == _last) return;
            _last = next; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
