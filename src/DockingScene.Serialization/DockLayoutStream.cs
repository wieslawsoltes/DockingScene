using System.Text;

namespace DockingScene.Serialization;

/// <summary>Bounded asynchronous layout input for seekable or streaming sources.
/// Reads at most MaximumBytes + 1 bytes to distinguish exact-size input from overflow.
/// Leaves the caller's stream open and propagates cancellation and I/O errors.</summary>
public static class DockLayoutStream
{
    public static async Task<LayoutLoadResult> ReadAsync(Stream source, LayoutLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        options = DockLayoutSerializer.ValidateOptions(options ?? new());
        cancellationToken.ThrowIfCancellationRequested();
        if (!source.CanRead) throw new ArgumentException("The layout stream is not readable.", nameof(source));
        using var content = new MemoryStream();
        byte[] buffer = new byte[Math.Min(16384, options.MaximumBytes)];
        while (true)
        {
            int size = (int)Math.Min(buffer.Length, (long)options.MaximumBytes - content.Length + 1);
            int count = await source.ReadAsync(buffer.AsMemory(0, size), cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (content.Length + count > options.MaximumBytes) return new(null, "Stored layout exceeds the configured size limit.");
            content.Write(buffer, 0, count);
        }
        content.Position = 0;
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return DockLayoutSerializer.Deserialize(json, options);
    }
}
