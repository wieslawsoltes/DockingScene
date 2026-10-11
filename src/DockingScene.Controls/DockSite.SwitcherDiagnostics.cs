namespace DockingScene.Controls;

public partial class DockSite
{
    public string? SwitcherSelectedId => _switcher?.SelectedId;
    public bool IsSwitcherInputScoped => _switcher?.HasInputScope == true;
}
