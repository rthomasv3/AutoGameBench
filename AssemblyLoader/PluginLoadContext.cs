using System;
using System.Reflection;
using System.Runtime.Loader;

namespace AssemblyLoader
{
    internal class PluginLoadContext : AssemblyLoadContext
    {
        #region Fields

        private readonly AssemblyDependencyResolver _resolver;

        #endregion

        #region Constructor

        public PluginLoadContext(string path)
            : base(true)
        {
            _resolver = new AssemblyDependencyResolver(path);
        }

        #endregion

        #region Public Methods

        protected override Assembly Load(AssemblyName assemblyName)
        {
            Assembly assembly = null;
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

            if (assemblyPath != null)
            {
                assembly = LoadFromAssemblyPath(assemblyPath);
            }

            return assembly;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            IntPtr assemblyPointer = IntPtr.Zero;
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

            if (libraryPath != null)
            {
                assemblyPointer = LoadUnmanagedDllFromPath(libraryPath);
            }

            return assemblyPointer;
        }

        #endregion
    }
}
