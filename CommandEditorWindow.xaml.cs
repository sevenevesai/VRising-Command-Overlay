using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;

namespace overlayc
{
    public partial class CommandEditorWindow : Window
    {
        private readonly string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        private Dictionary<string, Dictionary<string, List<Command>>> commandsData = new();
        private string currentFile;
        private Command? selectedCommand;

        public event Action<string>? CommandsSaved;

        public CommandEditorWindow(string presetFile)
        {
            InitializeComponent();
            currentFile = presetFile;

            PresetDropdown.SelectionChanged += PresetDropdown_SelectionChanged;
            CommandTree.SelectedItemChanged += OnSelectedItemChanged;
            CmdLabelBox.TextChanged += CmdLabelBox_TextChanged;
            CmdTemplateBox.TextChanged += CmdTemplateBox_TextChanged;
            SaveButton.Click += OnSave;
            SaveAsButton.Click += OnSaveAs;

            LoadPresetList();
            LoadPreset(Path.Combine(baseDir, presetFile));
        }

        private void LoadPresetList()
        {
            var files = Directory.GetFiles(baseDir, "commands*.json")
                .Select(Path.GetFileName)
                .OrderBy(f => f)
                .ToList();
            if (!files.Contains(currentFile))
                files.Insert(0, currentFile);
            PresetDropdown.ItemsSource = files;
            PresetDropdown.SelectedItem = currentFile;
        }

        private void LoadPreset(string path)
        {
            commandsData = CommandLoader.LoadCommands(path);
            BuildTree();
        }

        private void BuildTree()
        {
            CommandTree.Items.Clear();
            foreach (var cat in commandsData)
            {
                var catItem = new TreeViewItem { Header = cat.Key };
                foreach (var grp in cat.Value)
                {
                    var grpItem = new TreeViewItem { Header = grp.Key };
                    foreach (var cmd in grp.Value)
                    {
                        var cmdItem = new TreeViewItem { Header = cmd.label, Tag = cmd };
                        grpItem.Items.Add(cmdItem);
                    }
                    catItem.Items.Add(grpItem);
                }
                CommandTree.Items.Add(catItem);
            }
        }

        private void PresetDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetDropdown.SelectedItem is string file)
            {
                currentFile = file;
                LoadPreset(Path.Combine(baseDir, file));
            }
        }

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            CommandEditPanel.Visibility = Visibility.Collapsed;
            selectedCommand = null;
            if (CommandTree.SelectedItem is TreeViewItem tv && tv.Tag is Command cmd)
            {
                selectedCommand = cmd;
                CommandEditPanel.Visibility = Visibility.Visible;
                CmdLabelBox.Text = cmd.label;
                CmdTemplateBox.Text = cmd.template;
            }
        }

        private void CmdLabelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (selectedCommand != null)
            {
                selectedCommand.label = CmdLabelBox.Text;
                if (CommandTree.SelectedItem is TreeViewItem tv)
                    tv.Header = CmdLabelBox.Text;
            }
        }

        private void CmdTemplateBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (selectedCommand != null)
                selectedCommand.template = CmdTemplateBox.Text;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            SaveTo(Path.Combine(baseDir, currentFile));
            CommandsSaved?.Invoke(currentFile);
            Close();
        }

        private void OnSaveAs(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = baseDir,
                FileName = currentFile,
                Filter = "JSON files|*.json"
            };
            if (dlg.ShowDialog() == true)
            {
                currentFile = Path.GetFileName(dlg.FileName);
                SaveTo(dlg.FileName);
                CommandsSaved?.Invoke(currentFile);
                Close();
            }
        }

        private void SaveTo(string path)
        {
            var json = JsonConvert.SerializeObject(commandsData, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
