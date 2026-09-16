using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Store;

namespace Androidplayer.Src.Keymap
{
    public partial class List_item : UserControl
    {
        private bool _isExitingEditMode = false;

        // Avalonia property registration
        public static readonly StyledProperty<string> ItemNameProperty =
            AvaloniaProperty.Register<List_item, string>(nameof(ItemName), defaultValue: "");

        public string ItemName
        {
            get => GetValue(ItemNameProperty);
            set => SetValue(ItemNameProperty, value);
        }

        public List_item()
        {
            InitializeComponent();
        }

        private void EditButton_Click(object? sender, RoutedEventArgs e)
        {
            EnterEditMode();
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            ExitEditMode();
        }

        private void DeleteButton_Click(object? sender, RoutedEventArgs e)
        {
            if (this.Parent is Panel parentPanel)
            {
                var editor = my_info.Instance.Dataeditor;
                if (editor == null)
                {
                    return;
                }

                var keymaps = editor.Get("keymaps");
                if (keymaps.IsNull || !keymaps.IsArray)
                {
                    return;
                }

                var default_keymap = editor.Get("default_keymap")?.AsString;

                var keymapArray = keymaps.AsArray;
                var keymapToRemove = keymapArray.FirstOrDefault(k =>
                    k.IsDocument &&
                    k.AsDocument.Keys.Count > 0 &&
                    k.AsDocument.Keys.First().Equals(ItemName, StringComparison.OrdinalIgnoreCase)
                );

                if (keymapToRemove != null)
                {
                    keymapArray.Remove(keymapToRemove);
                    editor.Set("keymaps", keymapArray);
                }

                if (default_keymap == ItemName)
                {
                    if (keymapArray.Count > 0 && keymapArray[0].IsDocument && keymapArray[0].AsDocument.Keys.Count > 0)
                    {
                        string newDefault = keymapArray[0].AsDocument.Keys.First();
                        editor.Set("default_keymap", newDefault);

                        foreach (var child in parentPanel.Children)
                        {
                            if (child is List_item listItem && listItem.ItemName == newDefault)
                            {
                                k_info.Instance.KeymapMenu?.SelectItem(listItem);
                                break;
                            }
                        }
                    }
                    else
                    {
                        k_info.Instance.DefaultKeymap = null;
                        editor.Set("default_keymap", null);
                    }
                }

                parentPanel.Children.Remove(this);
            }
        }

        private void EnterEditMode()
        {
            DisplayTextBlock.IsVisible = false;
            EditTextBox.IsVisible = true;

            NormalButtonsPanel.IsVisible = false;
            SaveButton.IsVisible = true;

            // Avalonia: Dispatcher.UIThread.Post replaces BeginInvoke
            Dispatcher.UIThread.Post(() =>
            {
                EditTextBox.Focus();
                EditTextBox.SelectAll();
            }, DispatcherPriority.Input);
        }

        private void ExitEditMode()
        {
            string newName = EditTextBox.Text?.Trim() ?? "";

            var editor = my_info.Instance.Dataeditor;
            if (editor == null)
            {
                return;
            }

            var keymaps = editor.Get("keymaps");
            if (keymaps.IsNull || !keymaps.IsArray)
            {
                return;
            }

            // Check for duplicate names
            foreach (var keymapEntry in keymaps.AsArray)
            {
                if (keymapEntry.IsDocument && keymapEntry.AsDocument.Keys.Count > 0)
                {
                    string keymapName = keymapEntry.AsDocument.Keys.First();

                    if (keymapName == newName && keymapName != ItemName)
                    {
                        // Avalonia: no MessageBox. Log instead, or replace with a custom dialog.
                        Console.WriteLine("Duplicate name: " + newName);

                        Dispatcher.UIThread.Post(() =>
                        {
                            EditTextBox.Focus();
                            EditTextBox.SelectAll();
                        }, DispatcherPriority.Input);

                        return;
                    }
                }
            }

            Console.WriteLine($"new: {newName}, old: {ItemName}");

            // Rename the keymap in the database
            for (int i = 0; i < keymaps.AsArray.Count; i++)
            {
                var entry = keymaps.AsArray[i];
                if (entry.IsDocument && entry.AsDocument.Keys.First() == ItemName)
                {
                    var doc = entry.AsDocument;
                    var oldData = doc[ItemName];

                    doc.Remove(ItemName);
                    doc[newName] = oldData;

                    keymaps.AsArray[i] = doc;

                    editor.Set("keymaps", keymaps);

                    var defaultKeymap = editor.Get("default_keymap");
                    if (defaultKeymap.IsString && defaultKeymap.AsString == ItemName)
                    {
                        editor.Set("default_keymap", newName);
                    }

                    break;
                }
            }

            // Avalonia: no GetBindingExpression for TwoWay binding like WPF.
            // Simpler: set ItemName directly, since we already computed it.
            ItemName = newName;

            DisplayTextBlock.IsVisible = true;
            EditTextBox.IsVisible = false;

            NormalButtonsPanel.IsVisible = true;
            SaveButton.IsVisible = false;

            DisplayTextBlock.Text = ItemName;
        }

        private void EditTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_isExitingEditMode != true)
                {
                    _isExitingEditMode = true;
                    ExitEditMode();
                }

                e.Handled = true;
            }
        }

        public void SetSelected(bool isSelected)
        {
            MainBorder.BorderBrush = isSelected
                ? new SolidColorBrush(Color.Parse("#1A73E8"))
                : new SolidColorBrush(Color.Parse("#E0E0E0"));
        }

        private void EditTextBox_OnLostKeyboardFocus(object? sender, RoutedEventArgs e)
        {
            if (_isExitingEditMode != true)
            {
                ExitEditMode();
            }

            _isExitingEditMode = false;
        }
    }
}