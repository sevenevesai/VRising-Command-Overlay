using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace overlayc
{
    public class KeyboardHook : IDisposable
    {
        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);
        private readonly LowLevelProc proc;
        private IntPtr hookId = IntPtr.Zero;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);

        public event Action<int>? OnKeyPressed;

        public KeyboardHook()
        {
            proc = HookCallback;
            using var curProc = Process.GetCurrentProcess();
            using var curMod  = curProc.MainModule!;
            hookId = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curMod.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                OnKeyPressed?.Invoke(vkCode);
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public void Dispose() => UnhookWindowsHookEx(hookId);
    }
}
