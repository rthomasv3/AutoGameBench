using System;

namespace AutoGameBench.IPC;

public sealed class FrameTimeEventArgs : EventArgs
{
    public double FrameTime { get; init; }
}
