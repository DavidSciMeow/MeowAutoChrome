using System.Diagnostics;

namespace MeowAutoChrome.Core.Services;

/// <summary>
/// 资源指标快照，包含 CPU 使用率和内存使用（MB）。<br/>
/// Resource metrics snapshot containing CPU usage percentage and memory usage in MB.
/// </summary>
/// <param name="CpuUsagePercent">CPU 使用率（百分比）/ CPU usage percentage.</param>
/// <param name="MemoryUsageMb">内存使用量（MB）/ memory usage in MB.</param>
public sealed record ResourceMetricsSnapshot(double CpuUsagePercent, double MemoryUsageMb);

/// <summary>
/// 收集进程资源使用情况（CPU、内存）的服务。<br/>
/// Service that collects process resource usage metrics such as CPU and memory.
/// </summary>
public sealed class ResourceMetricsService
{
    private readonly object _syncRoot = new();
    private TimeSpan _lastTotalProcessorTime;
    private long _lastTimestamp;

    /// <summary>
    /// 构造函数：初始化进程基线 CPU 时间与时间戳以便后续度量。<br/>
    /// Constructor: initializes baseline CPU time and timestamp for subsequent measurements.
    /// </summary>
    public ResourceMetricsService()
    {
        using var process = Process.GetCurrentProcess();
        _lastTotalProcessorTime = process.TotalProcessorTime;
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// 获取当前进程的资源使用快照（CPU 百分比与内存 MB）。<br/>
    /// Get a snapshot of current process resource usage (CPU percent and memory in MB).
    /// </summary>
    /// <returns>资源指标快照 / resource metrics snapshot.</returns>
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

            var cpuUsagePercent = elapsedSeconds <= 0 ? 0 : cpuDeltaMs / (elapsedSeconds * 1000d * Environment.ProcessorCount) * 100d;

            _lastTimestamp = nowTimestamp;
            _lastTotalProcessorTime = nowCpuTime;

            return new ResourceMetricsSnapshot(Math.Round(Math.Clamp(cpuUsagePercent, 0, 100), 1), Math.Round(memoryUsageMb, 1));
        }
    }
}
