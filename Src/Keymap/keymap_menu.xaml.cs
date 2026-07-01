using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Store;
using LiteDB;
using UserControl = System.Windows.Controls.UserControl;

namespace Androidplayer.Src.Keymap;

public partial class keymap_menu : UserControl
{
    
    private List_item? _selectedItem;
    public keymap_menu()
    {
        InitializeComponent();
        
        
        
        // for (int i = 1; i <= 5; i++)
        // {
        //     var item = new List_item
        //     {
        //         ItemName = $"Keymap Item {i}"
        //     };
        //
        //     keymap_list_view.Children.Add(item);
        //     // ItemsPanel.Children.Add(item);
        // }
        
        // for (int i = 1; i <= 5; i++)
        // {
        //     var item = new List_item
        //     {
        //         ItemName = $"Keymap Item {i}"
        //     };
        //
        //     item.MouseLeftButtonUp += (s, e) =>
        //     {
        //         SelectItem(item);
        //     };
        //
        //     keymap_list_view.Children.Add(item);
        // }
        //
        // // Set initial selection to Keymap Item 2
        // if (keymap_list_view.Children.Count >= 2)
        //     SelectItem((List_item)keymap_list_view.Children[1]);
        
        
        
        k_info.Instance.KeymapMenu = this;
        
            
            
            
        // Access the global LiteDbEditor instance
        var editor = my_info.Instance.Dataeditor;

        if (editor == null)
        {
            Console.WriteLine("editor is null");
            
            return;
        }

        // Get the "keymaps" array from the DB
        var keymaps = editor.Get("keymaps");

        
        // var default_keymap = editor.Get("default_keymap");
        
        var default_keymap = editor.Get("default_keymap")?.AsString; 
        
        // If no keymaps yet, just skip
        if (keymaps.IsNull || !keymaps.IsArray)
            return;

        // Loop through all keymaps and add them to the list
        foreach (var keymapEntry in keymaps.AsArray)
        {
            if (keymapEntry.IsDocument && keymapEntry.AsDocument.Keys.Count > 0)
            {
                string keymapName = keymapEntry.AsDocument.Keys.First();

                // Console.WriteLine(keymapName);

                

                var item = new List_item
                {
                    ItemName = keymapName
                };

                // Hook up the click event
                item.MouseLeftButtonUp += (s, e) => SelectItem(item);

                // Add to your view
                keymap_list_view.Children.Add(item);
                
                
                if (keymapName == default_keymap)
                {
                    // Console.WriteLine("found the default #####");
                    
                    SelectItem(item);
                }
            }
        }

        
        k_info.Instance.PropertyChanged += InstanceOnPropertyChanged;
        
        
        
    }

    private void InstanceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    { 
        switch (e.PropertyName)
        {
            // case nameof(My_Store.VideoWidth):
            case nameof(k_info.KeymapMode):


                bool KeymapMode = k_info.Instance.KeymapMode;

               
                k_info.Instance.KeymapMenu.Visibility = k_info.Instance.KeymapMode ? Visibility.Visible : Visibility.Collapsed;
    
                break;
 
        }
    }


    public void SelectItem(List_item item)
    {
        // Deselect previous
        _selectedItem?.SetSelected(false);

        // Select new
        _selectedItem = item;
        _selectedItem.SetSelected(true);

        
        
        var editor = my_info.Instance.Dataeditor;
        var root = editor.Get().AsDocument;
        root["default_keymap"] = item.ItemName;
        editor.Set("", root);
        // editor.Save();
        
        k_info.Instance.DefaultKeymap = item.ItemName;

        // Update the label (optional)
       
    }


   

