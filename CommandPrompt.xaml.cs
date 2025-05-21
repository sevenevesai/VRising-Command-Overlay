using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace overlayc
{
    public partial class CommandPrompt : Window
    {
        // ─── Win32 for window styles & focus ─────────────────────────────────
        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        // ─── Win32 to get cursor position ──────────────────────────────────────
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private readonly Command cmd;
        private readonly List<string> values = new();
        private int index = 0;

        private IntPtr hwnd;
        private int oldExStyle;

        public string? FilledCommand { get; private set; }

        public CommandPrompt(Command cmd)
        {
            InitializeComponent();
            this.cmd = cmd;

            // Position the prompt under the cursor
            if (GetCursorPos(out POINT pt))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = pt.X - 50;
                Top  = pt.Y - 45;
            }

            // When loaded, clear NOACTIVATE so we can take focus
            Loaded += OnLoaded;
            // When closing, restore the original style
            Closing += OnClosing;

            // Cancel button (and Esc via IsCancel="True")
            CancelButton.Click += (_, __) => DialogResult = false;

            NextParam();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            hwnd = new WindowInteropHelper(this).Handle;
            oldExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            int newEx = oldExStyle & ~(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            SetWindowLong(hwnd, GWL_EXSTYLE, newEx);

            // Bring to front and activate
            SetForegroundWindow(hwnd);
            Activate();

            // Focus whichever control is current
            FocusCurrentControl();

            Loaded -= OnLoaded;
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (hwnd != IntPtr.Zero)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, oldExStyle);
            }
        }

        private void NextParam()
        {
            // Unsubscribe old handlers
            ComboOptions.SelectionChanged -= OnComboSelectionChanged;
            SendButton.Click            -= OnSendButtonClick;
            ParamInputBox.KeyDown       -= ParamInputBox_KeyDown;

            // If finished all params, build the command
            if (index >= cmd.@params.Count)
            {
                bool isBl = cmd.template.StartsWith(".bl cst", StringComparison.Ordinal);
                bool isWp = cmd.template.StartsWith(".wep cst", StringComparison.Ordinal);

                string result = cmd.template;
                for (int i = 0; i < cmd.@params.Count; i++)
                {
                    string tag = cmd.@params[i];
                    string val = values[i];
                    string rep;

                    if (isBl && tag == "BloodStat" && cmd.options.TryGetValue(tag, out var blOpts))
                    {
                        int idx = blOpts.IndexOf(val);
                        rep = (idx >= 0 ? idx + 1 : 1).ToString();
                    }
                    else if (isWp && tag == "WeaponStat" && cmd.options.TryGetValue(tag, out var wpOpts))
                    {
                        int idx = wpOpts.IndexOf(val);
                        rep = (idx >= 0 ? idx + 1 : 1).ToString();
                    }
                    else
                    {
                        rep = val;
                    }

                    result = Regex.Replace(
                        result,
                        $@"\[{Regex.Escape(tag)}\]",
                        rep,
                        RegexOptions.None,
                        TimeSpan.FromMilliseconds(50)
                    );
                }

                FilledCommand = result;
                DialogResult   = true;
                return;
            }

            // Prompt next parameter
            string paramName = cmd.@params[index];
            PromptLabel.Text = $"{paramName}:";

            if (cmd.options?.TryGetValue(paramName, out var opts) == true && opts.Count > 0)
            {
                // ComboBox mode
                ComboOptions.ItemsSource = opts;
                ComboOptions.Visibility  = Visibility.Visible;
                ParamInputBox.Visibility = Visibility.Collapsed;
                SendButton.Visibility    = Visibility.Collapsed;
                SendButton.IsDefault     = false;

                ComboOptions.SelectionChanged += OnComboSelectionChanged;

                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        ComboOptions.Focus();
                        Keyboard.Focus(ComboOptions);
                    }),
                    DispatcherPriority.Input
                );
            }
            else
            {
                // Free‐text mode
                ComboOptions.Visibility  = Visibility.Collapsed;
                ParamInputBox.Visibility = Visibility.Visible;
                SendButton.Visibility    = Visibility.Visible;
                SendButton.IsDefault     = true;

                SendButton.Click += OnSendButtonClick;
                ParamInputBox.KeyDown += ParamInputBox_KeyDown;

                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        ParamInputBox.Focus();
                        ParamInputBox.SelectAll();
                        Keyboard.Focus(ParamInputBox);
                    }),
                    DispatcherPriority.Input
                );
            }

            // Ensure window is active so default/cancel buttons work
            Activate();
        }

        private void OnComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ComboOptions.SelectedItem is string sel)
            {
                values.Add(sel);
                index++;
                NextParam();
            }
        }

        private void OnSendButtonClick(object? sender, RoutedEventArgs e)
        {
            string txt = ParamInputBox.Text.Trim();
            if (!string.IsNullOrEmpty(txt))
            {
                values.Add(txt);
                index++;
                NextParam();
            }
        }

        private void ParamInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OnSendButtonClick(sender, new RoutedEventArgs());
            }
        }

        private void FocusCurrentControl()
        {
            if (ComboOptions.Visibility == Visibility.Visible)
            {
                ComboOptions.Focus();
                Keyboard.Focus(ComboOptions);
            }
            else
            {
                ParamInputBox.Focus();
                ParamInputBox.SelectAll();
                Keyboard.Focus(ParamInputBox);
            }
        }
    }
}
