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
        private readonly Stack<string> undoStack = new();
        private readonly Stack<string> redoStack = new();
        private string currentFile;
        private Command? selectedCommand;
        private bool isLoaded;

        public event Action<string>? CommandsSaved;
        public event Action<string>? PresetChanged;

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
            UndoButton.Click += (_,__) => UndoAction();
            RedoButton.Click += (_,__) => RedoAction();

            LoadPresetList();
            LoadPreset(Path.Combine(baseDir, presetFile));
            UpdateUndoRedoButtons();
            isLoaded = true;
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
            RebuildTree(false);
        }

        private void BuildTree()
        {
            CommandTree.Items.Clear();
            foreach (var cat in commandsData)
            {
                var catItem = new TreeViewItem
                {
                    Header = cat.Key,
                    Tag    = $"cat:{cat.Key}",
                    ContextMenu = BuildContextMenu("cat")
                };
                foreach (var grp in cat.Value)
                {
                    var grpItem = new TreeViewItem
                    {
                        Header = grp.Key,
                        Tag    = $"grp:{cat.Key}/{grp.Key}",
                        ContextMenu = BuildContextMenu("grp")
                    };
                    foreach (var cmd in grp.Value)
                    {
                        var cmdItem = new TreeViewItem
                        {
                            Header = cmd.label,
                            Tag    = cmd,
                            ContextMenu = BuildContextMenu("cmd")
                        };
                        grpItem.Items.Add(cmdItem);
                    }
                    catItem.Items.Add(grpItem);
                }
                CommandTree.Items.Add(catItem);
            }

            CommandTree.ContextMenu = BuildContextMenu("root");
        }

        private void RebuildTree(bool preserveState)
        {
            HashSet<string>? expanded = null;
            object? selected = null;
            if (preserveState)
                (expanded, selected) = CaptureState();

            BuildTree();

            if (preserveState && expanded != null)
                ApplyState(expanded, selected);
            UpdateUndoRedoButtons();
        }

        private (HashSet<string> expanded, object? selected) CaptureState()
        {
            var expanded = new HashSet<string>();
            object? selected = null;

            foreach (TreeViewItem catItem in CommandTree.Items)
            {
                if (catItem.IsExpanded)
                    expanded.Add((string)catItem.Tag);
                foreach (TreeViewItem grpItem in catItem.Items)
                {
                    if (grpItem.IsExpanded)
                        expanded.Add((string)grpItem.Tag);
                    foreach (TreeViewItem cmdItem in grpItem.Items)
                    {
                        if (cmdItem.IsExpanded && cmdItem.Tag is Command c)
                            expanded.Add($"cmd:{c.label}");
                    }
                }
            }

            if (CommandTree.SelectedItem is TreeViewItem sel)
            {
                if (sel.Tag is Command cmd)
                    selected = cmd;
                else
                    selected = sel.Tag;
            }
            return (expanded, selected);
        }

        private void ApplyState(HashSet<string> expanded, object? selected)
        {
            TreeViewItem? toSelect = null;
            foreach (TreeViewItem catItem in CommandTree.Items)
            {
                if (expanded.Contains((string)catItem.Tag))
                    catItem.IsExpanded = true;
                if (selected != null && Equals(selected, catItem.Tag))
                    toSelect = catItem;
                foreach (TreeViewItem grpItem in catItem.Items)
                {
                    if (expanded.Contains((string)grpItem.Tag))
                        grpItem.IsExpanded = true;
                    if (selected != null && Equals(selected, grpItem.Tag))
                        toSelect = grpItem;
                    foreach (TreeViewItem cmdItem in grpItem.Items)
                    {
                        if (cmdItem.Tag is Command c)
                        {
                            if (expanded.Contains($"cmd:{c.label}"))
                                cmdItem.IsExpanded = true;
                            if (selected != null && ReferenceEquals(selected, c))
                                toSelect = cmdItem;
                        }
                    }
                }
            }

            if (toSelect != null)
            {
                toSelect.IsSelected = true;
                toSelect.BringIntoView();
            }
        }

        private void UpdateUndoRedoButtons()
        {
            if (!isLoaded) return;
            UndoButton.IsEnabled = undoStack.Count > 0;
            RedoButton.IsEnabled = redoStack.Count > 0;
        }

        private ContextMenu BuildContextMenu(string level)
        {
            var menu = new ContextMenu();
            menu.Opened += (s, e) =>
            {
                if (menu.PlacementTarget is TreeViewItem t)
                    t.IsSelected = true;
                else if (menu.PlacementTarget == CommandTree)
                    CommandTree.SelectedItem = null;
            };

            if (level != "root")
            {
                var rename = new MenuItem { Header = "Rename" };
                rename.Click += (_, __) => RenameSelected();
                var del = new MenuItem { Header = "Delete" };
                del.Click += (_, __) => DeleteSelected();
                menu.Items.Add(rename);
                menu.Items.Add(del);
            }

            var create = new MenuItem { Header = "Create" };
            create.Click += (_, __) => CreateItem(level);
            menu.Items.Add(create);

            return menu;
        }

        private void PresetDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetDropdown.SelectedItem is string file)
            {
                currentFile = file;
                LoadPreset(Path.Combine(baseDir, file));
                PresetChanged?.Invoke(currentFile);
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

        private void RenameSelected()
        {
            if (CommandTree.SelectedItem is not TreeViewItem tv) return;
            string oldName = tv.Header.ToString() ?? string.Empty;
            var dlg = new InputDialog("Rename", "New name:", oldName) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            string newName = dlg.Input.Trim();
            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            PushUndo();

            if (tv.Tag is Command cmd)
            {
                cmd.label = newName;
                tv.Header = newName;
            }
            else if (tv.Tag is string tag)
            {
                if (tag.StartsWith("grp:"))
                {
                    var parts = tag.Substring(4).Split('/');
                    string cat = parts[0];
                    string grp = parts[1];
                    var groupDict = commandsData[cat];
                    var cmds = groupDict[grp];
                    groupDict.Remove(grp);
                    groupDict[newName] = cmds;
                }
                else if (tag.StartsWith("cat:"))
                {
                    string cat = tag.Substring(4);
                    var groups = commandsData[cat];
                    commandsData.Remove(cat);
                    commandsData[newName] = groups;
                }
            }

            RebuildTree(true);
        }

        private void DeleteSelected()
        {
            if (CommandTree.SelectedItem is not TreeViewItem tv) return;
            PushUndo();
            if (tv.Tag is Command cmd)
            {
                foreach (var cat in commandsData.Values)
                    foreach (var grp in cat.Values)
                        if (grp.Remove(cmd))
                            break;
            }
            else if (tv.Tag is string tag)
            {
                if (tag.StartsWith("grp:"))
                {
                    var parts = tag.Substring(4).Split('/');
                    string cat = parts[0];
                    string grp = parts[1];
                    commandsData[cat].Remove(grp);
                }
                else if (tag.StartsWith("cat:"))
                {
                    string cat = tag.Substring(4);
                    commandsData.Remove(cat);
                }
            }

            RebuildTree(true);
        }

        private void CreateItem(string level)
        {
            var dlg = new InputDialog("Create", "Name:") { Owner = this };
            if (dlg.ShowDialog() != true) return;
            string name = dlg.Input.Trim();
            if (string.IsNullOrEmpty(name)) return;

            PushUndo();

            if (level == "root")
            {
                commandsData[name] = new Dictionary<string, List<Command>>();
            }
            else if (level == "cat" && CommandTree.SelectedItem is TreeViewItem tv)
            {
                string cat = ((string)tv.Tag)[4..];
                commandsData[cat][name] = new List<Command>();
            }
            else if (level == "grp" && CommandTree.SelectedItem is TreeViewItem tv)
            {
                var parts = ((string)tv.Tag)[4..].Split('/');
                string cat = parts[0];
                string grp = parts[1];
                commandsData[cat][grp].Add(new Command { label = name, template = "" });
            }
            else if (level == "cmd" && CommandTree.SelectedItem is TreeViewItem tv && tv.Tag is Command cmd)
            {
                foreach (var cat in commandsData)
                {
                    foreach (var grp in cat.Value)
                    {
                        if (grp.Value.Contains(cmd))
                        {
                            grp.Value.Add(new Command { label = name, template = "" });
                            goto done;
                        }
                    }
                }
            done: ;
            }

            RebuildTree(true);
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            SaveTo(Path.Combine(baseDir, currentFile));
            CommandsSaved?.Invoke(currentFile);
            RebuildTree(true);
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
                LoadPresetList();
                CommandsSaved?.Invoke(currentFile);
                RebuildTree(true);
            }
        }

        private void SaveTo(string path)
        {
            var json = JsonConvert.SerializeObject(commandsData, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        private void PushUndo()
        {
            var json = JsonConvert.SerializeObject(commandsData);
            undoStack.Push(json);
            redoStack.Clear();
            UpdateUndoRedoButtons();
        }

        private void UndoAction()
        {
            if (undoStack.Count == 0) return;
            var json = JsonConvert.SerializeObject(commandsData);
            redoStack.Push(json);
            json = undoStack.Pop();
            commandsData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<Command>>>>(json)!;
            RebuildTree(true);
            UpdateUndoRedoButtons();
        }

        private void RedoAction()
        {
            if (redoStack.Count == 0) return;
            var json = JsonConvert.SerializeObject(commandsData);
            undoStack.Push(json);
            json = redoStack.Pop();
            commandsData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<Command>>>>(json)!;
            RebuildTree(true);
            UpdateUndoRedoButtons();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
