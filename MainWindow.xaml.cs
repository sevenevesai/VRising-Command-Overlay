using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace overlayc
{
    public partial class MainWindow : Window
    {
        // ─── Win32 No-Activate Window Styles ─────────────────────────────────────────
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        // ─── Fields ─────────────────────────────────────────────────────────────────
        private readonly Dictionary<string, Dictionary<string, List<Command>>> commandsData;
        private readonly CommandPanel cmdPanel;
        private readonly KeyboardHook  keyHook;
        private readonly Dictionary<string, Button> iconButtons = new();

        // ─── Manual drag state ───────────────────────────────────────────────────────
        private bool  isDragging;
        private Point mouseStartScreen;
        private double windowStartLeft, windowStartTop;

        public MainWindow()
        {
            InitializeComponent();

            // Prevent stealing focus
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            ex |= (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex);

            // Hook Tab-to-close
            keyHook = new KeyboardHook();
            keyHook.OnKeyPressed += vk =>
            {
                const int VK_TAB = 0x09;
                if (vk == VK_TAB)
                    Dispatcher.Invoke(CloseAll);
            };

            // Load commands.json and panel
            commandsData = CommandLoader.LoadCommands(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "commands.json"));
            cmdPanel = new CommandPanel(commandsData);

            // Wire up Preview events for dragging
            PreviewMouseLeftButtonDown += Window_PreviewMouseLeftButtonDown;
            PreviewMouseMove           += Window_PreviewMouseMove;
            PreviewMouseLeftButtonUp   += Window_PreviewMouseLeftButtonUp;

            PopulateIcons();

            // Keep panel aligned
            LocationChanged += (_, __) =>
            {
                if (cmdPanel.IsVisible && cmdPanel.CurrentCategory != null)
                    cmdPanel.Reposition(this);
            };

            HighlightIcon(null);
        }

        // ─── Preview Mouse Handlers ─────────────────────────────────────────────────
        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Begin drag
            isDragging = true;
            var pos = e.GetPosition(this);
            // Convert to screen coords
            var screen = PointToScreen(pos);
            mouseStartScreen = new Point(screen.X, screen.Y);
            windowStartLeft  = Left;
            windowStartTop   = Top;
        }

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;
            var pos = e.GetPosition(this);
            var screen = PointToScreen(pos);
            // Compute delta
            var dx = screen.X - mouseStartScreen.X;
            var dy = screen.Y - mouseStartScreen.Y;
            Left = windowStartLeft + dx;
            Top  = windowStartTop  + dy;
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }

        // ─── Rest of your MainWindow logic ──────────────────────────────────────────

        private void PopulateIcons()
        {
            var imgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "imgs");
            foreach (var cat in commandsData.Keys)
            {
                var btn = new Button {
                    Width=64, Height=64, Margin=new Thickness(2),
                    Background=Brushes.Transparent, BorderThickness=new Thickness(0),
                    ToolTip=cat
                };
                var imgPath = Path.Combine(imgDir, $"{cat}.png");
                if (File.Exists(imgPath))
                    btn.Content = new Image { Source=new BitmapImage(new Uri(imgPath)), Stretch=Stretch.Uniform };
                else
                    btn.Content = cat.Substring(0,1).ToUpper();

                string category = cat;
                btn.Click += (_, __) => {
                    if (cmdPanel.IsVisible && cmdPanel.CurrentCategory==category) {
                        cmdPanel.Hide(); HighlightIcon(null);
                    } else {
                        cmdPanel.ShowCommands(category, this); HighlightIcon(category);
                    }
                };

                IconBarPanel.Children.Add(btn);
                iconButtons[cat]=btn;
            }
            var closeBtn = new Button {
                Content="✕", Width=64, Height=64, Margin=new Thickness(2),
                Background=Brushes.Transparent, BorderThickness=new Thickness(0),
                ToolTip="Close Overlay"
            };
            closeBtn.Click += (_, __) => Application.Current.Shutdown();
            IconBarPanel.Children.Add(closeBtn);
        }

        private void HighlightIcon(string? sel) {
            var brush=new SolidColorBrush(Color.FromArgb(200,0,200,255));
            foreach(var kv in iconButtons) {
                if(kv.Key==sel) {
                    kv.Value.BorderBrush=brush; kv.Value.BorderThickness=new Thickness(2);
                } else {
                    kv.Value.BorderThickness=new Thickness(0);
                }
            }
        }

        private void CloseAll() {
            cmdPanel.Hide(); HighlightIcon(null);
            foreach(var w in OwnedWindows) if(w is CommandPrompt cp) cp.Close();
        }
    }
}
