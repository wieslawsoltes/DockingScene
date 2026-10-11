namespace DockingScene.Controls;

public partial class DockSite
{
    public bool SwitcherHasInputFocus => _switcher?.HasInputFocus == true;
    public string? SwitcherSelectedId => _switcher?.SelectedId;
    public bool IsSwitcherInputScoped => _switcher?.HasInputScope == true;
}
