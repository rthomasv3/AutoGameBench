using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using AutoGameBench.IPC;
using AutoGameBench.Sensors;
using InputSimulatorEx;
using Vortice;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using NativeMethods = AutoGameBench.Native.Native;

namespace AutoGameBench.Automation;

public sealed class JobRunner : IDisposable
{
    #region Fields

    private readonly IpcServer _ipcServer;
    private readonly InputSimulator _inputSimulator;
    private readonly List<Job> _jobs;
    private readonly SensorMonitor _sensorMonitor;

    private ActionRunner _actionRunner;
    private bool _jobInitialized;
    private bool _jobComplete;
    private List<double> _frameTimes;

    #endregion

    #region Constructor

    public JobRunner(IpcServer ipcServer)
    {
        _ipcServer = ipcServer;
        _ipcServer.FrameTimeReceived += IpcServer_FrameTimeReceived;
        _inputSimulator = new InputSimulator();
        _jobs = new List<Job>();
        _sensorMonitor = new SensorMonitor();

        BuildJobCollection();
    }

    #endregion

    #region Public Methods

    public IReadOnlyList<Job> GetJobsForGame(string gameId)
    {
        return _jobs
            .Where(x => String.IsNullOrEmpty(x.GameId) ||
                        String.Equals(x.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    public JobResult RunJob(Job job, nint windowHandle)
    {
        JobResult result = null;

        _actionRunner = new ActionRunner(_inputSimulator);
        _jobInitialized = false;
        _frameTimes = new List<double>();

        DateTime startTime = DateTime.Now;

        try
        {
            InitializeJob(job, windowHandle);

            _sensorMonitor.StartMonitoring();

            _jobInitialized = true;

            DateTime initializationCompleteTime = DateTime.Now;

            Console.WriteLine("Starting actions...");
            foreach (JobAction action in job.Actions)
            {
                _actionRunner.RunAction(windowHandle, job.Name, action, job.ActionDelay);
            }
            Console.WriteLine("Actions Complete.");

            DateTime actionsCompleteTime = DateTime.Now;
            _sensorMonitor.StopMonitoring();

            _jobComplete = true;

            CleanupJob(job, windowHandle);

            DateTime endTime = DateTime.Now;

            double averageFps = 0;
            double onePercentLowFps = 0;
            double pointOnePercentLowFps = 0;
            if (_frameTimes.Count > 0)
            {
                List<double> orderedFrameTimes = _frameTimes.OrderByDescending(x => x).ToList();
                averageFps = 1000.0 / Math.Max(1.0, _frameTimes.Average());
                onePercentLowFps = 1000.0 / Math.Max(1.0, orderedFrameTimes.Take((int)(_frameTimes.Count * 0.1)).Average());
                pointOnePercentLowFps = 1000.0 / Math.Max(1.0, orderedFrameTimes.Take((int)(_frameTimes.Count * 0.01)).Average());
            }

            result = new JobResult()
            {
                JobName = job.Name,
                GameId = job.GameId,
                StartTime = startTime,
                InitializationCompleteTime = initializationCompleteTime,
                ActionsCompleteTime = actionsCompleteTime,
                EndTime = endTime,
                AverageFps = averageFps,
                OnePercentLowFps = onePercentLowFps,
                PointOnePercentLowFps = pointOnePercentLowFps,
                AverageCpuTemperature = _sensorMonitor.AverageCpuTemperature,
                AverageCpuLoad = _sensorMonitor.AverageCpuLoad,
                AverageMemoryUsage = _sensorMonitor.AverageMemoryUsage,
                AverageGpuTemperature = _sensorMonitor.AverageGpuTemperature,
                AverageGpuHotSpotTemperature = _sensorMonitor.AverageGpuHotSpotTemperature,
                AverageGpuMemoryUsage = _sensorMonitor.AverageGpuMemoryUsage,
                Screenshots = _actionRunner.Screenshots,
                Success = true
            };
        }
        catch (Exception e)
        {
            result = new JobResult()
            {
                JobName = job.Name,
                GameId = job.GameId,
                StartTime = startTime,
                EndTime = DateTime.Now,
                Screenshots = _actionRunner.Screenshots,
                Error = e.ToString()
            };
        }
        finally
        {
            _sensorMonitor.StopMonitoring();
            _actionRunner.Dispose();
        }

        SaveJobResult(job, result);

        return result;
    }

    public void Dispose()
    {
        if (_ipcServer != null)
        {
            _ipcServer.FrameTimeReceived -= IpcServer_FrameTimeReceived;
        }

        if (_sensorMonitor != null)
        {
            _sensorMonitor.Dispose();
        }
    }

    #endregion

    #region Private Methods

    private void IpcServer_FrameTimeReceived(object sender, FrameTimeEventArgs e)
    {
        if (_jobInitialized && !_jobComplete)
        {
            _frameTimes.Add(e.FrameTime);
        }
    }

    private void BuildJobCollection()
    {
        string jobDirectory = "Automation\\Jobs";

        if (Directory.Exists(jobDirectory))
        {
            IDeserializer deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            foreach (string file in Directory.EnumerateFiles(jobDirectory, "*.yaml"))
            {
                try
                {
                    string jobContents = File.ReadAllText(file);
                    Job job = deserializer.Deserialize<Job>(jobContents);
                    _jobs.Add(job);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
    }

    private void InitializeJob(Job job, nint windowHandle)
    {
        if (job.Initialization != null)
        {
            Console.WriteLine("Initializing job...");

            if (job.Initialization.StartDelay > 0)
            {
                Thread.Sleep(job.Initialization.StartDelay);
            }

            BringWindowToForeground(windowHandle);
            CenterMouse(windowHandle);

            foreach (JobAction action in job.Initialization.Actions)
            {
                _actionRunner.RunAction(windowHandle, job.Name, action, job.ActionDelay);
            }

            Console.WriteLine("Initialization Complete.");
        }
    }

    private void BringWindowToForeground(nint windowHandle)
    {
        if (!NativeMethods.IsWindowForeground(windowHandle))
        {
            Thread.Sleep(250);

            if (NativeMethods.IsIconic(windowHandle))
            {
                // Minimized so send restore
                NativeMethods.ShowWindow(windowHandle, NativeMethods.WindowShowStyle.Restore);
            }
            else
            {
                // Already Maximized or Restored so just bring to front
                NativeMethods.SetForegroundWindow(windowHandle);
            }
        }
    }

    private void CenterMouse(nint windowHandle)
    {
        Rectangle monitorRect = NativeMethods.GetMonitorSize(windowHandle);
        Rectangle windowRect = NativeMethods.GetWindowRect(windowHandle);
        float windowCenterX = windowRect.Left + (windowRect.Width / 2f);
        float windowCenterY = windowRect.Top + (windowRect.Height / 2f);
        float normalizedX = windowCenterX * (65536f / monitorRect.Width);
        float normalizedY = windowCenterY * (65536f / monitorRect.Height);
        _inputSimulator.Mouse.MoveMouseTo(normalizedX, normalizedY);
    }

    private void CleanupJob(Job job, nint windowHandle)
    {
        if (job.Cleanup != null)
        {
            Console.WriteLine("Starting cleanup...");

            foreach (JobAction action in job.Cleanup.Actions)
            {
                _actionRunner.RunAction(windowHandle, job.Name, action, job.ActionDelay);
            }

            Console.WriteLine("Cleanup Complete.");
        }
    }

    private void SaveJobResult(Job job, JobResult result)
    {
        string resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions()
        {
            WriteIndented = true
        });

        string fileName = $"Automation\\{job.GameId}_{DateTime.Now.Ticks}.json";
        File.WriteAllText(fileName, resultJson);

        Console.WriteLine($"Saved: {fileName}");
        Console.WriteLine($"Result:\n{resultJson}");
    }

    #endregion
}
