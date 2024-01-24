using System;
using System.Collections.Generic;

namespace AutoGameBench.Automation;

public sealed class JobResult
{
    public string JobName { get; init; }
    public string GameId { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime InitializationCompleteTime { get; init; }
    public DateTime ActionsCompleteTime { get; init; }
    public DateTime EndTime { get; init; }
    public double AverageFps { get; init; }
    public double OnePercentLowFps { get; init; }
    public double PointOnePercentLowFps { get; init; }
    public double AverageCpuTemperature { get; init; }
    public double AverageCpuLoad { get; init; }
    public double AverageMemoryUsage { get; init; }
    public double AverageGpuTemperature { get; init; }
    public double AverageGpuHotSpotTemperature { get; init; }
    public double AverageGpuMemoryUsage { get; init; }
    public IEnumerable<string> Screenshots { get; init; }
    public bool Success { get; init; }
    public string Error { get; init; }
}
