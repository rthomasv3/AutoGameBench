﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using InputSimulatorEx;
using InputSimulatorEx.Native;
using Tesseract;
using NativeMethods = AutoGameBench.Native.Native;

namespace AutoGameBench.Automation;

public sealed class ActionRunner : IDisposable
{
    #region Fields

    private readonly InputSimulator _inputSimulator;
    private readonly ScreenshotManager _screenshotManager;
    private readonly Dictionary<string, VirtualKeyCode> _keyMap;
    private readonly List<string> _screenshots;
    private readonly TesseractEngine _tesseractEngine;

    #endregion

    #region Constructor

    public ActionRunner(InputSimulator inputSimulator)
    {
        _inputSimulator = inputSimulator;
        _screenshotManager = new ScreenshotManager();
        _keyMap = new Dictionary<string, VirtualKeyCode>(StringComparer.OrdinalIgnoreCase);
        _screenshots = new List<string>();
        _tesseractEngine = new(@"./Tesseract", "eng", EngineMode.Default);

        BuildKeyMap();
    }

    #endregion

    #region Properties

    public IReadOnlyList<string> Screenshots
    { 
        get { return _screenshots.AsReadOnly(); }
    }

    #endregion

    #region Public Methods

    public void Dispose()
    {
        _tesseractEngine?.Dispose();
    }

    public void RunAction(nint windowHandle, string jobName, JobAction action, int postDelay)
    {
        if (action.Name == "KeyPress")
        {
            KeyPress(action);
        }
        else if (action.Name == "KeyDown")
        {
            KeyDown(action);
        }
        else if (action.Name == "KeyUp")
        {
            KeyUp(action);
        }
        else if (action.Name == "MoveMouseTo")
        {
            MoveMouseTo(windowHandle, action);
        }
        else if (action.Name == "MoveMouseBy")
        {
            MoveMouseBy(action);
        }
        else if (action.Name == "MouseClick")
        {
            MouseClick(action);
        }
        else if (action.Name == "MouseDown")
        {
            MouseDown(action);
        }
        else if (action.Name == "MouseUp")
        {
            MouseUp(action);
        }
        else if (action.Name == "Delay")
        {
            Delay(windowHandle, action);
        }
        else if (action.Name == "Screenshot")
        {
            string screenshotPath = _screenshotManager.TakeScreenshot(windowHandle, jobName);

            if (!String.IsNullOrEmpty(screenshotPath))
            {
                _screenshots.Add(screenshotPath);
            }
        }

        if (!action.SkipDelay)
        {
            Thread.Sleep(postDelay);
        }
    }

    #endregion

    #region Private Methods

