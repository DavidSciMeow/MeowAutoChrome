using System.Diagnostics;

namespace MeowAutoChrome.Web.Services;

public sealed record ResourceMetricsSnapshot(double CpuUsagePercent, double MemoryUsageMb);

public sealed class ResourceMetricsService
{
    private readonly object _syncRoot = new();
    private TimeSpan _lastTotalProcessorTime;
    private long _lastTimestamp;

    public ResourceMetricsService()
    {
        using var process = Process.GetCurrentProcess();
        _lastTotalProcessorTime = process.TotalProcessorTime;
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    public ResourceMetricsSnapshot GetSnapshot()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var memoryUsageMb = process.WorkingSet64 / 1024d / 1024d;

        lock (_syncRoot)
        {
            var nowTimestamp = Stopwatch.GetTimestamp();
            var nowCpuTime = process.TotalProcessorTime;
            var elapsedSeconds = (nowTimestamp - _lastTimestamp) / (double)Stopwatch.Frequency;
            var cpuDeltaMs = (nowCpuTime - _lastTotalProcessorTime).TotalMilliseconds;

            var cpuUsagePercent = elapsedSeconds <= 0
                ? 0
                : cpuDeltaMs / (elapsedSeconds * 1000d * Environment.ProcessorCount) * 100d;

            _lastTimestamp = nowTimestamp;
            _lastTotalProcessorTime = nowCpuTime;

            return new ResourceMetricsSnapshot(
                Math.Round(Math.Clamp(cpuUsagePercent, 0, 100), 1),
                Math.Round(memoryUsageMb, 1));
        }
    }
}
