using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AutoGameBench.IPC;
using InputSimulatorEx;
using InputSimulatorEx.Native;
using Vortice;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoGameBench.Automation;

public sealed class JobRunner : IDisposable
{
    #region Native

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hwnd, ref RawRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    #endregion

    #region Fields

    private readonly IpcServer _ipcServer;
    private readonly InputSimulator _inputSimulator;
    private readonly Dictionary<string, VirtualKeyCode> _keyMap;
    private readonly List<Job> _jobs;

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
        _keyMap = new Dictionary<string, VirtualKeyCode>(StringComparer.OrdinalIgnoreCase);
        _jobs = new List<Job>();

        BuildKeyMap();
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

        _jobInitialized = false;
        _frameTimes = new List<double>();

        DateTime startTime = DateTime.Now;

        try
        {
            InitializeJob(job, windowHandle);

            DateTime initializationCompleteTime = DateTime.Now;

            Console.WriteLine("Starting actions...");
            foreach (JobAction action in job.Actions)
            {
                RunAction(action, job.ActionDelay);
            }
            Console.WriteLine("Actions Complete.");

            _jobComplete = true;

            CleanupJob(job);

            List<double> orderedFrameTimes = _frameTimes.OrderByDescending(x => x).ToList();

            result = new JobResult()
            {
                JobName = job.Name,
                GameId = job.GameId,
                StartTime = startTime,
                InitializationCompleteTime = initializationCompleteTime,
                EndTime = DateTime.Now,
                AverageFps = 1000.0 / Math.Max(1.0, _frameTimes.Average()),
                OnePercentLow = 1000.0 / Math.Max(1.0, orderedFrameTimes.Take((int)(_frameTimes.Count * 0.1)).Average()),
                PointOnePercentLow = 1000.0 / Math.Max(1.0, orderedFrameTimes.Take((int)(_frameTimes.Count * 0.01)).Average()),
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
                Error = e.ToString()
            };
        }

        return result;
    }

    public void Dispose()
    {
        if (_ipcServer != null)
        {
            _ipcServer.FrameTimeReceived -= IpcServer_FrameTimeReceived;
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

    private void BuildKeyMap()
    {
        foreach (VirtualKeyCode keyCode in Enum.GetValues<VirtualKeyCode>())
        {
            _keyMap.TryAdd(keyCode.ToString().Replace("VK_", String.Empty), keyCode);
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

            CenterMouse(windowHandle);

            foreach (JobAction action in job.Initialization.Actions)
            {
                RunAction(action, job.ActionDelay);
            }

            _jobInitialized = true;
            Console.WriteLine("Initialization Complete.");
        }
    }

    private void CenterMouse(nint windowHandle)
    {
        RawRect windowRect = new RawRect();
        GetWindowRect(windowHandle, ref windowRect);

        double windowWidth = windowRect.Right - windowRect.Left;
        double windowHeight = windowRect.Bottom - windowRect.Top;

        MoveWindow(windowHandle, 0, 0, (int)windowWidth, (int)windowHeight, true);

        windowRect = new RawRect();
        GetWindowRect(windowHandle, ref windowRect);

        double centerX = windowRect.Left + (windowWidth / 2);
        double centerY = windowRect.Top + (windowHeight / 2);
        _inputSimulator.Mouse.MoveMouseTo(0, 0);
        _inputSimulator.Mouse.MoveMouseBy((int)centerX / 2, (int)centerY / 2);
    }

    private void CleanupJob(Job job)
    {
        if (job.Cleanup != null)
        {
            Console.WriteLine("Starting cleanup...");

            foreach (JobAction action in job.Cleanup.Actions)
            {
                RunAction(action, job.ActionDelay);
            }

            Console.WriteLine("Cleanup Complete.");
        }
    }

    private void RunAction(JobAction action, int postDelay)
    {
        if (action.Name == "KeyPress")
        {
            if (action.With.ContainsKey("key"))
            {
                VirtualKeyCode? keyCode = GetKey(action.With["key"]);

                if (keyCode.HasValue)
                {
                    _inputSimulator.Keyboard.KeyDown(keyCode.Value).Sleep(100).KeyUp(keyCode.Value);
                    Console.WriteLine($"KeyPress: {keyCode.Value}");
                }
            }
        }
        else if (action.Name == "KeyDown")
        {
            if (action.With.ContainsKey("key"))
            {
                VirtualKeyCode? keyCode = GetKey(action.With["key"]);

                if (keyCode.HasValue)
                {
                    _inputSimulator.Keyboard.KeyDown(keyCode.Value);
                    Console.WriteLine($"KeyDown: {keyCode.Value}");
                }
            }
        }
        else if (action.Name == "KeyUp")
        {
            if (action.With.ContainsKey("key"))
            {
                VirtualKeyCode? keyCode = GetKey(action.With["key"]);

                if (keyCode.HasValue)
                {
                    _inputSimulator.Keyboard.KeyUp(keyCode.Value);
                    Console.WriteLine($"KeyUp: {keyCode.Value}");
                }
            }
        }
        else if (action.Name == "MouseClick")
        {
            if (action.With.ContainsKey("button"))
            {
                if (String.Equals(action.With["button"], "right", StringComparison.OrdinalIgnoreCase))
                {
                    _inputSimulator.Mouse.RightButtonDown().Sleep(50).RightButtonUp();
                    Console.WriteLine($"MouseClick: right");
                }
                else if (String.Equals(action.With["button"], "left", StringComparison.OrdinalIgnoreCase))
                {
                    _inputSimulator.Mouse.LeftButtonDown().Sleep(50).LeftButtonUp();
                    Console.WriteLine($"MouseClick: left");
                }
            }
        }
        else if (action.Name == "MouseDown")
        {
            if (action.With.ContainsKey("button"))
            {
                if (String.Equals(action.With["button"], "right", StringComparison.OrdinalIgnoreCase))
                {
                    _inputSimulator.Mouse.RightButtonDown();
                    Console.WriteLine($"MouseDown: right");
                }
                else if (String.Equals(action.With["button"], "left", StringComparison.OrdinalIgnoreCase))
                {
                    _inputSimulator.Mouse.LeftButtonDown();
                    Console.WriteLine($"MouseDown: left");
                }
            }
        }
        else if (action.Name == "MouseUp")
        {
            if (action.With.ContainsKey("button"))
            {
                if (String.Equals(action.With["button"], "right", StringComparison.OrdinalIgnoreCase))
                {
                    _inputSimulator.Mouse.RightButtonUp();
                    Console.WriteLine($"MouseUp: right");
                }
                else if (String.Equals(action.With["button"], "left", StringComparison.OrdinalIgnoreCase))
                {
                    _inputSimulator.Mouse.LeftButtonUp();
                    Console.WriteLine($"MouseUp: left");
                }
            }
        }
        else if (action.Name == "Delay")
        {
            if (action.With.ContainsKey("time") &&
                Int32.TryParse(action.With["time"], out int delay))
            {
                Console.WriteLine($"Delaying: {delay}");
                Thread.Sleep(delay);
                Console.WriteLine("Delay Complete.");
            }
        }

        if (!action.SkipDelay)
        {
            Thread.Sleep(postDelay);
        }
    }

    private VirtualKeyCode? GetKey(string key)
    {
        VirtualKeyCode? keyCode = null;

        if (_keyMap.ContainsKey(key))
        {
            keyCode = _keyMap[key];
        }

        return keyCode;
    }

    #endregion
}
