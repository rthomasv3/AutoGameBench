using System.Collections.Generic;

namespace AutoGameBench.Automation;

public sealed class JobInitialization
{
    public int StartDelay { get; init; }
    public List<JobAction> Actions { get; init; } = new List<JobAction>();
}
