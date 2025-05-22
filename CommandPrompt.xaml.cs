using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        private readonly Command _cmd;
        private readonly List<string> _values = new();
        private readonly Regex[] _paramRegexes;
        private int _index = 0;

        private IntPtr _hwnd;
        private int _oldExStyle;

        public string? FilledCommand { get; private set; }

        public CommandPrompt(Command cmd)
        {
            InitializeComponent();
            _cmd = cmd;

            // Pre-compile one Regex per param tag
            _paramRegexes = (cmd.@params ?? new List<string>())
                .Select(tag => new Regex($@"\[{Regex.Escape(tag)}\]", RegexOptions.Compiled | RegexOptions.CultureInvariant))
                .ToArray();

            // Position the prompt under the cursor
            if (GetCursorPos(out POINT pt))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = pt.X - 50;
                Top  = pt.Y - 45;
            }

            // When loaded, clear NOACTIVATE so we can take focus
            Loaded  += OnLoaded;
            Closing += OnClosing;

            // Cancel button (and Esc via IsCancel="True")
            CancelButton.Click += (_,__) => DialogResult = false;

            NextParam();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd       = new WindowInteropHelper(this).Handle;
            _oldExStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            int newEx   = _oldExStyle & ~(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            SetWindowLong(_hwnd, GWL_EXSTYLE, newEx);

            // Bring to front and activate
            SetForegroundWindow(_hwnd);
            Activate();

            // Focus the first control
            FocusCurrentControl();

            Loaded -= OnLoaded;
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_hwnd != IntPtr.Zero)
                SetWindowLong(_hwnd, GWL_EXSTYLE, _oldExStyle);
        }

        private void NextParam()
        {
            // Unhook old handlers
            ComboOptions.SelectionChanged -= OnComboSelectionChanged;
            SendButton.Click            -= OnSendButtonClick;
            ParamInputBox.KeyDown       -= ParamInputBox_KeyDown;

            // All parameters collected?  Build the final command
            if (_index >= (_cmd.@params?.Count ?? 0))
            {
                // One fast, cached-regex pass
                string result = _cmd.template ?? string.Empty;
                for (int i = 0; i < (_cmd.@params?.Count ?? 0); i++)
                {
                    string tag = _cmd.@params![i];
                    string val = _values[i];
                    string rep = val;  // default

                    // special .bl/.wep index logic, now with safety checks
                    if (!string.IsNullOrEmpty(_cmd.template) && _cmd.@params != null)
                    {
                        // blood stat
                        if (_cmd.template.StartsWith(".bl cst", StringComparison.Ordinal) &&
                            tag == "BloodStat" &&
                            _cmd.options != null &&
                            _cmd.options.TryGetValue(tag, out var blOpts) &&
                            blOpts != null &&
                            blOpts.Count > 0)
                        {
                            int idx = blOpts.IndexOf(val);
                            if (idx >= 0)
                                rep = (idx + 1).ToString();
                        }
                        // weapon stat
                        else if (_cmd.template.StartsWith(".wep cst", StringComparison.Ordinal) &&
                                 tag == "WeaponStat" &&
                                 _cmd.options != null &&
                                 _cmd.options.TryGetValue(tag, out var wpOpts) &&
                                 wpOpts != null &&
                                 wpOpts.Count > 0)
                        {
                            int idx = wpOpts.IndexOf(val);
                            if (idx >= 0)
                                rep = (idx + 1).ToString();
                        }
                    }

                    // use pre-compiled regex
                    result = _paramRegexes[i].Replace(result, rep);
                }

                FilledCommand = result;
                DialogResult   = true;
                return;
            }

            // Otherwise prompt for the next one…
            string paramName = _cmd.@params![_index];
            PromptLabel.Text = $"{paramName}:";

            if (_cmd.options?.TryGetValue(paramName, out var opts) == true && opts.Count > 0)
            {
                // ComboBox mode
                ComboOptions.ItemsSource = opts;
                ComboOptions.Visibility  = Visibility.Visible;
                ParamInputBox.Visibility = Visibility.Collapsed;
                SendButton.Visibility    = Visibility.Collapsed;
                SendButton.IsDefault     = false;

                ComboOptions.SelectionChanged += OnComboSelectionChanged;
            }
            else
            {
                // Free-text mode
                ComboOptions.Visibility  = Visibility.Collapsed;
                ParamInputBox.Visibility = Visibility.Visible;
                SendButton.Visibility    = Visibility.Visible;
                SendButton.IsDefault     = true;

                SendButton.Click      += OnSendButtonClick;
                ParamInputBox.KeyDown += ParamInputBox_KeyDown;
            }

            // Focus the active control on the input dispatcher queue
            Dispatcher.BeginInvoke(
                (Action)(() => FocusCurrentControl()),
                DispatcherPriority.Input);
            Activate();
        }

        private void OnComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ComboOptions.SelectedItem is string sel)
            {
                _values.Add(sel);
                _index++;
                NextParam();
            }
        }

        private void OnSendButtonClick(object? sender, RoutedEventArgs e)
        {
            string txt = ParamInputBox.Text.Trim();
            if (!string.IsNullOrEmpty(txt))
            {
                _values.Add(txt);
                _index++;
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
                ComboOptions.Focus();
            else
            {
                ParamInputBox.Focus();
                ParamInputBox.SelectAll();
            }
            Keyboard.Focus(
                ComboOptions.Visibility == Visibility.Visible
                ? (IInputElement)ComboOptions
                : ParamInputBox);
        }
    }
}
