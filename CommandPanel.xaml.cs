using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace overlayc
{
    public partial class CommandPanel : Window
    {
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private readonly Dictionary<string, Dictionary<string, List<Command>>> commandsData;
        public     string? CurrentCategory { get; private set; }

        public CommandPanel(Dictionary<string, Dictionary<string, List<Command>>> commandsData)
        {
            InitializeComponent();

            // Non-activating style
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            int ex  = GetWindowLong(hwnd, GWL_EXSTYLE);
            ex |= (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex);

            this.commandsData = commandsData;
            ShowActivated = false;
            Hide();

            // Make panel draggable
            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };
        }

        public void ShowCommands(string category, Window owner)
        {
            CurrentCategory = category;
            CommandStack.Children.Clear();

            foreach (var grp in commandsData[category])
            {
                // Header
                var header = new TextBlock
                {
                    Text       = grp.Key,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Margin     = new Thickness(0, 10, 0, 4)
                };
                CommandStack.Children.Add(header);

                foreach (var cmd in grp.Value)
                {
                    var btn = new Button
                    {
                        Content    = cmd.label,
                        ToolTip    = cmd.description,
                        Margin     = new Thickness(0, 2, 0, 2),
                        Background = Brushes.Gray,
                        Foreground = Brushes.White
                    };
                    btn.Click += (_, __) => ExecuteCommandSafely(cmd);
                    CommandStack.Children.Add(btn);
                }
            }

            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Reposition(owner);
            Show();
        }

        public void Reposition(Window owner)
        {
            var barPos = owner.PointToScreen(new Point(0, 0));
            Left = barPos.X + owner.Width;
            Top  = barPos.Y;
        }

        private void ExecuteCommandSafely(Command cmd)
        {
            try
            {
                if (cmd.@params.Count == 0)
                {
                    if (!string.IsNullOrEmpty(cmd.template))
                        KeyInjector.SendCommand(cmd.template);
                }
                else
                {
                    var prompt = new CommandPrompt(cmd) { Owner = this };
                    bool? result = prompt.ShowDialog();
                    if (result == true && !string.IsNullOrEmpty(prompt.FilledCommand))
                        KeyInjector.SendCommand(prompt.FilledCommand);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending command:\n{ex.Message}", "Overlay Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
