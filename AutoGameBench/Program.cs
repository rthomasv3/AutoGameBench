using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using AutoGameBench.Automation;
using AutoGameBench.IPC;
using AutoGameBench.Steam;
using Reloaded.Injector;

namespace AutoGameBench;

internal class Program
{
    private static readonly string _loaderLibraryPath = Path.Combine(Environment.CurrentDirectory, "AssemblyLoaderNE.dll");

    private static GameLibrary _gameLibrary = new GameLibrary();
    private static IpcServer _ipcServer = new IpcServer();

    static void Main(string[] args)
    {
        Console.Write("Games:\n0\tCustom EXE\n");

        int index = 0;
        int gameSelection = -1;
        string gameExePath = String.Empty;
        foreach (App app in _gameLibrary.Apps)
        {
            Console.WriteLine($"{++index}:\t{app.AppState.Name}");
        }

        while (gameSelection < 0)
        {
            Console.Write("Select a game (enter number): ");
            string game = Console.ReadLine();
            if (Int32.TryParse(game, out int gameIndex))
            {
                if (gameIndex > 0 && gameIndex <= _gameLibrary.Apps.Count)
                {
                    gameSelection = gameIndex - 1;
                }
                else if (gameIndex == 0)
                {
                    Console.Write("Enter EXE path: ");
                    gameExePath = Console.ReadLine();

                    if (!String.IsNullOrWhiteSpace(gameExePath))
                    {
                        gameExePath = gameExePath.Replace("\"", String.Empty);
                    }

                    if (File.Exists(gameExePath))
                    {
                        break;
                    }
                }
            }
        }

        App selectedApp = gameSelection > -1 ? _gameLibrary.Apps[gameSelection] : null;

        Console.Write("\nAvailable Jobs:\n");

        JobRunner jobRunner = new(_ipcServer);

        index = 0;
        int jobSelection = -1;
        IReadOnlyList<Job> gameJobs = jobRunner.GetJobsForGame(selectedApp?.AppState?.AppId);
        foreach (Job job in gameJobs)
        {
            Console.WriteLine($"{++index}:\t{job.Name}");
        }

        while (jobSelection < 0)
        {
            Console.Write("Select a job (enter number): ");
            string job = Console.ReadLine();
            if (Int32.TryParse(job, out int jobIndex))
            {
                if (jobIndex > 0 && jobIndex <= gameJobs.Count)
                {
                    jobSelection = jobIndex - 1;
                }
            }
        }

        Process gameProcess = null;

        if (selectedApp != null)
        {
            gameProcess = StartGame(selectedApp);
        }
        else
        {
            gameProcess = StartGame(gameExePath);
        }

        if (gameProcess != null)
        {
            Job selectedJob = gameJobs[jobSelection];

            if (TryAttachToProcess(gameProcess, out Injector injector))
            {
                JobResult result = jobRunner.RunJob(selectedJob, gameProcess.MainWindowHandle);
                
                Console.WriteLine("Result:");

                string resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions()
                {
                    WriteIndented = true
                });

                File.WriteAllText($"Automation\\{selectedJob.GameId}_{DateTime.Now.Ticks}.json", resultJson);

                Console.WriteLine(resultJson);

                if (TryDetachFromProcess(injector))
                {
                    gameProcess.Kill();
                }
            }
        }

        jobRunner.Dispose();
        _ipcServer.Dispose();

        Console.WriteLine("Complete. Press any key to exit.");
        Console.Read();
    }

    static Process StartGame(string gameExePath)
    {
        return Process.Start(gameExePath);
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
            Thread.Sleep(1250);

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

    static bool TryAttachToProcess(Process process, out Injector injector)
    {
        bool success = false;
        injector = null;

        try
        {
            injector = new(process);

            string libraryPath = Path.Combine(Environment.CurrentDirectory, "AssemblyLoaderNE.dll");
            long address = injector.Inject(libraryPath);

            if (address > 0)
            {
                Console.WriteLine("Injected.");

                bool started = injector.CallFunction<int>(libraryPath, "StartHook", (int)process.MainWindowHandle) > 0;

                if (started)
                {
                    Console.WriteLine("Hook Complete.");
                    success = true;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        return success;
    }

    static bool TryDetachFromProcess(Injector injector)
    {
        bool success = false;

        try
        {
            bool stopped = injector.CallFunction<int>(_loaderLibraryPath, "StopHook") > 0;

            if (stopped)
            {
                Console.WriteLine("Hook Stopped.");
                success = injector.Eject(_loaderLibraryPath);
                injector.Dispose();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        return success;
    }

    static void AttachToProcess(Process process)
    {
        using Injector injector = new(process);
        long address = injector.Inject(_loaderLibraryPath);

        if (address > 0)
        {
            Console.WriteLine("Injected.");

            bool started = injector.CallFunction<int>(_loaderLibraryPath, "StartHook", (int)process.MainWindowHandle) > 0;

            if (started)
            {
                Console.WriteLine("Started Hook.");

                Thread.Sleep(5000);

                bool stopped = injector.CallFunction<int>(_loaderLibraryPath, "StopHook") > 0;

                if (stopped)
                {
                    Console.WriteLine("Stopped Hook.");
                }
            }

            Thread.Sleep(1000);

            try
            {
                injector.Eject(_loaderLibraryPath);
            }
            catch { }

            Thread.Sleep(1000);
        }
    }
}
