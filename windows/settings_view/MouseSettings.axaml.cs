using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Androidplayer.windows.settings_view;

public partial class MouseSettings : UserControl
{
    
    private bool _isInitializing = false;
    
    public MouseSettings()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        
        
        
        
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        try
        {
            string saved = UISettings.Instance.CurrentCursor;

            var match = this.GetVisualDescendants()
                .OfType<RadioButton>()
                .FirstOrDefault(rb =>
                    string.Equals(rb.Tag?.ToString(), saved,
                        StringComparison.OrdinalIgnoreCase));

            match ??= Cursor_Default;
            if (match != null) match.IsChecked = true;
        }
        finally { _isInitializing = false; }
    }

    private void CursorCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border card) return;

        var rb = card.GetVisualDescendants()
            .OfType<RadioButton>()
            .FirstOrDefault();

        if (rb == null) return;

        if (rb.IsChecked != true)
            rb.IsChecked = true;

        // Stop the event from bubbling to the RadioButton's own handler,
        // so we don't get double-toggle behavior.
        e.Handled = true;
    }
    private void Cursor_Checked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        // Clear 'selected' from all cursor cards
        foreach (var card in this.GetVisualDescendants()
                     .OfType<Border>()
                     .Where(b => b.Classes.Contains("cursorCard")))
        {
            card.Classes.Remove("selected");
        }

        // Mark the card that contains this radio as selected
        var parentCard = rb.FindAncestorOfType<Border>();
        parentCard?.Classes.Add("selected");

        string key = rb.Tag?.ToString() ?? "default";
        Console.WriteLine($"[cursor] selected: {key}");
        
        UISettings.Instance.CurrentCursor = key;   // <-- the avares:// path
        UISettings.Instance.Save();
        
        
        
//         // 1. Point to your converted PNG resource
//         var cursorUri = new Uri(key);
//
// // 2. Open the asset stream
//         using var cursorStream = AssetLoader.Open(cursorUri);
//
// // 3. Load the image into an Avalonia Bitmap
//         var cursorBitmap = new Avalonia.Media.Imaging.Bitmap(cursorStream);
//
// // 4. Set the Hotspot (X, Y in pixels). 
// // For a sword tip, it's typically the top-left corner (0, 0)
//         var hotSpot = new PixelPoint(0, 0);
//
// // 5. Instantiate the cursor correctly
//         Cursor customCursor = new Cursor(cursorBitmap, hotSpot);
//
//         this.Cursor = customCursor;
        
        
        
        
        
    }
}