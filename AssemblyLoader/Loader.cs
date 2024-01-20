using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AssemblyLoader
{
    public static class Loader
    {
        #region Fields

        private static PluginLoadContext _dependencyLoadContext;
        private static object _hookInstance;
        private static MethodInfo _startHookMethod;
        private static MethodInfo _stopHookMethod;

        #endregion

        #region Constructor

        static Loader()
        {
            string assemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Hook.dll");
            PluginLoadContext assemblyLoadContext = new(assemblyPath);
            Assembly assembly = assemblyLoadContext.LoadFromAssemblyPath(assemblyPath);
            Type hookType = assembly.GetType("Hook.HookEntry");
            _startHookMethod = hookType.GetMethod("StartPresentHook");
            _stopHookMethod = hookType.GetMethod("StopPresentHook");
            _hookInstance = Activator.CreateInstance(hookType);
        }

        #endregion

        #region Public Methods

#if X86
        [UnmanagedCallersOnly(CallConvs = new []{typeof(CallConvCdecl)})]
#else
        [UnmanagedCallersOnly]
#endif
        public static int StartHook(nint windowHandle)
        {
            return (int)_startHookMethod.Invoke(_hookInstance, new object[] { windowHandle });
        }

#if X86
        [UnmanagedCallersOnly(CallConvs = new []{typeof(CallConvCdecl)})]
#else
        [UnmanagedCallersOnly]
#endif
        public static int StopHook()
        {
            if (_stopHookMethod != null)
            {
                _stopHookMethod.Invoke(_hookInstance, null);
            }

            if (_dependencyLoadContext != null)
            {
                _dependencyLoadContext.Unload();
            }

            _dependencyLoadContext = null;
            _hookInstance = null;
            _stopHookMethod = null;

            return 1;
        }

        #endregion
    }
}
