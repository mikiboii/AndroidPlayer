using System;
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
        SettingsListBox.SelectedIndex = 2;
        SettingsContent.Content = new MouseSettings();

        this.DataContext = UISettings.Instance;
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