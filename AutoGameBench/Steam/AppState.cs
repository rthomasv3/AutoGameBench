using System.IO;

namespace AutoGameBench.Steam;

internal sealed class AppState
{
    public string AppId { get; init; }
    public string LauncherPath { get; init; }
    public string Name { get; init; }
    public string InstallDir { get; init; }
    public string LibraryAppDir { get; set; }
    public string FullInstallDir { get { return Path.Combine(LibraryAppDir, InstallDir); } }
}
