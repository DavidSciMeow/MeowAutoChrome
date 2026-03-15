using System.Diagnostics;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 表示资源使用快照（CPU 百分比与内存 MB）。
/// </summary>
public sealed record ResourceMetricsSnapshot(double CpuUsagePercent, double MemoryUsageMb);

/// <summary>
/// 资源度量服务：用于获取当前进程的 CPU 与内存使用快照。
/// </summary>
public sealed class ResourceMetricsService
{
    /// <summary>
    /// 锁::用于保护 CPU 使用率计算的基准时间点和处理器时间，确保在多线程环境下获取快照时数据的一致性。
    /// </summary>
    private readonly object _syncRoot = new();
    /// <summary>
    /// CPU 使用率时的总处理器时间和时间戳，用于计算当前 CPU 使用率的增量。通过锁保护以确保线程安全。
    /// </summary>
    private TimeSpan _lastTotalProcessorTime;
    /// <summary>
    /// CPU 使用率时的时间戳（Stopwatch.GetTimestamp），用于计算时间增量。通过锁保护以确保线程安全。
    /// </summary>
    private long _lastTimestamp;

    /// <summary>
    /// 资源度量服务构造函数：初始化 CPU 使用率计算的基准时间点和处理器时间。
    /// </summary>
    public ResourceMetricsService()
    {
        using var process = Process.GetCurrentProcess();
        _lastTotalProcessorTime = process.TotalProcessorTime;
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// 获取当前进程的资源使用快照（线程安全）。
    /// </summary>
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

            return new ResourceMetricsSnapshot( Math.Round(Math.Clamp(cpuUsagePercent, 0, 100), 1), Math.Round(memoryUsageMb, 1));
        }
    }
}
