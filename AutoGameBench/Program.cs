using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using AutoGameBench.IPC;
using AutoGameBench.Steam;
using Reloaded.Injector;

namespace AutoGameBench;

internal class Program
{
    private static GameLibrary _gameLibrary = new GameLibrary();
    private static IpcServer _ipcServer = new IpcServer();

    static void Main(string[] args)
    {
        Console.Write("Games:\n");

        int index = 0;
        int selectedGame = -1;
        foreach (App app in _gameLibrary.Apps)
        {
            Console.WriteLine($"{++index}:\t{app.AppState.Name}");
        }

        while (selectedGame < 0)
        {
            Console.Write("Select a game (enter number): ");
            string game = Console.ReadLine();
            if (Int32.TryParse(game, out int gameIndex))
            {
                if (gameIndex > 0 && gameIndex <= _gameLibrary.Apps.Count)
                {
                    selectedGame = gameIndex - 1;
                }
            }
        }

        Process gameProcess = StartGame(_gameLibrary.Apps[selectedGame]);

        if (gameProcess != null)
        {
            AttachToProcess(gameProcess);
        }

        Thread.Sleep(1000);

        _ipcServer.Dispose();
    }

    static Process StartGame(App game)
    {
        Process gameProcess = null;

        List<string> possibleGameExecutables = Directory
            .GetFiles(game.AppState.FullInstallDir, "*.exe", SearchOption.AllDirectories)
            .Select(x => Path.GetFileNameWithoutExtension(x))
            .ToList();

        // Alternate way to launch: $"steam://rungameid/{game.AppState.AppId}"
        Process steamProcess = Process.Start(_gameLibrary.SteamExePath, $"steam://launch/{game.AppState.AppId}");
        steamProcess.WaitForExit();

        int retryCount = 0;
        while (gameProcess == null && retryCount < 10)
        {
            Thread.Sleep(1000);

            gameProcess = Process.GetProcesses().Where(x => possibleGameExecutables.Contains(x.ProcessName)).FirstOrDefault();

            if (gameProcess == null)
            {
                retryCount++;
            }
        }

        return gameProcess;
    }

    static void AttachToProcessByName(string processName)
    {
        Process gameProcess = null;
        Process[] processes = Process.GetProcessesByName(processName);

        foreach (Process process in processes)
        {
            if (process.MainWindowHandle == IntPtr.Zero)
            {
                continue;
            }

            gameProcess = process;
            break;
        }

        if (gameProcess != null)
        {
            AttachToProcess(gameProcess);
        }
    }

    static void AttachToProcess(Process process)
    {
        string libraryPath = Path.Combine(Environment.CurrentDirectory, "AssemblyLoaderNE.dll");
        using Injector injector = new(process);
        long address = injector.Inject(libraryPath);

        if (address > 0)
        {
            Console.WriteLine("Injected.");

            bool started = injector.CallFunction<int>(libraryPath, "StartHook", (int)process.MainWindowHandle) > 0;

            if (started)
            {
                Console.WriteLine("Started Hook.");

                Thread.Sleep(5000);

                bool stopped = injector.CallFunction<int>(libraryPath, "StopHook") > 0;

                if (stopped)
                {
                    Console.WriteLine("Stopped Hook.");
                }
            }

            Thread.Sleep(1000);

            try
            {
                injector.Eject(libraryPath);
            }
            catch { }

            Thread.Sleep(1000);
        }
    }
}
