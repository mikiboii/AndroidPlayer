using System.Configuration;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Androidplayer.windows.settings_view;

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
                SettingsContent.Content = null;
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
    }
}