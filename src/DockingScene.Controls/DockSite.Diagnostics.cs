using Microsoft.UI.Xaml.Input;

namespace DockingScene.Controls;

public partial class DockSite
{
    private readonly Queue<string> _interactionLog = new();
    /// <summary>Opt-in bounded input tracing. Never stores document content.</summary>
    public bool InputDiagnosticsEnabled { get; set; }
    public IReadOnlyList<string> InteractionLog => _interactionLog.ToArray();
    private void TraceInput(string phase, PointerRoutedEventArgs e)
    {
        if (!InputDiagnosticsEnabled) return;
        _interactionLog.Enqueue($"{phase}; source={e.OriginalSource?.GetType().Name}; captures={_canvas.PointerCaptures?.Count ?? 0}; active={IsInteractionActive}; revision={Manager.Layout.Revision}");
        while (_interactionLog.Count > 32) _interactionLog.Dequeue();
    }
}
