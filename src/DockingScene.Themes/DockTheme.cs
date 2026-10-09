namespace DockingScene.Themes;

/// <summary>UI-independent design tokens. Values are interpreted by the renderer, not tied to XAML resources.</summary>
public sealed record DockTheme(string Name, string Background, string Surface, string Text, string MutedText,
    string Border, string Accent, string AccentText, string Hover, string InactiveTab, string ActiveToolTitle,
    string EditorBackground, string GuideBackground, bool IsDark = false, bool IsHighContrast = false)
{
    public double FontSize { get; init; } = 12;
    public double TabHeight { get; init; } = 26;
    public double TitleHeight { get; init; } = 24;
    public double SplitterSize { get; init; } = 5;
    public static DockTheme MetroLight { get; } = new("Metro Light", "#F0F0F0", "#FFFFFF", "#252526", "#666666", "#B8B8B8", "#007ACC", "#FFFFFF", "#D9EAF7", "#E7E7E7", "#007ACC", "#FFFFFF", "#F4F8FC");
    public static DockTheme MetroDark { get; } = new("Metro Dark", "#2D2D30", "#252526", "#F1F1F1", "#AAAAAA", "#555558", "#007ACC", "#FFFFFF", "#3E4C59", "#333337", "#007ACC", "#1E1E1E", "#333D48", true);
    public static DockTheme OfficeBlue { get; } = new("Office Blue", "#D9E4F3", "#FFFFFF", "#253B5B", "#5E7190", "#8CA6C9", "#2B579A", "#FFFFFF", "#C5DCF7", "#D4E0EF", "#2B579A", "#FFFFFF", "#EFF5FD");
    public static DockTheme HighContrast { get; } = new("High Contrast", "#000000", "#000000", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFF00", "#000000", "#003A70", "#000000", "#FFFF00", "#000000", "#000000", true, true);
    public static IReadOnlyList<DockTheme> BuiltIn { get; } = [MetroLight, MetroDark, OfficeBlue, HighContrast];
}

public sealed record DockStrings
{
    public string Close { get; init; } = "Close";
    public string CloseAll { get; init; } = "Close all";
    public string CloseOthers { get; init; } = "Close other documents";
    public string Float { get; init; } = "Float";
    public string Dock { get; init; } = "Dock";
    public string AutoHide { get; init; } = "Auto hide";
    public string Pin { get; init; } = "Pin";
    public string Unpin { get; init; } = "Unpin";
    public string MoveToWorkspace { get; init; } = "Move to document area";
    public string NewHorizontalGroup { get; init; } = "New horizontal tab group";
    public string NewVerticalGroup { get; init; } = "New vertical tab group";
    public string Restore { get; init; } = "Restore";
    public string Minimize { get; init; } = "Minimize";
    public string Maximize { get; init; } = "Maximize";
    public string Documents { get; init; } = "Active files";
    public string Tools { get; init; } = "Active tool windows";
    public string WindowList { get; init; } = "Window list";
    public string NewDocument { get; init; } = "New document";
}

/// <summary>Original 16x16 vector artwork, authored for this project.</summary>
public static class DockGlyphs
{
    public const string Close = "M4,4 L12,12 M12,4 L4,12";
    public const string Pin = "M5,2 L11,2 M6,2 L6,7 L4,9 L12,9 L10,7 L10,2 M8,9 L8,14";
    public const string Unpin = "M3,5 L7,1 M4,4 L8,8 L11,8 L6,13 L6,10 L2,6 M8,10 L12,14";
    public const string Float = "M2,5 L2,14 L11,14 L11,11 M5,2 L14,2 L14,10 L5,10 Z";
    public const string Maximize = "M3,3 L13,3 L13,13 L3,13 Z M3,5 L13,5";
    public const string Restore = "M5,2 L14,2 L14,11 M2,5 L11,5 L11,14 L2,14 Z M2,7 L11,7";
    public const string Minimize = "M3,12 L13,12";
    public const string Chevron = "M4,6 L8,10 L12,6";
    public const string Document = "M4,1 L10,1 L13,4 L13,15 L3,15 L3,1 Z M10,1 L10,5 L13,5 M5,8 L11,8 M5,11 L10,11";
    public const string Lock = "M4,7 L12,7 L12,14 L4,14 Z M5,7 L5,4 Q8,-1 11,4 L11,7";
    public const string Add = "M8,2 L8,14 M2,8 L14,8";
    public const string Folder = "M2,4 L6,4 L8,6 L14,6 L14,13 L2,13 Z";
    public const string Save = "M2,2 L12,2 L14,4 L14,14 L2,14 Z M5,2 L5,6 L11,6 L11,2 M5,10 L11,10 L11,14 L5,14 Z";
}
