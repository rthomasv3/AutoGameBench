using System.Collections.Generic;

namespace AutoGameBench.Automation;

public sealed class Job
{
    public string Name { get; init; }
    public string GameId { get; init; }
    public int ActionDelay { get; init; } = 3000;
    public JobInitialization Initialization { get; init; }
    public List<JobAction> Actions { get; init; } = new List<JobAction>();
    public JobCleanup Cleanup { get; init; }
}
