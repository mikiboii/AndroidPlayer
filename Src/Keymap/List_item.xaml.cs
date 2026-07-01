using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using System.Windows.Media;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Store;


namespace Androidplayer.Src.Keymap
{
    public partial class List_item : System.Windows.Controls.UserControl
    {
        private bool _isExitingEditMode = false;
        
        public static readonly DependencyProperty ItemNameProperty =
            DependencyProperty.Register("ItemName", typeof(string), typeof(List_item), 
                new PropertyMetadata("", OnItemNameChanged));

        public string ItemName
        {
            get { return (string)GetValue(ItemNameProperty); }
            set { SetValue(ItemNameProperty, value); }
        }

        public List_item()
        {
            InitializeComponent();
        }

        private static void OnItemNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (List_item)d;
            // Update display if needed
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            EnterEditMode();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle delete logic here
            
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
                    keymapArray.Remove(keymapToRemove); // remove from database
                    editor.Set("keymaps", keymapArray); // save changes back
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
                                // Call a function on the new default
                                
                                
                                k_info.Instance.KeymapMenu.SelectItem(listItem);
                                
                                // Inside DeleteButton_Click
                                // var container = Window.GetWindow(this) as keymap_menu; 
                                // if (container != null)
                                // {
                                //     container.SelectItem(listItem); // call parent method
                                // }
                                //
                                //
                                //
                                //
                                // listItem.SetSelected(true); // Example function
                                break;
                            }
                        }
                        
                        
                    }
                    else
                    {

                        k_info.Instance.DefaultKeymap = null;
                        // No keymaps left, clear default
                        editor.Set("default_keymap", null);
                    }
                    
                }
                
                parentPanel.Children.Remove(this);
            }
        }

        private void EnterEditMode()
        {
            // Show edit controls, hide normal controls
            DisplayTextBlock.Visibility = Visibility.Collapsed;
            EditTextBox.Visibility = Visibility.Visible;
            
            NormalButtonsPanel.Visibility = Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;
            
            // Focus and select text - use Dispatcher to ensure proper timing
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
            {
                EditTextBox.Focus();
                EditTextBox.SelectAll();
                // Keyboard.Focus(EditTextBox);
            }));
        }

        // private void ExitEditMode()
        // {
        //     // Show normal controls, hide edit controls
        //     DisplayTextBlock.Visibility = Visibility.Visible;
        //     EditTextBox.Visibility = Visibility.Collapsed;
        //     
        //     NormalButtonsPanel.Visibility = Visibility.Visible;
        //     SaveButton.Visibility = Visibility.Collapsed;
        //     
        //     // Validate input
        //     if (string.IsNullOrWhiteSpace(ItemName))
        //     {
        //         ItemName = "miki_demo"; // Default fallback
        //     }
        // }
        
        
        private void ExitEditMode()
        {
            // Console.WriteLine("here ######");
            string newName = EditTextBox.Text.Trim();

            // Get the editor instance
            var editor = my_info.Instance.Dataeditor;
            if (editor == null)
            {
                return;
            }

            // Get keymaps array
            var keymaps = editor.Get("keymaps");
            if (keymaps.IsNull || !keymaps.IsArray)
            {
                return;
            }

            
            

            // Console.WriteLine("******************");
            
            // Check if newName already exists in the database
            foreach (var keymapEntry in keymaps.AsArray)
            {
                if (keymapEntry.IsDocument && keymapEntry.AsDocument.Keys.Count > 0)
                {
                    string keymapName = keymapEntry.AsDocument.Keys.First();

                    // Console.WriteLine($"db : {keymapName}, cn : {newName}");


                    if (keymapName == newName && keymapName != ItemName)
                    {
                        

                       
                            
                        MessageBox.Show("This name already exists in the database. Please choose another.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                        {
                            EditTextBox.Focus();
                            EditTextBox.SelectAll();
                        }));
                        
                        
                        
                        

                        
                        return;
                        
                    }
                    

                    // if (keymapName.Equals(newName, StringComparison.OrdinalIgnoreCase) && keymapName != ItemName)
                    // {
                    //     MessageBox.Show("This name already exists in the database. Please choose another.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    //
                    //     // Keep focus on EditTextBox
                    // Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                    // {
                    //     EditTextBox.Focus();
                    //     EditTextBox.SelectAll();
                    // }));
                    //
                    //     return; // Exit without leaving edit mode
                    // }
                }
            }




            Console.WriteLine($"new: {newName}, old: {ItemName}");
            
            
            
            
            
            // 🔹 Find and rename the keymap in the database
            for (int i = 0; i < keymaps.AsArray.Count; i++)
            {
                var entry = keymaps.AsArray[i];
                if (entry.IsDocument && entry.AsDocument.Keys.First() == ItemName)
                {
                    var doc = entry.AsDocument;
                    var oldData = doc[ItemName]; // Keep the existing data

                    // Remove the old key and add the new one
                    doc.Remove(ItemName);
                    doc[newName] = oldData;

                    // Replace the old entry in the keymaps array
                    keymaps.AsArray[i] = doc;

                    // Save changes to editor
                    editor.Set("keymaps", keymaps);

                    // Update default keymap reference if needed
                    var defaultKeymap = editor.Get("default_keymap");
                    if (defaultKeymap.IsString && defaultKeymap.AsString == ItemName)
                    {
                        editor.Set("default_keymap", newName);
                    }

                    break;
                }
            }
            
            
            
            // Force data binding to update
            var binding = EditTextBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();

            // Show normal controls, hide edit controls
            DisplayTextBlock.Visibility = Visibility.Visible;
            EditTextBox.Visibility = Visibility.Collapsed;

            NormalButtonsPanel.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Collapsed;

            // Validate input
            

            // Update display text if needed
            DisplayTextBlock.Text = ItemName;
        }


        private void EditTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

            if (e.Key == Key.Enter)
            {

                if (_isExitingEditMode != true)
                {
                    _isExitingEditMode = true;
                    ExitEditMode();
                }
                
                // Console.WriteLine("in inter");
                e.Handled = true;
            }
            // else if (e.Key == Key.Escape)
            // {
            //     // Cancel edit - revert to original value
            //     // Console.WriteLine("in escape");
            //     EditTextBox.Text = ItemName;
            //     ExitEditMode();
            //     e.Handled = true;
            // }
            
            
            
           
        }

         // Make sure this namespace is included

        public void SetSelected(bool isSelected)
        {
            MainBorder.BorderBrush = isSelected 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A73E8")) 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
        }


        private void EditTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Console.WriteLine("lost keyboard focus");
            
            // EditTextBox.Text = ItemName;
            
            if (_isExitingEditMode != true)
            {
                
                ExitEditMode();
            }

            _isExitingEditMode = false;
        }
    }
}