    // private void Add_new_keymap_OnClick(object sender, RoutedEventArgs e)
    // {
    //     string file_path = Environment.CurrentDirectory + "\\user\\data.db";
    //
    //     
    //     using var db = new LiteDatabase(file_path);
    //     
    //     var col = db.GetCollection<KeymapItem>("keymaps");
    //
    //     Console.WriteLine(col);
    //     
    // }
    
    
    private void Add_new_keymap_OnClick(object sender, RoutedEventArgs e)
    {
        
     
    
        var editor = my_info.Instance.Dataeditor;

        // Get the existing keymaps array
        var keymaps = editor.Get("keymaps");
        if (keymaps.IsNull || !keymaps.IsArray)
        {
            keymaps = new BsonArray();
            editor.Set("keymaps", keymaps);
        }

        bool isFirstKeymap = keymaps.AsArray.Count == 0;
        
        
        bool keymap_isfull = keymaps.AsArray.Count > 14;


        if (keymap_isfull)
        {
            return;
        }

        // Console.WriteLine($"is first keymap: {isFirstKeymap}");
        
        // Prepare base name
        string baseName = "keymap ";
        int count = 1;
        string newName;

        // Collect existing keymap names
        var existingNames = keymaps.AsArray
            .Where(item => item.IsDocument && item.AsDocument.Keys.Count > 0)
            .Select(item => item.AsDocument.Keys.First())
            .ToList();

        // Find a unique name
        while (true)
        {
            newName = baseName + count;
            if (!existingNames.Contains(newName))
                break;
            count++;
        }

        // Create the new empty keymap document
        var newKeymap = new BsonDocument
        {
            [newName] = new BsonDocument()
        };

        // Append to keymaps
        editor.Append("keymaps", newKeymap);
        
        
        var item = new List_item
        {
            ItemName = newName
        };

        // Hook up the click event
        item.MouseLeftButtonUp += (s, e) => SelectItem(item);

        // Add to your view
        keymap_list_view.Children.Add(item);
        
        
        if (isFirstKeymap)
        {
            var root = editor.Get().AsDocument;
            root["default_keymap"] = newName;
            editor.Set("", root);
            
            SelectItem(item);
        }

        
        

        
       






    }
    
    
    
    private bool _isCollapsed = false;



    private void ToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        
        if (!k_info.Instance.Collapsed)
        {
            // Collapse
            ContentGrid.Visibility = Visibility.Collapsed;
            Width = 24; // only show button

            // Change icon to "arrow-right"
            ToggleIcon.Source = new BitmapImage(
                new Uri("pack://application:,,,/Androidplayer;component/Src/Keymap/K_icons/arrow-right.png"));



            k_info.Instance.Collapsed = true;
            
           
                
        }
        else
        {
            // Expand
            ContentGrid.Visibility = Visibility.Visible;
            Width = 314;

            // Change icon back to "arrow-left"
            // ToggleIcon.Source = new BitmapImage(new Uri("pack://application:,,,/K_icons/arrow-left.png"));
            ToggleIcon.Source = new BitmapImage(
                new Uri("pack://application:,,,/Androidplayer;component/Src/Keymap/K_icons/arrow-left.png"));



            k_info.Instance.Collapsed = false;
        }
    }


    private void Save_keyamp_OnClick(object sender, RoutedEventArgs e)
    {
       OverlayManager.Instance.Save_keymap_data();
    }

    private void Close_keymap_OnClick(object sender, RoutedEventArgs e)
    {
        k_info.Instance.Toggle_Keymap_mode();
        // var fire = KeyMapManager.Instance?.GetElement("Fire");
        // if (fire != null)
        // {
        //     Console.WriteLine($"🔥 Fire: X={fire.X}, Y={fire.Y}");
        // }
        //
        // KeyMapManager.Instance?.PrintAll();
        //
        //
        //
        // var multiKeys = KeyMapManager.Instance?.GetMultiKeyElements();
        // if (multiKeys != null)
        // {
        //     Console.WriteLine($"🎹 Found {multiKeys.Count} multi-key elements:");
        //     foreach (var el in multiKeys)
        //     {
        //         Console.WriteLine($"  → {el.Type}: {string.Join("+", el.Keys)} at ({el.X}, {el.Y})");
        //     }
        // }
        
    }
}