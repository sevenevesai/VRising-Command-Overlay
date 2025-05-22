using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using overlayc.Input;

namespace overlayc
{
    public class KeyboardHook : IDisposable
    {
        private readonly NativeMethods.LowLevelKeyboardProc proc;
        private IntPtr hookId = IntPtr.Zero;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN    = 0x0100;

        public event Action<int>? OnKeyPressed;

        public KeyboardHook()
        {
            proc   = HookCallback;
            hookId = NativeMethods.SetHook(proc);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                // offload quickly so the hook thread never stalls
                Task.Run(() => OnKeyPressed?.Invoke(vkCode));
            }
            return NativeMethods.CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (hookId != IntPtr.Zero)
                NativeMethods.UnhookWindowsHookEx(hookId);
        }
    }
}
