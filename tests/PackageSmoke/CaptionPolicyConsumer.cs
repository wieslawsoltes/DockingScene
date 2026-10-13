using System.Runtime.CompilerServices;
using DockingScene.Core;
using DockingScene.Serialization;

internal static class CaptionPolicyConsumer
{
    [ModuleInitializer]
    internal static void Validate()
    {
        var manager = new DockManager();
        manager.Register(new DockWindow("caption-policy", "Instance", WindowKind.Tool)
        { HasTitleBar = false, HasOptionsButton = true, HasCloseButton = null, HasToggleAutoHideButton = false }).ThrowIfFailed();
        var json = DockLayoutSerializer.Serialize(manager.Layout);
        var restored = DockLayoutSerializer.Deserialize(json);
        var model = restored.Layout?.Windows["caption-policy"] ?? throw new InvalidOperationException(restored.Error);
        if (model.HasCloseButton is not null || ToolCaptionSettings.Default.Resolve(model) != new ToolCaptionSettings(false, true, true, false))
            throw new InvalidOperationException("Packaged instance caption inheritance failed.");
    }
}
