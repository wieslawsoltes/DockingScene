using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DockingScene.Core;

namespace DockingScene.Serialization;

public enum UnknownWindowHandling { Preserve, Ignore, Reject }
public sealed record LayoutLoadOptions
{
    public int MaximumBytes { get; init; } = 4 * 1024 * 1024;
    public int MaximumWindows { get; init; } = 10000;
    public UnknownWindowHandling UnknownWindows { get; init; } = UnknownWindowHandling.Preserve;
    public IReadOnlySet<string>? KnownWindowIds { get; init; }
}
public sealed record LayoutLoadResult(DockLayout? Layout, string? Error)
{
    public bool Success => Layout is not null && Error is null;
}

public static class DockLayoutSerializer
{
    private static readonly DockJsonContext Context = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        IgnoreReadOnlyProperties = true,
        MaxDepth = 128
    });

    public static string Serialize(DockLayout layout)
    {
        DockLayoutValidator.Validate(layout);
        return JsonSerializer.Serialize(layout, Context.DockLayout);
    }

    public static LayoutLoadResult Deserialize(string json, LayoutLoadOptions? options = null)
    {
        options = ValidateOptions(options ?? new());
        if (json is null || json.Length > options.MaximumBytes || Encoding.UTF8.GetByteCount(json) > options.MaximumBytes)
            return new(null, "Layout exceeds the configured size limit.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 128 });
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("schemaVersion", out var version) || version.GetInt32() != 1)
                return new(null, "Unsupported or missing layout schema version.");
            var layout = JsonSerializer.Deserialize(json, Context.DockLayout) ?? throw new InvalidOperationException("Empty layout.");
            DockLayoutValidator.Validate(layout, options.MaximumWindows);
            if (options.KnownWindowIds is { } known)
            {
                var unknown = layout.Windows.Keys.Where(id => !known.Contains(id)).ToHashSet(StringComparer.Ordinal);
                if (unknown.Count > 0 && options.UnknownWindows == UnknownWindowHandling.Reject) return new(null, "Layout references unknown windows: " + string.Join(", ", unknown));
                if (options.UnknownWindows == UnknownWindowHandling.Ignore)
                {
                    layout = DockTree.RemoveReferences(layout, unknown, false);
                    layout = DockTree.RepairActivation(layout with { Windows = layout.Windows.RemoveRange(unknown) });
                    DockLayoutValidator.Validate(layout, options.MaximumWindows);
                }
            }
            return new(layout, null);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or NotSupportedException or NullReferenceException or KeyNotFoundException or FormatException or OverflowException)
        { return new(null, "Invalid docking layout: " + e.Message); }
    }

    internal static LayoutLoadOptions ValidateOptions(LayoutLoadOptions options)
    {
        if (options.MaximumBytes < 1 || options.MaximumWindows < 1 || !Enum.IsDefined(options.UnknownWindows))
            throw new ArgumentOutOfRangeException(nameof(options));
        return options;
    }

    public static DockResult Restore(DockManager manager, string json, LayoutLoadOptions? options = null)
    {
        var revision = manager.Layout.Revision;
        var loaded = Deserialize(json, options);
        return loaded.Success ? manager.Restore(loaded.Layout!, revision) : new(false, revision, loaded.Error);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(DockLayout))]
internal partial class DockJsonContext : JsonSerializerContext { }

public interface IDockLayoutStore
{
    Task SaveAsync(string name, DockLayout layout, CancellationToken cancellationToken = default);
    Task<LayoutLoadResult> LoadAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>Desktop storage using a same-directory temporary file followed by an atomic rename.</summary>
public sealed class FileDockLayoutStore(string directory, LayoutLoadOptions? options = null) : IDockLayoutStore
{
    private readonly string _directory = Path.GetFullPath(directory);
    private readonly LayoutLoadOptions _options = DockLayoutSerializer.ValidateOptions(options ?? new());
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string GetPath(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 80 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ArgumentException("Profile names must contain only ASCII letters, digits, hyphens and underscores.", nameof(name));
        return Path.Combine(_directory, name + ".json");
    }
    public async Task SaveAsync(string name, DockLayout layout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(name);
        DockLayoutValidator.Validate(layout, _options.MaximumWindows);
        var bytes = Encoding.UTF8.GetBytes(DockLayoutSerializer.Serialize(layout));
        if (bytes.Length > _options.MaximumBytes) throw new InvalidOperationException("Layout exceeds the storage limit.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(_directory);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16384, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { _gate.Release(); }
        }
    }
    public async Task<LayoutLoadResult> LoadAsync(string name, CancellationToken cancellationToken = default)
    {
        var path = GetPath(name);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return new(null, "Layout profile does not exist.");
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, FileOptions.Asynchronous);
            if (stream.Length > _options.MaximumBytes) return new(null, "Stored layout exceeds the configured size limit.");
            return await DockLayoutStream.ReadAsync(stream, _options, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
