using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Store;
using LiteDB;

namespace Androidplayer.Src.Keymap;

public partial class keymap_menu : UserControl
{
    private List_item? _selectedItem;

    public keymap_menu()
    {
        InitializeComponent();

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

                var item = new List_item
                {
                    ItemName = keymapName
                };

                // Avalonia: PointerReleased instead of MouseLeftButtonUp
                item.PointerReleased += (s, e) => SelectItem(item);

                keymap_list_view.Children.Add(item);

                if (keymapName == default_keymap)
                {
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
            case nameof(k_info.KeymapMode):
                bool keymapMode = k_info.Instance.KeymapMode;

                // Avalonia: IsVisible = bool instead of Visibility
                k_info.Instance.KeymapMenu.IsVisible = keymapMode;

                break;
        }
    }

    public void SelectItem(List_item item)
    {
        _selectedItem?.SetSelected(false);

        _selectedItem = item;
        _selectedItem.SetSelected(true);

        var editor = my_info.Instance.Dataeditor;
        var root = editor.Get().AsDocument;
        root["default_keymap"] = item.ItemName;
        editor.Set("", root);

        k_info.Instance.DefaultKeymap = item.ItemName;
    }

    private void Add_new_keymap_OnClick(object? sender, RoutedEventArgs e)
    {
        var editor = my_info.Instance.Dataeditor;

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

        string baseName = "keymap ";
        int count = 1;
        string newName;

        var existingNames = keymaps.AsArray
            .Where(item => item.IsDocument && item.AsDocument.Keys.Count > 0)
            .Select(item => item.AsDocument.Keys.First())
            .ToList();

        while (true)
        {
            newName = baseName + count;
            if (!existingNames.Contains(newName))
                break;
            count++;
        }

        var newKeymap = new BsonDocument
        {
            [newName] = new BsonDocument()
        };

        editor.Append("keymaps", newKeymap);

        var item = new List_item
        {
            ItemName = newName
        };

        item.PointerReleased += (s, e) => SelectItem(item);

        keymap_list_view.Children.Add(item);

        if (isFirstKeymap)
        {
            var root = editor.Get().AsDocument;
            root["default_keymap"] = newName;
            editor.Set("", root);

            SelectItem(item);
        }
    }

    private void ToggleButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!k_info.Instance.Collapsed)
        {
            // Collapse
            ContentGrid.IsVisible = false;
            Width = 24;

            // Change icon to "arrow-right"
            // ToggleIcon.Source = new Bitmap(AssetLoader.Open(
            //     new Uri("avares://Androidplayer/K_icons/arrow-right.png")));
            
            ToggleIcon.Source = new Bitmap(AssetLoader.Open(
                new Uri("avares://Androidplayer/Src/Keymap/K_icons/arrow-right.png")));

            k_info.Instance.Collapsed = true;
        }
        else
        {
            // Expand
            ContentGrid.IsVisible = true;
            Width = 314;

            // Change icon back to "arrow-left"
            ToggleIcon.Source = new Bitmap(AssetLoader.Open(
                new Uri("avares://Androidplayer/Src/Keymap/K_icons/arrow-left.png")));

            k_info.Instance.Collapsed = false;
        }
    }

    private void Save_keyamp_OnClick(object? sender, RoutedEventArgs e)
    {
        OverlayManager.Instance.Save_keymap_data();
    }

    private void Close_keymap_OnClick(object? sender, RoutedEventArgs e)
    {
        k_info.Instance.Toggle_Keymap_mode();
    }
}