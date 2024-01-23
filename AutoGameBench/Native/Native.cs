using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AutoGameBench.Native;

public class Native
{
    #region Imports

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hwnd, ref Rectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, WindowShowStyle nCmdShow);

    public enum WindowShowStyle : uint
    {
        Hide = 0,
        ShowNormal = 1,
        ShowMinimized = 2,
        ShowMaximized = 3,
        Maximize = 3,
        ShowNormalNoActivate = 4,
        Show = 5,
        Minimize = 6,
        ShowMinNoActivate = 7,
        ShowNoActivate = 8,
        Restore = 9,
        ShowDefault = 10,
        ForceMinimized = 11
    }

    #endregion

    #region Public Methods

    public static Rectangle GetWindowRect(IntPtr hwnd)
    {
        Rectangle rectangle = default;
        int attempts = 0;

        while (attempts++ < 3)
        {
            rectangle = new Rectangle();
            GetWindowRect(hwnd, ref rectangle);

            if (rectangle.Width > 0 && rectangle.Height > 0)
            {
                break;
            }
        }

        return rectangle;
    }

    public static bool IsWindowForeground(IntPtr hWnd)
    {
        return hWnd == GetForegroundWindow();
    }

    #endregion
}
