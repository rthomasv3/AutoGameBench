using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Hook.DX11;

public class D3D11Hook : IGraphicsHook
{
    #region Delegates

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    private delegate int DXGISwapChain_PresentDelegate(nint swapChainPtr, int syncInterval, PresentFlags flags);

    #endregion

    #region Fields

    private const int DXGI_SWAPCHAIN_METHOD_COUNT = 18;

    private nint _presentAddress;
    private Hook<DXGISwapChain_PresentDelegate> _presentHook;
    private DateTime _lastUpdateTime = DateTime.UtcNow;
    private Logger _logger;

    #endregion

    #region Constructor

    public D3D11Hook(nint windowHandle, Logger logger)
    {
        _logger = logger;
        Initialize(windowHandle);
    }

    #endregion

    #region Public Methods

    public void Hook()
    {
        _presentHook = new Hook<DXGISwapChain_PresentDelegate>(
                _presentAddress,
                new DXGISwapChain_PresentDelegate(PresentHook),
                this);
        _presentHook.Activate();

        _logger.Log("DirectX11 Hook Applied.");
    }

    public void Unhook()
    {
        if (_presentHook != null)
        {
            _presentHook.Deactivate();
            _presentHook.Dispose();
            _presentHook = null;

            _logger.Log("DirectX11 Hook Removed.");
        }
    }

    #endregion

    #region Private Methods

    private void Initialize(nint windowHandle)
    {
        SwapChainDescription descrip = new()
        {
            BufferCount = 1,
            Flags = SwapChainFlags.None,
            Windowed = true,
            BufferDescription = new ModeDescription(100, 100, new Rational(60, 1), Format.R8G8B8A8_UNorm),
            OutputWindow = windowHandle,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.Discard,
            BufferUsage = Usage.RenderTargetOutput
        };

        D3D11.D3D11CreateDeviceAndSwapChain(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            null,
            descrip,
            out IDXGISwapChain swapChain,
            out _,
            out _,
            out _).CheckError();

        List<nint> vTableAddresses = new List<nint>();
        nint vTable = Marshal.ReadIntPtr(swapChain.NativePointer);
        for (int i = 0; i < DXGI_SWAPCHAIN_METHOD_COUNT; ++i)
        {
            vTableAddresses.Add(Marshal.ReadIntPtr(vTable, i * nint.Size));
        }

        swapChain.Dispose();

        _presentAddress = vTableAddresses[(int)DXGISwapChainVTable.Present];

        _logger.Log($"Got present address: {_presentAddress}");
    }

    private nint GetPresentAddress_Old()
    {
        FeatureLevel[] featureLevels = new[]
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0
        };

        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug,
            featureLevels,
            out ID3D11Device tempDevice,
            out ID3D11DeviceContext _).CheckError();

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

        IDXGIDevice dxgiDevice = tempDevice.QueryInterface<IDXGIDevice>();
        IDXGIAdapter1 dxgiAdapter = dxgiDevice.GetParent<IDXGIAdapter1>();
        IDXGIFactory2 dxgiFactory2 = dxgiAdapter.GetParent<IDXGIFactory2>();
        IDXGISwapChain1 swapChain = dxgiFactory2.CreateSwapChainForComposition(tempDevice, chainDescription, null);

        List<nint> vtblAddresses = new List<nint>();
        nint vTable = Marshal.ReadIntPtr(swapChain.NativePointer);
        for (int i = 0; i < 18; ++i)
        {
            vtblAddresses.Add(Marshal.ReadIntPtr(vTable, i * nint.Size));
        }

        try
        {
            dxgiFactory2.Dispose();
            dxgiAdapter.Dispose();
            dxgiDevice.Dispose();
            swapChain.Dispose();
        }
        catch { }

        return vtblAddresses[8];
    }

    private int PresentHook(nint swapChainPtr, int syncInterval, PresentFlags flags)
    {
        double frameTime = (DateTime.UtcNow - _lastUpdateTime).TotalMilliseconds;
        _lastUpdateTime = DateTime.UtcNow;

        _logger.Log($"Frame time: {frameTime}");

        return _presentHook.Original(swapChainPtr, syncInterval, flags);
    }

    #endregion
}
