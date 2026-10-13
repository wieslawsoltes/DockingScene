using System.Runtime.CompilerServices;

// Test-only interval boundary. Keep assertions, timing, formatting and delegate
// construction outside the no-inline method so the JIT cannot move their boxes
// into the measured interval. See dotnet/runtime#96836. This does not suppress
// GC, change JIT settings, subtract a baseline or forgive positive allocations.
internal static class AllocationProbe
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long Measure<T>(Func<T> operation, int iterations, out T result)
    {
        result = default!;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) result = operation();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    internal static void VerifyControls()
    {
        Func<int> empty = static () => 42;
        Func<byte[]> positive = static () => new byte[40];
        _ = Measure(empty, 1000, out _);
        _ = Measure(positive, 1, out _);
        long emptyBytes = Measure(empty, 10000, out int value);
        long positiveBytes = Measure(positive, 1, out byte[] allocation);
        if (emptyBytes != 0 || value != 42 || positiveBytes < 40 || allocation.Length != 40)
            throw new Exception($"Allocation probe controls failed: empty={emptyBytes}, positive={positiveBytes}, result={value}");
        GC.KeepAlive(allocation);
        Console.WriteLine($"ALLOCATION controls: empty={emptyBytes}, positive={positiveBytes}; no baseline subtraction");
    }
}
