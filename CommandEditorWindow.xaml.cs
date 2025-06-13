using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
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
            DropdownCheck.Checked      += DropdownCheck_Changed;
            DropdownCheck.Unchecked    += DropdownCheck_Changed;
            DropdownOptionsBox.TextChanged += DropdownOptionsBox_TextChanged;
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
                var catItem = CreateItem(cat.Key, null);
                foreach (var grp in cat.Value)
                {
                    var grpItem = CreateItem(grp.Key, null);
                    foreach (var cmd in grp.Value)
                    {
                        var cmdItem = CreateItem(cmd.label, cmd);
                        grpItem.Items.Add(cmdItem);
                    }
                    catItem.Items.Add(grpItem);
                }
                CommandTree.Items.Add(catItem);
            }
        }

        private TreeViewItem CreateItem(string header, object? tag)
        {
            var item = new TreeViewItem { Header = header, Tag = tag };
            var menu = new ContextMenu();
            var rename = new MenuItem { Header = "Rename" };
            rename.Click += RenameItem_Click;
            var del = new MenuItem { Header = "Delete" };
            del.Click += DeleteItem_Click;
            menu.Items.Add(rename);
            menu.Items.Add(del);
            item.ContextMenu = menu;
            return item;
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

                if (cmd.@params != null && cmd.@params.Count > 0)
                {
                    string p = cmd.@params[0];
                    ParamNameLabel.Text = $"Param: [{p}]";
                    ParamOptionsPanel.Visibility = Visibility.Visible;
                    if (cmd.options != null && cmd.options.TryGetValue(p, out var opts) && opts.Count > 0)
                    {
                        DropdownCheck.IsChecked = true;
                        DropdownOptionsBox.Text = string.Join(", ", opts);
                    }
                    else
                    {
                        DropdownCheck.IsChecked = false;
                        DropdownOptionsBox.Text = string.Empty;
                    }
                }
                else
                {
                    ParamOptionsPanel.Visibility = Visibility.Collapsed;
                }
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
            // Stay open after saving
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

        // ─── Dropdown param editing ─────────────────────────────────────────
        private void DropdownCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (selectedCommand == null || selectedCommand.@params == null || selectedCommand.@params.Count == 0)
                return;
            string p = selectedCommand.@params[0];

            if (DropdownCheck.IsChecked == true)
            {
                selectedCommand.options ??= new();
                selectedCommand.options[p] = ParseOptions(DropdownOptionsBox.Text);
            }
            else
            {
                selectedCommand.options?.Remove(p);
            }
        }

        private void DropdownOptionsBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DropdownCheck.IsChecked != true || selectedCommand == null || selectedCommand.@params == null || selectedCommand.@params.Count == 0)
                return;
            string p = selectedCommand.@params[0];
            selectedCommand.options ??= new();
            selectedCommand.options[p] = ParseOptions(DropdownOptionsBox.Text);
        }

        private static List<string> ParseOptions(string text)
        {
            return text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => s.Length > 0)
                       .ToList();
        }

        // ─── Context menu actions ───────────────────────────────────────────
        private void RenameItem_Click(object sender, RoutedEventArgs e)
        {
            if (CommandTree.SelectedItem is not TreeViewItem item) return;

            string current = item.Header?.ToString() ?? string.Empty;
            string input = Interaction.InputBox("Enter new name:", "Rename", current);
            if (string.IsNullOrWhiteSpace(input) || input == current) return;

            if (item.Tag is Command cmd)
            {
                cmd.label = input;
                item.Header = input;
            }
            else if (GetParent(item) is TreeViewItem parent)
            {
                // group rename
                string cat = parent.Header.ToString()!;
                var grpList = commandsData[cat][current];
                commandsData[cat].Remove(current);
                commandsData[cat][input] = grpList;
            }
            else
            {
                // category rename
                var val = commandsData[current];
                commandsData.Remove(current);
                commandsData[input] = val;
            }

            BuildTree();
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (CommandTree.SelectedItem is not TreeViewItem item) return;

            if (item.Tag is Command cmd)
            {
                foreach (var cat in commandsData.Values)
                    foreach (var grp in cat.Values)
                        if (grp.Remove(cmd)) break;
            }
            else if (GetParent(item) is TreeViewItem parent)
            {
                // delete group
                string catName = parent.Header.ToString()!;
                string grpName = item.Header.ToString()!;
                commandsData[catName].Remove(grpName);
            }
            else
            {
                // delete category
                string catName = item.Header.ToString()!;
                commandsData.Remove(catName);
            }

            BuildTree();
            CommandEditPanel.Visibility = Visibility.Collapsed;
        }

        private static TreeViewItem? GetParent(TreeViewItem item)
        {
            return ItemsControl.ItemsControlFromItemContainer(item) as TreeViewItem;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
