using System;
using System.Runtime.InteropServices;
using CoreHook;

namespace Hook
{
    public class Hook<T> : Hook
        where T : class
    {
        public T Original { get; private set; }

        public Hook(IntPtr funcToHook, Delegate newFunc, object owner)
            : base(funcToHook, newFunc, owner)
        {
            System.Diagnostics.Debug.Assert(typeof(Delegate).IsAssignableFrom(typeof(T)));

            Original = (T)(object)Marshal.GetDelegateForFunctionPointer(funcToHook, typeof(T));
        }
    }

    public class Hook : IDisposable
    {
        public IntPtr FuncToHook { get; private set; }

        public Delegate NewFunc { get; private set; }

        public object Owner { get; private set; }

        public LocalHook LocalHook { get; private set; }

        public bool IsActive { get; private set; }

        public Hook(IntPtr funcToHook, Delegate newFunc, object owner)
        {
            this.FuncToHook = funcToHook;
            this.NewFunc = newFunc;
            this.Owner = owner;

            CreateHook();
        }

        ~Hook()
        {
            Dispose(false);
        }

        protected void CreateHook()
        {
            if (LocalHook != null) return;

            this.LocalHook = LocalHook.Create(FuncToHook, NewFunc, Owner);
        }

        protected void UnHook()
        {
            if (this.IsActive)
                Deactivate();

            if (this.LocalHook != null)
            {
                this.LocalHook.Dispose();
                this.LocalHook = null;
            }
        }

        public void Activate()
        {
            if (this.LocalHook == null)
                CreateHook();

            if (this.IsActive) return;

            this.IsActive = true;
            this.LocalHook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
        }

        public void Deactivate()
        {
            if (!this.IsActive) return;

            this.IsActive = false;
            this.LocalHook.ThreadACL.SetInclusiveACL(new Int32[] { 0 });
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposeManagedObjects)
        {
            if (disposeManagedObjects)
            {
                UnHook();
            }
        }
    }
}
