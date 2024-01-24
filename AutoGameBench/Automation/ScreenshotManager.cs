using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        Rectangle windowRect = NativeMethods.GetWindowRect(windowHandle);
        return TakeScreenshot(windowRect, jobName);
    }

    public string TakeScreenshot(Rectangle windowRect, string jobName)
    {
        string path = null;

        if (windowRect.Width > 0 && windowRect.Height > 0)
        {
            using Bitmap bitmap = GetScreenImage(windowRect);
            string safeJobName = String.Join("_", jobName.Split(_invalidFileCharacters));
            path = Path.Combine(_screenshotDirectory, $"{safeJobName}_{DateTime.Now.Ticks}.png");
            bitmap.Save(path, ImageFormat.Png);
        }

        return path;
    }

    public byte[] TakeScreenshot(nint windowHandle)
    {
        byte[] imageBytes = null;

        Rectangle windowRect = NativeMethods.GetWindowRect(windowHandle);

        if (windowRect.Width > 0 && windowRect.Height > 0)
        {
            using Bitmap bitmap = GetScreenImage(windowRect);
            using MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            imageBytes = ms.ToArray();
        }

        return imageBytes;
    }

    #endregion

    #region Private Methods

    private Bitmap GetScreenImage(Rectangle windowRect)
    {
        Bitmap bitmap = null;

        int width = windowRect.Width;
        int height = windowRect.Height - _headerHeight - _shadowHeight;

        if (width > 0 && height > 0)
        {
            bitmap = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(windowRect.Left, windowRect.Top + _headerHeight, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            }
        }

        return bitmap;
    }

    #endregion
}
