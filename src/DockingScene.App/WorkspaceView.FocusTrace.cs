using System.Diagnostics;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DockingScene.App;

public sealed partial class WorkspaceView
{
    private readonly Queue<string> _managedFocusTrace = new();
    private readonly ConditionalWeakTable<TextBox, object> _focusTraceEditors = new();
    public void EnableFocusTracing()
    {
        _site.VisualsUpdated += () =>
        {
            if (_diagnosticsEnabled != true || !_editors.TryGetValue("code", out var reference) || !reference.TryGetTarget(out var editor) || _focusTraceEditors.TryGetValue(editor, out _)) return;
            _focusTraceEditors.Add(editor, new()); ObserveEditorFocus(editor);
        };
    }
    private void ObserveEditorFocus(TextBox editor)
    {
        if (_diagnosticsEnabled != true) return;
        long generation = _workspaceGeneration;
        string Identity(DependencyObject? element) => element is null ? "null" : element.GetType().Name + ":" + AutomationProperties.GetAutomationId(element);
        void Record(string phase, bool stack = false)
        {
            if (_diagnosticsEnabled != true) return;
            _managedFocusTrace.Enqueue($"{Stopwatch.GetTimestamp()} {phase} gen={generation} current={_workspaceGeneration} state={editor.FocusState} loaded={editor.IsLoaded} parent={Identity(VisualTreeHelper.GetParent(editor))} focused={Identity(XamlRoot is null ? null : FocusManager.GetFocusedElement(XamlRoot) as DependencyObject)}\n" + (stack ? Environment.StackTrace : ""));
            while (_managedFocusTrace.Count > 24) _managedFocusTrace.Dequeue();
#if BROWSER_WASM
            Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.dockingSceneManagedFocusTrace = " + JsonSerializer.Serialize(_managedFocusTrace.ToArray()) + ";");
#endif
        }
        editor.RegisterPropertyChangedCallback(Control.FocusStateProperty, (_, _) => Record("FocusState", editor.FocusState == FocusState.Unfocused));
        editor.Unloaded += (_, _) => Record("Unloaded", true);
        editor.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => Record("PointerPressed")), true);
        editor.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => Record("PointerReleased")), true);
        editor.LosingFocus += (_, e) => Record("LosingFocus new=" + Identity(e.NewFocusedElement), true);
    }
}
