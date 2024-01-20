using System.Collections.Generic;
using System.IO;
using AutoGameBench.Steam;
using Microsoft.Win32;
using VdfParser;

namespace AutoGameBench;

internal class GameLibrary
{
    #region Fields

    private readonly List<App> _apps = new List<App>();
    private readonly string _steamExePath;

    #endregion

    #region Constructor

    public GameLibrary()
    {
        _steamExePath = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam").GetValue("SteamExe").ToString();

        VdfDeserializer deserializer = new();
        string libraryPath = Path.Combine(Path.GetDirectoryName(_steamExePath), "steamapps\\libraryfolders.vdf");
        FileStream libraryStream = File.OpenRead(libraryPath);
        Library library = deserializer.Deserialize<Library>(libraryStream);

        foreach (LibraryFolder libraryFolder in library.LibraryFolders.Values)
        {
            string appsPath = Path.Combine(libraryFolder.Path, "steamapps");

            foreach (string file in Directory.EnumerateFiles(appsPath, "*.acf"))
            {
                try
                {
                    FileStream fileStream = File.OpenRead(file);
                    App app = deserializer.Deserialize<App>(fileStream);
                    app.AppState.LibraryAppDir = Path.Combine(appsPath, "common");
                    _apps.Add(app);
                }
                catch { }
            }
        }
    }

    #endregion

    #region Properties

    public IReadOnlyList<App> Apps
    {
        get { return _apps; }
    }

    public string SteamExePath
    {
        get { return _steamExePath; }
    }

    #endregion
}
