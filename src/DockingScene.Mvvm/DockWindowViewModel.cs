using System.ComponentModel;
using DockingScene.Core;

namespace DockingScene.Mvvm;

public sealed class DockWindowViewModel : INotifyPropertyChanged
{
    private readonly DockWorkspaceViewModel _owner;
    private DockWindow _model;
    private WindowLocation _location = new(WindowPlacement.Hidden, null, null, DockSide.Center, -1);
    private bool _active, _detached;
    private object? _content;
    public string Id => _model.Id;
    public WindowKind Kind => _model.Kind;
    public string Title { get => _model.Title; set => Update(window => window with { Title = value }); }
    public string Description { get => _model.Description; set => Update(window => window with { Description = value }); }
    public bool IsModified { get => _model.IsModified; set => Update(window => window with { IsModified = value }); }
    public bool IsReadOnly { get => _model.IsReadOnly; set => Update(window => window with { IsReadOnly = value }); }
    public TabDisplayMode DisplayMode { get => _model.DisplayMode; set => Update(window => window with { DisplayMode = value }); }
    public DockCapabilities Capabilities { get => _model.Capabilities; set => Update(window => window with { Capabilities = value }); }
    public WindowPlacement Placement => _location.Placement;
    public string? GroupId => _location.GroupId;
    public string? FloatingHostId => _location.HostId;
    public bool IsRegistered => !_detached && !_owner.IsDisposed;
    public bool IsOpen { get => IsRegistered && Placement != WindowPlacement.Hidden; set { EnsureRegistered(); _owner.Execute(value ? _owner.Manager.Open(Id) : _owner.Manager.Close(Id)); } }
    public bool IsActive { get => _active && IsRegistered; set { EnsureRegistered(); if (value) _owner.Execute(IsOpen ? _owner.Manager.Activate(Id) : _owner.Manager.Open(Id)); } }
    public object? Content { get => _content; set { if (ReferenceEquals(_content, value)) return; _content = value; PropertyChanged?.Invoke(this, new(nameof(Content))); } }
    public DockCommand OpenCommand { get; }
    public DockCommand CloseCommand { get; }
    public DockCommand ActivateCommand { get; }
    public DockCommand FloatCommand { get; }
    public DockCommand DockCommand { get; }
    public DockCommand AutoHideCommand { get; }
    public DockCommand PinCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal DockWindowViewModel(DockWorkspaceViewModel owner, DockWindow model)
    {
        _owner = owner; _model = model;
        OpenCommand = new(() => Run(() => owner.Manager.Open(Id)), () => IsRegistered && !IsOpen);
        CloseCommand = new(() => Run(() => owner.Manager.Close(Id)), () => IsOpen && Allows(DockCapabilities.Close));
        ActivateCommand = new(() => Run(() => owner.Manager.Activate(Id)), () => IsOpen);
        FloatCommand = new(() => Run(() => owner.Manager.Float(Id)), () => Allows(DockCapabilities.Float, true));
        DockCommand = new(() => Run(() => owner.Manager.Dock(Id)), () => Allows(DockCapabilities.Dock, true));
        AutoHideCommand = new(() => Run(() => owner.Manager.AutoHide(Id, DockSide.Left)), () => Kind == WindowKind.Tool && Allows(DockCapabilities.AutoHide, true));
        PinCommand = new(() => Run(() => Placement == WindowPlacement.AutoHidden ? owner.Manager.PinAutoHide(Id) : owner.Manager.SetPinned(Id, DisplayMode != TabDisplayMode.Pinned)),
            () => IsRegistered && (Kind == WindowKind.Document || Placement == WindowPlacement.AutoHidden && Allows(DockCapabilities.Dock, true)));
    }
    private bool Allows(DockCapabilities capability, bool structural = false) => IsRegistered && (!structural || !_owner.Manager.Layout.IsLayoutLocked) && _owner.Manager.Allows(Id, capability);
    private void EnsureRegistered() { _owner.EnsureAlive(); if (_detached) throw new InvalidOperationException("The docking window is no longer registered."); }
    private DockResult Run(Func<DockResult> action) { EnsureRegistered(); return _owner.Execute(action()); }
    private void Update(Func<DockWindow, DockWindow> update) { EnsureRegistered(); _owner.Execute(_owner.Manager.UpdateWindow(Id, update)); }
    internal void Refresh(DockLayout snapshot)
    {
        _model = snapshot.Windows[Id]; _location = DockTree.Locate(snapshot, Id); _active = snapshot.ActiveWindowId == Id;
        PropertyChanged?.Invoke(this, new(null)); NotifyCommands();
    }
    internal void Detach() { _detached = true; PropertyChanged?.Invoke(this, new(null)); NotifyCommands(); }
    private void NotifyCommands()
    {
        foreach (var command in new[] { OpenCommand, CloseCommand, ActivateCommand, FloatCommand, DockCommand, AutoHideCommand, PinCommand }) command.NotifyCanExecuteChanged();
    }
}
