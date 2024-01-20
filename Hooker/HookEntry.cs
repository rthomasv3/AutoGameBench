using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Hook
{
    public class HookEntry
    {
        private CoreHook.LocalHook _coreHook;
        private Hook<DXGISwapChain_PresentDelegate> _hook;
        private DXGISwapChain_PresentDelegate _orignalPresentFunction;
        private DateTime _lastUpdateTime = DateTime.UtcNow;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int DXGISwapChain_PresentDelegate(IntPtr swapChainPtr, int syncInterval, PresentFlags flags);

        public int StartPresentHook(IntPtr windowHandle)
        {
            SwapChainDescription description = new SwapChainDescription
            {
                BufferCount = 1,
                Flags = SwapChainFlags.None,
                IsWindowed = true,
                ModeDescription = new ModeDescription(100, 100, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                OutputHandle = windowHandle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            SharpDX.Direct3D11.Device.CreateWithSwapChain(
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                description,
                out _,
                out SwapChain swapChain);

            List<IntPtr> vTableAddresses = new List<IntPtr>();
            IntPtr vTable = Marshal.ReadIntPtr(swapChain.NativePointer);
            for (int i = 0; i < 18; ++i)
            {
                vTableAddresses.Add(Marshal.ReadIntPtr(vTable, i * IntPtr.Size));
            }

            swapChain.Dispose();

            File.AppendAllText("C:\\Users\\Robert\\Code\\AutoGameBench\\hook_entry.txt", $"Present Address: {vTableAddresses[8]}\n");

            // *** Works with EasyHook ***
            //_easyPresentHook = LocalHook.Create(vTableAddresses[8], new DXGISwapChain_PresentDelegate(PresentHook), this);
            //_easyPresentHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
            //_orignalPresentFunction = (DXGISwapChain_PresentDelegate)(object)Marshal.GetDelegateForFunctionPointer(vTableAddresses[8], typeof(DXGISwapChain_PresentDelegate));

            // *** Doesn't work with ReloadedHooks ***
            //_reloadedPresentHook = ReloadedHooks.Instance.CreateHook<DXGISwapChain_PresentDelegate>(PresentHook, vTableAddresses[8].ToInt64());
            //_reloadedPresentHook.Activate();
            //_orignalPresentFunction = _reloadedPresentHook.OriginalFunction;

            // *** Works with CoreHook ***
            //_coreHook = CoreHook.LocalHook.Create(vTableAddresses[8], new DXGISwapChain_PresentDelegate(PresentHook), this);
            //_coreHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
            //_orignalPresentFunction = (DXGISwapChain_PresentDelegate)(object)Marshal.GetDelegateForFunctionPointer(vTableAddresses[8], typeof(DXGISwapChain_PresentDelegate));

            _hook = new Hook<DXGISwapChain_PresentDelegate>(
                vTableAddresses[8],
                new DXGISwapChain_PresentDelegate(PresentHook),
                this);
            _hook.Activate();

            return 1;
        }

        public int StopPresentHook()
        {
            if (_coreHook != null)
            {
                _coreHook.ThreadACL.SetInclusiveACL(new Int32[] { 0 });
                _coreHook.Dispose();
                _coreHook = null;
            }

            if (_hook != null)
            {
                _hook.Dispose();
                _hook = null;
            }

            return 1;
        }

        private int PresentHook(IntPtr swapChainPtr, int syncInterval, PresentFlags flags)
        {
            double frameTime = (DateTime.UtcNow - _lastUpdateTime).TotalMilliseconds;
            _lastUpdateTime = DateTime.UtcNow;

            File.AppendAllText("C:\\Users\\Robert\\Code\\AutoGameBench\\hook_entry.txt", $"Frame time: {frameTime}\n");

            //return _orignalPresentFunction(swapChainPtr, syncInterval, flags);
            return _hook.Original(swapChainPtr, syncInterval, flags);
        }
    }
}
