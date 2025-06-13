using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using overlayc.Input;

namespace overlayc
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")] 
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] 
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const string SettingsFileName = "settings.json";

        private readonly SettingsData settings;
        private readonly DispatcherTimer _saveTimer;

        private SettingsWindow? _settingsWin;
        private bool isHorizontalMode;
        private bool isButtonsInverted;

        private Dictionary<string, Dictionary<string, List<Command>>> commandsData;
        private CommandPanel cmdPanel;
        private KeyboardHook? keyHook;
        private readonly Dictionary<string, Button> iconButtons = new();

        private bool   isDragging;
        private Point  mouseStartScreen;
        private double windowStartLeft, windowStartTop;

        public MainWindow()
        {
            InitializeComponent();

            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            var ex   = GetWindowLong(hwnd, GWL_EXSTYLE);
            ex |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, ex);

            settings = SettingsManager.Load(SettingsFileName)
                       ?? new SettingsData { HorizontalMode = true };
            settings.Favorites ??= new();
            if (settings.WindowLeft.HasValue) Left = settings.WindowLeft.Value;
            if (settings.WindowTop .HasValue) Top  = settings.WindowTop.Value;

            isHorizontalMode  = settings.HorizontalMode;
            isButtonsInverted = settings.InvertButtons;

            _saveTimer = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _saveTimer.Tick += (_,__) => {
                _saveTimer.Stop();
                settings.WindowLeft     = Left;
                settings.WindowTop      = Top;
                settings.HorizontalMode = isHorizontalMode;
                settings.InvertButtons  = isButtonsInverted;
                SettingsManager.Save(SettingsFileName, settings);
            };

            LocationChanged += (_,__) => {
                _saveTimer.Stop();
                _saveTimer.Start();
                if (cmdPanel?.IsVisible == true)
                    PositionCommandWindow();
            };

            keyHook = new KeyboardHook();
            keyHook.OnKeyPressed += vk => {
                const int VK_TAB    = 0x09;
                const int VK_ESCAPE = 0x1B;
                if (vk == VK_TAB || vk == VK_ESCAPE)
                    Dispatcher.BeginInvoke(
                        new Action(CloseCommands),
                        DispatcherPriority.Normal);
            };

            string cmdFile = settings.CommandPreset ?? "commands.json";
            commandsData = CommandLoader.LoadCommands(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cmdFile));
            ApplyFavoriteFlags();
            cmdPanel = new CommandPanel(commandsData);

            PreviewMouseLeftButtonDown += Window_PreviewMouseLeftButtonDown;
            PreviewMouseMove          += Window_PreviewMouseMove;
            PreviewMouseLeftButtonUp  += Window_PreviewMouseLeftButtonUp;

            PopulateIcons();
            HighlightIcon(null);

            ApplySettings(isHorizontalMode, isButtonsInverted);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _saveTimer.Stop();
            settings.WindowLeft     = Left;
            settings.WindowTop      = Top;
            settings.HorizontalMode = isHorizontalMode;
            settings.InvertButtons  = isButtonsInverted;
            SettingsManager.Save(SettingsFileName, settings);

            keyHook?.Dispose();
            cmdPanel?.Close();
            foreach (Window w in OwnedWindows) w.Close();
            Application.Current.Shutdown();
            base.OnClosing(e);
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            var pos   = e.GetPosition(this);
            mouseStartScreen = PointToScreen(pos);
            windowStartLeft  = Left;
            windowStartTop   = Top;
        }

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;
            var screen = PointToScreen(e.GetPosition(this));
            Left = windowStartLeft + (screen.X - mouseStartScreen.X);
            Top  = windowStartTop  + (screen.Y - mouseStartScreen.Y);
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }

        private void PopulateIcons()
        {
            var imgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "imgs");
            foreach (var cat in commandsData.Keys)
            {
                var btn = new Button {
                    Width           = 64,
                    Height          = 64,
                    Margin          = new Thickness(2),
                    Background      = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    ToolTip         = cat,
                    Focusable       = false
                };

                var imgPath = Path.Combine(imgDir, $"{cat}.png");
                if (File.Exists(imgPath))
                    btn.Content = new Image {
                        Source  = new BitmapImage(new Uri(imgPath)),
                        Stretch = Stretch.Uniform
                    };
                else
                    btn.Content = cat.Substring(0,1).ToUpper();

                btn.Click += (_,__) => {
                    if (cmdPanel.IsVisible && cmdPanel.CurrentCategory == cat)
                    {
                        cmdPanel.Hide();
                        HighlightIcon(null);
                    }
                    else
                    {
                        cmdPanel.ShowCommands(cat, this);
                        PositionCommandWindow();
                        HighlightIcon(cat);
                    }
                };

                IconBarPanel.Children.Add(btn);
                iconButtons[cat] = btn;
            }
        }

        private void HighlightIcon(string? sel)
        {
            var hb = new SolidColorBrush(Color.FromArgb(200, 0, 200, 255));
            foreach (var kv in iconButtons)
            {
                kv.Value.BorderThickness = kv.Key == sel ? new Thickness(2) : new Thickness(0);
                kv.Value.BorderBrush     = kv.Key == sel ? hb : null!;
            }
        }

        private void CloseCommands()
        {
            if (cmdPanel.IsVisible)
            {
                cmdPanel.Hide();
                HighlightIcon(null);
            }

            foreach (Window w in OwnedWindows)
                if (w != _settingsWin && w != cmdPanel)
                    w.Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWin?.IsVisible != true)
            {
                _settingsWin = new SettingsWindow { Owner = this };
                _settingsWin.Closed += (_,__) => _settingsWin = null;
                _settingsWin.Show();
            }
            else
            {
                _settingsWin.Activate();
            }
        }

        public void ApplySettings(bool horizontalMode, bool invertButtons)
        {
            isHorizontalMode  = horizontalMode;
            isButtonsInverted = invertButtons;

            IconBarPanel.Orientation = horizontalMode
                ? Orientation.Horizontal
                : Orientation.Vertical;

            // reorder the Settings/Close row:
            RootPanel.Children.Clear();
            if (invertButtons)
            {
                RootPanel.Children.Add(StaticButtonsPanel);
                RootPanel.Children.Add(IconBarPanel);
            }
            else
            {
                RootPanel.Children.Add(IconBarPanel);
                RootPanel.Children.Add(StaticButtonsPanel);
            }
        }

        public void OpenEditCommands()
        {
            var editor = new CommandEditorWindow(settings.CommandPreset ?? "commands.json")
            {
                Owner = this
            };
            editor.CommandsSaved += file =>
            {
                settings.CommandPreset = file;
                SettingsManager.Save(SettingsFileName, settings);
                ReloadCommands(file);
            };
            editor.ShowDialog();
        }

        private void ReloadCommands(string file)
        {
            commandsData = CommandLoader.LoadCommands(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file));
            ApplyFavoriteFlags();
            cmdPanel.Close();
            cmdPanel = new CommandPanel(commandsData);
            IconBarPanel.Children.Clear();
            iconButtons.Clear();
            PopulateIcons();
            HighlightIcon(null);
        }

        private void PositionCommandWindow()
        {
            if (!cmdPanel.IsVisible) return;
            var wa = SystemParameters.WorkArea;
            var pt = IconBarPanel.TransformToAncestor(this).Transform(new Point(0,0));
            double iconLeft  = Left + pt.X;
            double iconTop   = Top  + pt.Y;
            double iconW     = IconBarPanel.ActualWidth;
            double iconH     = IconBarPanel.ActualHeight;
            double x, y;
            bool vertical = !isHorizontalMode;

            if (vertical)
            {
                if (iconLeft + iconW/2 > wa.Left + wa.Width/2)
                    x = iconLeft - cmdPanel.Width;
                else
                    x = iconLeft + iconW;
                y = iconTop;
            }
            else
            {
                x = iconLeft + (iconW - cmdPanel.Width)/2;
                if (iconTop + iconH/2 > wa.Top + wa.Height/2)
                    y = iconTop - cmdPanel.Height;
                else
                    y = iconTop + iconH;
            }

            x = Math.Min(Math.Max(wa.Left,    x), wa.Right  - cmdPanel.Width);
            y = Math.Min(Math.Max(wa.Top,     y), wa.Bottom - cmdPanel.Height);

            cmdPanel.Left = x;
            cmdPanel.Top  = y;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ApplyFavoriteFlags()
        {
            foreach (var cat in commandsData.Values)
                foreach (var grp in cat.Values)
                    foreach (var cmd in grp)
                        cmd.isStarred = settings.Favorites.Contains(cmd.template);
        }

        public void UpdateFavorite(Command cmd)
        {
            if (cmd.isStarred)
                settings.Favorites.Add(cmd.template);
            else
                settings.Favorites.Remove(cmd.template);
            SettingsManager.Save(SettingsFileName, settings);
        }
    }
}
