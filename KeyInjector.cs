using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace overlayc
{
    public static class KeyInjector
    {
        // ─── Win32 APIs for window finding & focus ─────────────────────────────────
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
        private static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        // ─── keybd_event for injection ────────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(
            byte bVk,
            byte bScan,
            uint dwFlags,
            UIntPtr dwExtraInfo
        );

        // ─── Constants ──────────────────────────────────────────────────────────────
        private const int    SW_RESTORE       = 9;
        private const byte   VK_RETURN        = 0x0D;
        private const uint   MAPVK_VK_TO_VSC   = 0;
        private const uint   KEYEVENTF_KEYUP   = 0x0002;
        private const uint   KEYEVENTF_UNICODE = 0x0004;

        // ─── Force V Rising to foreground ──────────────────────────────────────────
        private static void ForceForeground(IntPtr hwnd)
        {
            var fg = GetForegroundWindow();
            uint fgThread     = GetWindowThreadProcessId(fg, out _);
            uint thisThread   = GetCurrentThreadId();
            uint targetThread = GetWindowThreadProcessId(hwnd, out _);

            AttachThreadInput(thisThread, fgThread,   true);
            AttachThreadInput(thisThread, targetThread, true);

            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);

            AttachThreadInput(thisThread, fgThread,   false);
            AttachThreadInput(thisThread, targetThread, false);
        }

        // ─── Press & release a virtual-key by scancode ─────────────────────────────
        private static void PressSc(byte vk)
        {
            uint sc = MapVirtualKey(vk, MAPVK_VK_TO_VSC);
            keybd_event(vk, (byte)sc, 0, UIntPtr.Zero);
            Thread.Sleep(5);
            keybd_event(vk, (byte)sc, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(5);
        }

        // ─── Send Unicode text via keybd_event ─────────────────────────────────────
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

        // ─── Find the V Rising window by process name ───────────────────────────────
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
                        return false; // stop
                    }
                }
                catch { }
                return true; // continue
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>
        /// Sends a chat/console command to V Rising, offloaded to a background thread:
        /// 1) Force-foreground the game  
        /// 2) Press Enter to open chat  
        /// 3) Type the command text  
        /// 4) Press Enter to submit  
        /// </summary>
        public static void SendCommand(string command)
        {
            Task.Run(() =>
            {
                var hwnd = FindVRisingWindow();
                if (hwnd == IntPtr.Zero) return;

                ForceForeground(hwnd);
                Thread.Sleep(50);

                // Open chat
                PressSc(VK_RETURN);
                Thread.Sleep(50);

                // Type the command
                SendTextSc(command);
                Thread.Sleep(50);

                // Submit
                PressSc(VK_RETURN);
            });
        }
    }
}
