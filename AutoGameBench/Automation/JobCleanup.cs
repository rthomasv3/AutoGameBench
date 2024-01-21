using System.Collections.Generic;

namespace AutoGameBench.Automation;

public sealed class JobCleanup
{
    public List<JobAction> Actions { get; init; } = new List<JobAction>();
}
