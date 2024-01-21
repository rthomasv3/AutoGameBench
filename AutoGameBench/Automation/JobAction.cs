using System.Collections.Generic;

namespace AutoGameBench.Automation;

public sealed class JobAction
{
    public string Name { get; init; }
    public Dictionary<string, string> With { get; init; }
    public bool SkipDelay { get; init; }
}
