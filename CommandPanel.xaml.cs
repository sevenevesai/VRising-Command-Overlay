using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace overlayc
{
    public partial class CommandPanel : Window
    {
        // ─── Win32 No-Activate Window Styles ───────────────────────────────────
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private readonly Dictionary<string, Dictionary<string, List<Command>>> commandsData;

        public string? CurrentCategory { get; private set; }

        public CommandPanel(Dictionary<string, Dictionary<string, List<Command>>> commandsData)
        {
            InitializeComponent();
            DataContext   = this;
            ShowActivated = false;
            Hide();

            this.commandsData = commandsData;

            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            int ex   = GetWindowLong(hwnd, GWL_EXSTYLE);
            ex |= (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex);
        }

        /// <summary>
        /// Quickly blanks the list, then swaps in the real items off-screen before showing.
        /// This avoids any flash of old content while remaining lightweight.
        /// </summary>
        public void ShowCommands(string category, Window owner)
        {
            CurrentCategory = category;

            // Capitalize first letter of category for the header
            var displayCategory = !string.IsNullOrEmpty(category)
                ? char.ToUpper(category[0]) + category.Substring(1)
                : category;

            Title = $"{displayCategory} Commands";
            Owner = owner;

            // 1) Prime with an empty list and render immediately
            CommandList.ItemsSource = Array.Empty<object>();
            CommandList.Dispatcher.Invoke(
                () => CommandList.UpdateLayout(),
                System.Windows.Threading.DispatcherPriority.Render);

            // 2) Build the real items
            var newItems = new List<Command>();
            foreach (var grp in commandsData[category])
                foreach (var cmd in grp.Value)
                    newItems.Add(cmd);

            newItems = newItems
                .OrderByDescending(c => c.isStarred)
                .ThenBy(c => c.label)
                .ToList();

            // 3) Off-screen swap
            Visibility = Visibility.Hidden;
            CommandList.ItemsSource = newItems;
            CommandList.Dispatcher.Invoke(
                () => CommandList.UpdateLayout(),
                System.Windows.Threading.DispatcherPriority.Render);

            // 4) Reposition and show
            Reposition(owner);
            Visibility = Visibility.Visible;
        }

        public void HideCommands()
        {
            CurrentCategory = null;
            Visibility      = Visibility.Hidden;
        }

        public void Reposition(Window owner)
        {
            const double gutter = 4;
            if (owner is MainWindow mw)
            {
                var iconPt = mw.IconBarPanel.PointToScreen(new System.Windows.Point(0, 0));
                Left = iconPt.X + mw.IconBarPanel.ActualWidth + gutter;
                Top  = iconPt.Y;
            }
            else
            {
                var screenPt = owner.PointToScreen(new System.Windows.Point(0, 0));
                Left = screenPt.X + owner.Width + gutter;
                Top  = screenPt.Y;
            }
        }

        private void OnCommandClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Command cmd)
            {
                try
                {
                    if (cmd.@params == null || cmd.@params.Count == 0)
                        KeyInjector.SendCommand(cmd.template);
                    else
                    {
                        var prompt = new CommandPrompt(cmd) { Owner = this };
                        if (prompt.ShowDialog() == true)
                            KeyInjector.SendCommand(prompt.FilledCommand!);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error sending command:\n{ex.Message}",
                        "Overlay Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void OnCommandRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Command cmd)
            {
                cmd.isStarred = !cmd.isStarred;
                if (Owner is MainWindow mw)
                    mw.UpdateFavorite(cmd);

                ResortCurrentList();
            }
        }

        private void ResortCurrentList()
        {
            if (CurrentCategory == null) return;
            var sorted = CommandList.Items.Cast<Command>()
                .OrderByDescending(c => c.isStarred)
                .ThenBy(c => c.label)
                .ToList();
            CommandList.ItemsSource = sorted;
        }
    }
}