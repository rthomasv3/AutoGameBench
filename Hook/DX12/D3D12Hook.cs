using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Hook.DX12;

public sealed class D3D12Hook : IGraphicsHook
{
    #region Native

    delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private WndProc delegWndProc = myWndProc;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct WNDCLASSEX
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cbSize;
        [MarshalAs(UnmanagedType.U4)]
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    const UInt32 WS_OVERLAPPEDWINDOW = 0xcf0000;
    const UInt32 WS_VISIBLE = 0x10000000;
    const UInt32 CS_USEDEFAULT = 0x80000000;
    const UInt32 CS_DBLCLKS = 8;
    const UInt32 CS_VREDRAW = 1;
    const UInt32 CS_HREDRAW = 2;
    const UInt32 COLOR_WINDOW = 5;
    const UInt32 COLOR_BACKGROUND = 1;
    const UInt32 IDC_CROSS = 32515;
    const UInt32 WM_DESTROY = 2;
    const UInt32 WM_PAINT = 0x0f;
    const UInt32 WM_LBUTTONUP = 0x0202;
    const UInt32 WM_LBUTTONDBLCLK = 0x0203;

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowA")]
    public static extern IntPtr CreateWindowA(
           string lpClassName,
           string lpWindowName,
           int dwStyle,
           int x,
           int y,
           int nWidth,
           int nHeight,
           IntPtr hWndParent,
           IntPtr hMenu,
           IntPtr hInstance,
           IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowEx")]
    public static extern IntPtr CreateWindowEx(
           int dwExStyle,
           UInt16 regResult,
           //string lpClassName,
           string lpWindowName,
           UInt32 dwStyle,
           int x,
           int y,
           int nWidth,
           int nHeight,
           IntPtr hWndParent,
           IntPtr hMenu,
           IntPtr hInstance,
           IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterClassEx")]
    static extern UInt16 RegisterClassEx([In] ref WNDCLASSEX lpWndClass);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    #endregion

    #region Delegates

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate long DXGISwapChain_PresentDelegate(IntPtr swapChain, uint syncInterval, uint presentFlags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    private delegate int DXGISwapChain_ResizeBuffersDelegate(uint bufferCount, uint width, uint height, Format newFormat, SwapChainFlags swapChainFlags);

    #endregion

    #region Fields

    private const int DXGI_SWAPCHAIN_METHOD_COUNT = 18;

    private nint _presentAddress;
    private nint _resizeBuffersAddress;
    private Hook<DXGISwapChain_PresentDelegate> _presentHook;
    private Hook<DXGISwapChain_ResizeBuffersDelegate> _resizeBuffersHook;
    private DateTime _lastUpdateTime = DateTime.UtcNow;
    private IpcClient _ipcClient;

    #endregion

    #region Constructor

    public D3D12Hook(nint windowHandle, IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        Initialize(windowHandle);
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    public void Hook()
    {
        _presentHook = new Hook<DXGISwapChain_PresentDelegate>(
                _presentAddress,
                new DXGISwapChain_PresentDelegate(PresentHook),
                this);
        _presentHook.Activate();

        _resizeBuffersHook = new Hook<DXGISwapChain_ResizeBuffersDelegate>(
                _resizeBuffersAddress,
                new DXGISwapChain_ResizeBuffersDelegate(ResizeBuffersHook),
                this);
        _resizeBuffersHook.Activate();

        _ipcClient.Log("DirectX12 Hook Applied.");
    }

    public void Unhook()
    {
        if (_presentHook != null)
        {
            _presentHook.Deactivate();
            _presentHook.Dispose();
            _presentHook = null;

            _ipcClient.Log("DirectX12 Hook Removed.");
        }

        if (_resizeBuffersHook != null)
        {
            _resizeBuffersHook.Deactivate();
            _resizeBuffersHook.Dispose();
            _resizeBuffersHook = null;
        }
    }

    #endregion

    #region Private Methods

    private void Initialize(nint windowHandle)
    {
        D3D12.D3D12CreateDevice(null, out ID3D12Device tempDevice);
        _ipcClient.Log($"ID3D12Device Loaded: {tempDevice != null}");

        SwapChainDescription1 chainDescription = new()
        {
            Stereo = false,
            Width = 800,
            Height = 600,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Scaling = Scaling.Stretch,
            AlphaMode = AlphaMode.Premultiplied,
            Flags = SwapChainFlags.None,
            SwapEffect = SwapEffect.FlipSequential,
        };

        ID3D12CommandQueue graphicsQueue = tempDevice.CreateCommandQueue(CommandListType.Direct);
        _ipcClient.Log($"ID3D12CommandQueue Created: {graphicsQueue != null}");

        IDXGIFactory4 dxgiFactory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(true);
        _ipcClient.Log($"IDXGIFactory4 Created: {dxgiFactory != null}");

        IDXGISwapChain1 tempSwapChain1 = null;
        IDXGISwapChain3 swapChain = null;

        try
        {
            IntPtr tempWindowHandle = CreateWindow(windowHandle);
            _ipcClient.Log($"Got Window Handle: {tempWindowHandle}");

            SwapChainDescription1 descrip = new()
            {
                Stereo = false,
                Width = 0,
                Height = 0,
                BufferCount = 3,
                BufferUsage = Usage.RenderTargetOutput,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Scaling = Scaling.Stretch,
                AlphaMode = AlphaMode.Unspecified,
                Flags = SwapChainFlags.FrameLatencyWaitableObject,
                SwapEffect = SwapEffect.FlipDiscard,
            };

            tempSwapChain1 = dxgiFactory.CreateSwapChainForHwnd(graphicsQueue, tempWindowHandle, descrip);
            _ipcClient.Log($"IDXGISwapChain1 Created Using Method 1: {tempSwapChain1 != null}");

            swapChain = tempSwapChain1.QueryInterface<IDXGISwapChain3>();
            _ipcClient.Log($"IDXGISwapChain3 Found Using Method 1: {swapChain != null}");

            DestroyWindow(tempWindowHandle);
        }
        catch (Exception e)
        {
            _ipcClient.Log(e.ToString());
        }

        if (tempSwapChain1 == null)
        {
            tempSwapChain1 = dxgiFactory.CreateSwapChainForComposition(graphicsQueue, chainDescription);
            _ipcClient.Log($"IDXGISwapChain1 Created: {tempSwapChain1 != null}");

            swapChain = tempSwapChain1.QueryInterface<IDXGISwapChain3>();
            _ipcClient.Log($"IDXGISwapChain3 Found: {swapChain != null}");
        }

        List<nint> vTableAddresses = new List<nint>();
        nint vTable = Marshal.ReadIntPtr(swapChain.NativePointer);
        for (int i = 0; i < DXGI_SWAPCHAIN_METHOD_COUNT; ++i)
        {
            vTableAddresses.Add(Marshal.ReadIntPtr(vTable, i * nint.Size));
        }

        _presentAddress = vTableAddresses[(int)DXGISwapChainVTable.Present]; // Present is 8
        _resizeBuffersAddress = vTableAddresses[(int)DXGISwapChainVTable.ResizeBuffers]; // ResizeBuffers is 13

        _ipcClient.Log($"Got present address: {_presentAddress}");

        try
        {
            tempDevice.Dispose();
            graphicsQueue.Dispose();
            dxgiFactory.Dispose();
            tempSwapChain1.Dispose();
            swapChain.Dispose();
        }
        catch { }
    }

    private long PresentHook(IntPtr swapChain, uint syncInterval, uint presentFlags)
    {
        _ipcClient.Log("Entered Present.");

        double frameTime = (DateTime.UtcNow - _lastUpdateTime).TotalMilliseconds;
        _lastUpdateTime = DateTime.UtcNow;

        _ipcClient.SendFrameTime(frameTime);

        return _presentHook.Original(swapChain, syncInterval, presentFlags);
    }

    private int ResizeBuffersHook(uint bufferCount, uint width, uint height, Format newFormat, SwapChainFlags swapChainFlags)
    {
        _ipcClient.Log("Entered ResizeBuffers.");
        return _resizeBuffersHook.Original(bufferCount, width, height, newFormat, swapChainFlags);
    }

    private IntPtr CreateWindow(IntPtr parentWindowHandle)
    {
        IntPtr windowHandle = IntPtr.Zero;

        WNDCLASSEX wind_class = new WNDCLASSEX();
        wind_class.cbSize = Marshal.SizeOf(typeof(WNDCLASSEX));
        wind_class.style = (int)(CS_HREDRAW | CS_VREDRAW);
        wind_class.cbClsExtra = 0;
        wind_class.cbWndExtra = 0;
        //wind_class.hInstance = Marshal.GetHINSTANCE(this.GetType().Module);// alternative: Process.GetCurrentProcess().Handle;
        //wind_class.hInstance = Process.GetCurrentProcess().Handle;
        //wind_class.hInstance = parentWindowHandle;
        wind_class.hInstance = GetModuleHandle(null);
        wind_class.hIcon = IntPtr.Zero;
        wind_class.hCursor = IntPtr.Zero;
        wind_class.hbrBackground = IntPtr.Zero;
        wind_class.lpszMenuName = null;
        wind_class.lpszClassName = "DX12_DUMMY";
        wind_class.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(delegWndProc);
        wind_class.hIconSm = IntPtr.Zero;

        ushort regResult = RegisterClassEx(ref wind_class);

        try
        {
            windowHandle = CreateWindowA(wind_class.lpszClassName, "DX 12 Dummy Window", (int)WS_OVERLAPPEDWINDOW, 0, 0, 100, 100, IntPtr.Zero, IntPtr.Zero, wind_class.hInstance, IntPtr.Zero);
        }
        catch { }

        if (windowHandle == IntPtr.Zero)
        {
            windowHandle = CreateWindowEx(0, regResult, "DX 12 Dummy Window", WS_OVERLAPPEDWINDOW, 0, 0, 100, 100, IntPtr.Zero, IntPtr.Zero, wind_class.hInstance, IntPtr.Zero);
        }

        return windowHandle;
    }

    private static IntPtr myWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            // All GUI painting must be done here
            case WM_PAINT:
                break;

            case WM_LBUTTONDBLCLK:
                break;

            case WM_DESTROY:
                break;

            default:
                break;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    #endregion
}
