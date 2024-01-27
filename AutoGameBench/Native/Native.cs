using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

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

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(SystemMetric metricIndex);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr handle, Int32 flags);

    [DllImport("user32.dll")]
    public static extern Boolean GetMonitorInfo(IntPtr hMonitor, NativeMonitorInfo lpmi);

    #endregion

    #region Constants

    public const Int32 MONITOR_DEFAULTTOPRIMERTY = 0x00000001;
    public const Int32 MONITOR_DEFAULTTONEAREST = 0x00000002;

    #endregion

    #region Enums

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

    public enum SystemMetric
    {
        SM_CXSCREEN = 0, // The width of the screen of the primary display monitor, in pixels.
        SM_CYSCREEN = 1, // The height of the screen of the primary display monitor, in pixels.
        SM_CXBORDER = 5, // The width of a window border, in pixels.
        SM_CYBORDER = 6, // The height of a window border, in pixels.
        SM_CXFULLSCREEN = 16, // The width of the client area for a full-screen window on the primary display monitor, in pixels. 
        SM_CYFULLSCREEN = 17, // The height of the client area for a full-screen window on the primary display monitor, in pixels.
        SM_XVIRTUALSCREEN = 76, // The coordinates for the left side of the virtual screen.
        SM_YVIRTUALSCREEN = 77, // The coordinates for the top of the virtual screen.
        SM_CXVIRTUALSCREEN = 78, // The width of the virtual screen, in pixels. The virtual screen is the bounding rectangle of all display monitors.
        SM_CYVIRTUALSCREEN = 79, // The height of the virtual screen, in pixels. The virtual screen is the bounding rectangle of all display monitors.
    }

    #endregion

    #region Classes and Structs

    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct NativeRectangle
    {
        public Int32 Left;
        public Int32 Top;
        public Int32 Right;
        public Int32 Bottom;


        public NativeRectangle(Int32 left, Int32 top, Int32 right, Int32 bottom)
        {
            this.Left = left;
            this.Top = top;
            this.Right = right;
            this.Bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public sealed class NativeMonitorInfo
    {
        public Int32 Size = Marshal.SizeOf(typeof(NativeMonitorInfo));
        public NativeRectangle Monitor;
        public NativeRectangle Work;
        public Int32 Flags;
    }

    #endregion

    #region Public Methods

    public static Rectangle GetWindowRect(IntPtr hwnd)
    {
        Rectangle rectangle = default;
        int attempts = 0;

        while (attempts++ < 5)
        {
            rectangle = new Rectangle();
            GetWindowRect(hwnd, ref rectangle);

            if (rectangle.Width > 0 && rectangle.Height > 0)
            {
                break;
            }

            Thread.Sleep(100);
        }

        return rectangle;
    }

    public static bool IsWindowForeground(IntPtr hWnd)
    {
        return hWnd == GetForegroundWindow();
    }

    public static Rectangle GetMonitorSize(IntPtr hWnd)
    {
        Rectangle rectangle = Rectangle.Empty;

        IntPtr monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);

        if (monitor != IntPtr.Zero)
        {
            NativeMonitorInfo monitorInfo = new();
            GetMonitorInfo(monitor, monitorInfo);
            rectangle = new(
                monitorInfo.Monitor.Left, 
                monitorInfo.Monitor.Top, 
                monitorInfo.Monitor.Right - monitorInfo.Monitor.Left, 
                monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top
            );
        }

        return rectangle;
    }

    #endregion
}
