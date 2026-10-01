using System;
using System.ComponentModel;
using System.Configuration;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Androidplayer.windows.settings_view;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Androidplayer.windows;

public partial class settings : Window
{
    Configuration AppConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

    public settings()
    {
        InitializeComponent();

        this.Loaded += OnLoaded;
        SettingsListBox.SelectionChanged += SettingsListBox_SelectionChanged;
        SettingsListBox.SelectedIndex = 0;
        SettingsContent.Content = new ScreenSettings();

        this.DataContext = UISettings.Instance;
        
        UISettings.Instance.PropertyChanged += UISettingsOnPropertyChanged;
        
        
        
        // // 1. Point to your converted PNG resource
        // var cursorUri = new Uri(UISettings.Instance.CurrentCursor);
        //
        // using var cursorStream = AssetLoader.Open(cursorUri);
        // var cursorBitmap = new Avalonia.Media.Imaging.Bitmap(cursorStream);
        //
        // var hotSpot = new PixelPoint(0, 0);
        //
        // Cursor customCursor = new Cursor(cursorBitmap, hotSpot);
        //
        // this.Cursor = customCursor;
        
    }

    private void UISettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UISettings.CurrentCursor))
        {
            // WPF: Application.Current.Dispatcher.Invoke(...)
            // Avalonia: Dispatcher.UIThread.Post(...) — fire-and-forget on UI thread
            Dispatcher.UIThread.Post(() =>
            {


                if (UISettings.Instance.CurrentCursor == "Default")
                {
                    this.Cursor = new Cursor(StandardCursorType.Arrow);
                }
                else
                {
                    // 1. Point to your converted PNG resource
                    var cursorUri = new Uri(UISettings.Instance.CurrentCursor);

// 2. Open the asset stream
                    using var cursorStream = AssetLoader.Open(cursorUri);

// 3. Load the image into an Avalonia Bitmap
                    var cursorBitmap = new Avalonia.Media.Imaging.Bitmap(cursorStream);

// 4. Set the Hotspot (X, Y in pixels). 
// For a sword tip, it's typically the top-left corner (0, 0)
                    var hotSpot = new PixelPoint(0, 0);

// 5. Instantiate the cursor correctly
                    Cursor customCursor = new Cursor(cursorBitmap, hotSpot);

                    this.Cursor = customCursor;
                    
                }
                
                // bool isLocked = my_info.Instance.IsMouseLocked;
                //
                // if (my_info.Instance.IsMouseLocked)
                // {
                //     PressMouse_demo();
                // }
                // else
                // {
                //     UnPressMouse_demo();
                // }
            });
        }
    }


    private void SettingsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        switch (SettingsListBox.SelectedIndex)
        {
            case 0: // Screen Settings
                SettingsContent.Content = new ScreenSettings();
                break;
            case 1: // Window Settings
                SettingsContent.Content = new WindowSettings();
                break;
            case 2: // Mouse Settings
                SettingsContent.Content = new MouseSettings();
                break;
            default:
                SettingsContent.Content = new ScreenSettings();
                break;
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
    }

    private void TopBar_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            if (e.Source is Image) return;

            this.BeginMoveDrag(e);
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void save_btn_click(object? sender, RoutedEventArgs e)
    {
        
        UISettings.Instance.Save();
        ShowToast("done", "Saved successfully!");
        
        

    }
    
    
  
    
    
    public void ShowToast(string mode, string message)
    {
        // Uses the settings window's own children instead of a canvas
        var toastBorder = this.FindControl<Border>("ToastMessage");
        var toastText   = this.FindControl<TextBlock>("ToastText");
        var toasticon   = this.FindControl<Image>("Toasticon");

        if (toastBorder == null || toastText == null || toasticon == null)
        {
            Console.WriteLine("⚠️ Toast elements not found.");
            return;
        }

        toasticon.Source = new Bitmap(AssetLoader.Open(new Uri(
            mode.Equals("warning", StringComparison.OrdinalIgnoreCase)
                ? "avares://Androidplayer/Icons/warning.png"
                : "avares://Androidplayer/Icons/checkmark.png"
        )));

        toastText.Text = message;

        // The settings window uses a Grid, so no Canvas.SetLeft/Top.
        // The toast Border already has HorizontalAlignment="Center" VerticalAlignment="Top"
        // and Margin="0,20,0,0" in XAML, which positions it correctly.
        toastBorder.ZIndex = 9999;

        toastBorder.Opacity = 1;
        toastBorder.IsVisible = true;

        DispatcherTimer.RunOnce(() =>
        {
            toastBorder.IsVisible = false;
        }, TimeSpan.FromSeconds(2.5));
    }
    
    
    
}