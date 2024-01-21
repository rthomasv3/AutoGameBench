using System;
using System.Runtime.InteropServices;
using System.Threading;
using Hook.DX11;
using Hook.DX12;

namespace Hook;

public class HookEntry
{
    #region Native

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    #endregion

    #region Fields

    private readonly IpcClient _ipcClient;
    private IGraphicsHook _graphicsHook;

    #endregion

    #region Constructor

    public HookEntry()
    {
        _ipcClient = new IpcClient();
    }

    #endregion

    #region Public Methods

    public int StartPresentHook(nint windowHandle)
    {
        try
        {
            _ipcClient.Log("Hook Entered.");

            DirectXVersion directXVersion = DetectDirectXVersion();

            _graphicsHook = directXVersion switch
            {
                DirectXVersion.Direct3D12 => new D3D12Hook(windowHandle, _ipcClient),
                DirectXVersion.Direct3D11_1 => new D3D11Hook(windowHandle, _ipcClient),
                DirectXVersion.Direct3D11 => new D3D11Hook(windowHandle, _ipcClient),
                _ => null
            };

            if (_graphicsHook != null)
            {
                _graphicsHook.Hook();
            }
        }
        catch (Exception e)
        {
            _ipcClient.Log(e.ToString());
        }

        return 1;
    }

    public int StopPresentHook()
    {
        if (_graphicsHook != null)
        {
            _graphicsHook.Unhook();
        }

        _ipcClient.Log("Hook Stopped.");
        _ipcClient.Dispose();

        return 1;
    }

    #endregion

    #region Private Methods

    private DirectXVersion DetectDirectXVersion()
    {
        DirectXVersion version = DirectXVersion.Unknown;

        IntPtr d3D9Loaded = IntPtr.Zero;
        IntPtr d3D10Loaded = IntPtr.Zero;
        IntPtr d3D10_1Loaded = IntPtr.Zero;
        IntPtr d3D11Loaded = IntPtr.Zero;
        IntPtr d3D11_1Loaded = IntPtr.Zero;
        IntPtr d3D12Loaded = IntPtr.Zero;

        int delayTime = 100;
        int retryCount = 0;
        while (d3D9Loaded == IntPtr.Zero && 
               d3D10Loaded == IntPtr.Zero && 
               d3D10_1Loaded == IntPtr.Zero && 
               d3D11Loaded == IntPtr.Zero && 
               d3D11_1Loaded == IntPtr.Zero &&
               retryCount++ * delayTime < 5000)
        {
            retryCount++;
            d3D9Loaded = GetModuleHandle("d3d9.dll");
            d3D10Loaded = GetModuleHandle("d3d10.dll");
            d3D10_1Loaded = GetModuleHandle("d3d10_1.dll");
            d3D11Loaded = GetModuleHandle("d3d11.dll");
            d3D11_1Loaded = GetModuleHandle("d3d11_1.dll");
            d3D12Loaded = GetModuleHandle("d3d12.dll");
            Thread.Sleep(delayTime);
        }

        if (d3D12Loaded != IntPtr.Zero)
        {
            version = DirectXVersion.Direct3D12;
        }
        if (d3D11_1Loaded != IntPtr.Zero)
        {
            version = DirectXVersion.Direct3D11_1;
        }
        if (d3D11Loaded != IntPtr.Zero)
        {
            version = DirectXVersion.Direct3D11;
        }
        if (d3D10_1Loaded != IntPtr.Zero)
        {
            version = DirectXVersion.Direct3D10_1;
        }
        if (d3D10Loaded != IntPtr.Zero)
        {
            version = DirectXVersion.Direct3D10;
        }
        if (d3D9Loaded != IntPtr.Zero)
        {
            version = DirectXVersion.Direct3D9;
        }

        _ipcClient.Log($"DirectXVersion: {version}");

        return version;
    }

    #endregion
}
