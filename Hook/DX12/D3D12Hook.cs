using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.DXGI;

namespace Hook.DX12;

public sealed class D3D12Hook : IGraphicsHook
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

        _ipcClient.Log("DirectX11 Hook Applied.");
    }

    public void Unhook()
    {
        if (_presentHook != null)
        {
            _presentHook.Deactivate();
            _presentHook.Dispose();
            _presentHook = null;

            _ipcClient.Log("DirectX11 Hook Removed.");
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
        
        Vortice.Direct3D11.D3D11.D3D11CreateDeviceAndSwapChain(
            null,
            DriverType.Hardware,
            Vortice.Direct3D11.DeviceCreationFlags.BgraSupport,
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

        _presentAddress = vTableAddresses[(int)DX11.DXGISwapChainVTable.Present];

        _ipcClient.Log($"Got present address: {_presentAddress}");
    }

    private int PresentHook(nint swapChainPtr, int syncInterval, PresentFlags flags)
    {
        double frameTime = (DateTime.UtcNow - _lastUpdateTime).TotalMilliseconds;
        _lastUpdateTime = DateTime.UtcNow;

        _ipcClient.SendFrameTime(frameTime);

        return _presentHook.Original(swapChainPtr, syncInterval, flags);
    }

    #endregion
}
