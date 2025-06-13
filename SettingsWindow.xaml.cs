using System.Windows;
using System.Windows.Input;

namespace overlayc
{
    public partial class SettingsWindow : Window
    {
        private SettingsData settings;

        public SettingsWindow()
        {
            InitializeComponent();

            settings = SettingsManager.Load("settings.json")
                       ?? new SettingsData { HorizontalMode = true };
            settings.Favorites ??= new();

            HorizontalModeCheckbox.IsChecked = settings.HorizontalMode;
            InvertButtonsCheckbox.IsChecked  = settings.InvertButtons;

            HorizontalModeCheckbox.Checked   += OnToggle;
            HorizontalModeCheckbox.Unchecked += OnToggle;
            InvertButtonsCheckbox.Checked    += OnToggle;
            InvertButtonsCheckbox.Unchecked  += OnToggle;

            EditCommandsButton.Click         += (_,__) =>
                (Owner as MainWindow)?.OpenEditCommands();

            CloseButton.Click                += (_,__) => Close();
            CloseX.Click                     += (_,__) => Close();
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            settings.HorizontalMode = HorizontalModeCheckbox.IsChecked == true;
            settings.InvertButtons  = InvertButtonsCheckbox.IsChecked  == true;
            SettingsManager.Save("settings.json", settings);
            if (Owner is MainWindow mw)
                mw.ApplySettings(settings.HorizontalMode, settings.InvertButtons);
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
