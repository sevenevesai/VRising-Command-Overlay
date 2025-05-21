using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace overlayc
{
    public static class KeyInjector
    {
        // --- Win32 APIs ---
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(
            byte bVk,
            byte bScan,
            uint dwFlags,
            UIntPtr dwExtraInfo
        );

        // --- Constants ---
        private const int    SW_RESTORE       = 9;
        private const byte   VK_RETURN        = 0x0D;
        private const uint   MAPVK_VK_TO_VSC  = 0;
        private const uint   KEYEVENTF_KEYUP   = 0x0002;
        private const uint   KEYEVENTF_UNICODE = 0x0004;

        // --- Helpers matching your Python code ---
        private static void PressSc(byte vk)
        {
            uint sc = MapVirtualKey(vk, MAPVK_VK_TO_VSC);
            keybd_event(vk, (byte)sc, 0, UIntPtr.Zero);
            Thread.Sleep(5);
            keybd_event(vk, (byte)sc, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(5);
        }

        private static void SendTextSc(string text)
        {
            foreach (char ch in text)
            {
                byte scan = (byte)ch;
                // key down Unicode
                keybd_event(0, scan, KEYEVENTF_UNICODE, UIntPtr.Zero);
                // key up Unicode
                keybd_event(0, scan, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(10);
            }
        }

        // --- Find VRising window by process name ---
        private static IntPtr FindVRisingWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                GetWindowThreadProcessId(hWnd, out uint pid);
                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    if (string.Equals(proc.ProcessName, "VRising", StringComparison.OrdinalIgnoreCase))
                    {
                        found = hWnd;
                        return false; // stop enumeration
                    }
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static void SendCommand(string command)
        {
            var hwnd = FindVRisingWindow();
            if (hwnd == IntPtr.Zero) return;

            // 1) Restore & focus the game
            ShowWindow(hwnd, SW_RESTORE);
            Thread.Sleep(50);
            SetForegroundWindow(hwnd);
            Thread.Sleep(50);

            // 2) Open chat
            PressSc(VK_RETURN);
            Thread.Sleep(50);

            // 3) Type the command
            SendTextSc(command);
            Thread.Sleep(50);

            // 4) Send chat
            PressSc(VK_RETURN);
        }
    }
}