    private void BuildKeyMap()
    {
        foreach (VirtualKeyCode keyCode in Enum.GetValues<VirtualKeyCode>())
        {
            _keyMap.TryAdd(keyCode.ToString().Replace("VK_", String.Empty), keyCode);
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

    private void KeyPress(JobAction action)
    {
        if (action.With.ContainsKey("key"))
        {
            VirtualKeyCode? keyCode = GetKey(action.With["key"]);

            if (keyCode.HasValue)
            {
                _inputSimulator.Keyboard.KeyDown(keyCode.Value).Sleep(50).KeyUp(keyCode.Value);
                Console.WriteLine($"KeyPress: {keyCode.Value}");
            }
        }
    }

    private void KeyDown(JobAction action)
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

    private void KeyUp(JobAction action)
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

    private void MoveMouseTo(nint windowHandle, JobAction action)
    {
        if (action.With.ContainsKey("text"))
        {
            int retries = 3;
            bool found = false;
            string text = action.With["text"];

            while (!found && retries-- > 0)
            {
                byte[] imageData = _screenshotManager.TakeScreenshot(windowHandle);

                Console.WriteLine($"Searching for text {text}...");

                if (imageData != null)
                {
                    using Pix image = Pix.LoadFromMemory(imageData);
                    using Page page = _tesseractEngine.Process(image);

                    using ResultIterator iterator = page.GetIterator();
                    iterator.Begin();

                    do
                    {
                        string pageText = iterator.GetText(PageIteratorLevel.Block);

                        if (!String.IsNullOrWhiteSpace(pageText))
                        {
                            Console.WriteLine($"Found block {pageText}");

                            if (pageText.Contains(text, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine("Getting bounding box...");

                                if (iterator.TryGetBoundingBox(PageIteratorLevel.Block, out Rect block))
                                {
                                    Rectangle windowRect = NativeMethods.GetWindowRect(windowHandle);
                                    float centerX = windowRect.Left + (block.X1 + (block.Width / 2f));
                                    float centerY = windowRect.Top + (block.Y1 + (block.Height / 2f));
                                    MoveMouseTo(windowHandle, centerX, centerY);
                                    found = true;
                                }
                            }
                        }
                    }
                    while (!found && iterator.Next(PageIteratorLevel.TextLine));
                }
            }
        }
        else if (action.With.ContainsKey("x") || action.With.ContainsKey("y"))
        {
            action.With.TryGetValue("x", out string xString);
            action.With.TryGetValue("y", out string yString);
            action.With.TryGetValue("absolute", out string absoluteString);

            Single.TryParse(xString, out float x);
            Single.TryParse(yString, out float y);
            Boolean.TryParse(absoluteString, out bool absolute);

            if (!absolute)
            {
                Rectangle windowRect = NativeMethods.GetWindowRect(windowHandle);
                x = windowRect.Left + x;
                y = windowRect.Top + y;
            }

            MoveMouseTo(windowHandle, x, y);
        }
    }

    private void MoveMouseTo(nint windowHandle, float x, float y)
    {
        Console.WriteLine($"Moving mouse to {x}, {y}");

        Rectangle monitorRect = NativeMethods.GetMonitorSize(windowHandle);
        float normalizedX = x * (65536f / monitorRect.Width);
        float normalizedY = y * (65536f / monitorRect.Height);
        _inputSimulator.Mouse.MoveMouseTo(normalizedX, normalizedY);
    }

    private void MoveMouseBy(JobAction action)
    {
        if (action.With.ContainsKey("x") || action.With.ContainsKey("y"))
        {
            action.With.TryGetValue("x", out string xString);
            action.With.TryGetValue("y", out string yString);

            Single.TryParse(xString, out float x);
            Single.TryParse(yString, out float y);

            Console.WriteLine($"Moving mouse by {x}, {y}");

            _inputSimulator.Mouse.MoveMouseBy((int)x, (int)y);
        }
    }

    private void MouseClick(JobAction action)
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

    private void MouseDown(JobAction action)
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

    private void MouseUp(JobAction action)
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

    private void Delay(nint windowHandle, JobAction action)
    {
        if (action.With.ContainsKey("time") &&
            Int32.TryParse(action.With["time"], out int delay))
        {
            Console.WriteLine($"Delaying: {delay}...");
            Thread.Sleep(delay);
            Console.WriteLine("Delay Complete.");
        }
        else if (action.With.ContainsKey("text"))
        {
            string text = action.With["text"];
            Console.WriteLine($"Delaying: {text}...");

            bool foundText = false;
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (!foundText && stopwatch.ElapsedMilliseconds < 60000)
            {
                Thread.Sleep(1000);

                byte[] imageData = _screenshotManager.TakeScreenshot(windowHandle);

                if (imageData != null)
                {
                    using Pix image = Pix.LoadFromMemory(imageData);
                    using Page page = _tesseractEngine.Process(image);
                    string pageText = page.GetText();

                    if (!String.IsNullOrWhiteSpace(pageText))
                    {
                        foundText = pageText.Contains(text, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            if (foundText)
            {
                Console.WriteLine("Delay Complete - Text Found.");
            }
            else
            {
                Console.WriteLine("Delay Timeout.");
            }
        }
    }

    #endregion
}
