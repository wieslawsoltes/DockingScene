using System.Text;
using DockingScene.Core;
using DockingScene.Serialization;

int passed = 0, failed = 0;
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
}
var manager = new DockManager(); manager.Register(new("d", "Zażółć 日本語", WindowKind.Document)).ThrowIfFailed();
byte[] bytes = Encoding.UTF8.GetBytes(DockLayoutSerializer.Serialize(manager.Layout));
await Test("Nonseekable one-byte chunks preserve multibyte JSON and leave source open", async () => {
    using var source = new ChunkStream(bytes, 1);
    var result = await DockLayoutStream.ReadAsync(source);
    Check(result.Success && result.Layout!.Windows["d"].Title == "Zażółć 日本語" && source.CanRead);
});
await Test("Exact input byte limit succeeds", async () => {
    using var source = new ChunkStream(bytes, 7);
    Check((await DockLayoutStream.ReadAsync(source, new() { MaximumBytes = bytes.Length })).Success);
    Check(source.BytesRead == bytes.Length);
});
await Test("Oversized unknown-length input stops at budget plus one byte", async () => {
    using var source = new ChunkStream(new byte[1000000], 7000);
    var result = await DockLayoutStream.ReadAsync(source, new() { MaximumBytes = 101 });
    Check(!result.Success && source.BytesRead == 102);
});
await Test("Stream input starts at the caller's current position", async () => {
    using var source = new MemoryStream(new byte[4].Concat(bytes).ToArray()); source.Position = 4;
    Check((await DockLayoutStream.ReadAsync(source)).Success && source.CanRead);
});
await Test("Cancellation remains cancellation rather than invalid layout", async () => {
    using var source = new ChunkStream(bytes, 1); using var cancel = new CancellationTokenSource(); cancel.Cancel();
    try { await DockLayoutStream.ReadAsync(source, cancellationToken: cancel.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { Check(source.BytesRead == 0); }
});
await Test("Malformed stream input reports a rejected layout", async () => {
    using var source = new MemoryStream(Encoding.UTF8.GetBytes("{not-json"));
    Check(!(await DockLayoutStream.ReadAsync(source)).Success);
});
await Test("Invalid unknown-window policy is rejected before reading", async () => {
    using var source = new ChunkStream(bytes, 1);
    try { await DockLayoutStream.ReadAsync(source, new() { UnknownWindows = (UnknownWindowHandling)99 }); throw new Exception("Invalid enum accepted"); }
    catch (ArgumentOutOfRangeException) { Check(source.BytesRead == 0); }
});
await Test("Storage save honors the same window budget as load", async () => {
    var folder = Path.Combine(Path.GetTempPath(), "DockingScene-" + Guid.NewGuid().ToString("N"));
    try {
        var store = new FileDockLayoutStore(folder, new() { MaximumWindows = 1 });
        await store.SaveAsync("sample", manager.Layout);
        var second = new DockManager(manager.Layout); second.Register(new("extra", "Extra", WindowKind.Document)).ThrowIfFailed();
        try { await store.SaveAsync("sample", second.Layout); throw new Exception("Window budget ignored"); }
        catch (InvalidOperationException) { }
        var loaded = await store.LoadAsync("sample"); Check(loaded.Success && loaded.Layout!.Windows.Count == 1);
    } finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
});
Console.WriteLine($"{passed}/{passed + failed} serialization input review specifications passed.");
return failed == 0 ? 0 : 1;

sealed class ChunkStream(byte[] bytes, int chunk) : Stream
{
    private int _position;
    public int BytesRead => _position;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count)
    {
        count = Math.Min(Math.Min(count, chunk), bytes.Length - _position);
        bytes.AsSpan(_position, count).CopyTo(buffer.AsSpan(offset, count)); _position += count; return count;
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int count = Math.Min(Math.Min(buffer.Length, chunk), bytes.Length - _position);
        bytes.AsMemory(_position, count).CopyTo(buffer); _position += count; return ValueTask.FromResult(count);
    }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
