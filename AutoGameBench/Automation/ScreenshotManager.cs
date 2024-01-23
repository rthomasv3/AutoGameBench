using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Vortice;
using NativeMethods = AutoGameBench.Native.Native;

namespace AutoGameBench.Automation;

public sealed class ScreenshotManager
{
    #region Fields

    private readonly string _screenshotDirectory = "Automation\\Screenshots";
    private readonly char[] _invalidFileCharacters = [' ', .. Path.GetInvalidFileNameChars()];
    private readonly int _headerHeight = 32;
    private readonly int _shadowHeight = 10;

    #endregion

    #region Constructor

    public ScreenshotManager()
    {
        if (!Directory.Exists(_screenshotDirectory))
        {
            Directory.CreateDirectory(_screenshotDirectory);
        }
    }

    #endregion

    #region Public Methods

    public string TakeScreenshot(Process process, string jobName)
    {
        return TakeScreenshot(process.MainWindowHandle, jobName);
    }

    public string TakeScreenshot(nint windowHandle, string jobName)
    {
        string path = null;

        RawRect windowRect = NativeMethods.GetWindowRect(windowHandle);

        int width = windowRect.Right - windowRect.Left;
        int height = windowRect.Bottom - windowRect.Top - _headerHeight - _shadowHeight;

        Console.WriteLine($"x: {windowRect.Left}, y: {windowRect.Top}, width: {width}, height: {height}");

        if (width > 0 && height > 0)
        {
            using (Bitmap bitmap = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(windowRect.Left, windowRect.Top + _headerHeight, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                }

                string safeJobName = String.Join("_", jobName.Split(_invalidFileCharacters));
                path = Path.Combine(_screenshotDirectory, $"{safeJobName}_{DateTime.Now.Ticks}.jpg");

                bitmap.Save(path, ImageFormat.Jpeg);
            }
        }

        return path;
    }

    public string TakeScreenshot(RawRect windowRect, string jobName)
    {
        string path = null;

        int width = windowRect.Right - windowRect.Left;
        int height = windowRect.Bottom - windowRect.Top - _headerHeight - _shadowHeight;

        Console.WriteLine($"x: {windowRect.Left}, y: {windowRect.Top}, width: {width}, height: {height}");

        if (width > 0 && height > 0)
        {
            using (Bitmap bitmap = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(windowRect.Left, windowRect.Top + _headerHeight, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                }

                string safeJobName = String.Join("_", jobName.Split(_invalidFileCharacters));
                path = Path.Combine(_screenshotDirectory, $"{safeJobName}_{DateTime.Now.Ticks}.jpg");

                bitmap.Save(path, ImageFormat.Jpeg);
            }
        }

        return path;
    }

    #endregion
}
