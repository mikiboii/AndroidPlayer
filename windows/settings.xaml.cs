using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        
      

        // var UISettingSection = AppConfig.GetSection("UISettings");
        //
        //
        // this.DataContext = UISettingSection;
        
        this.DataContext = UISettings.Instance;
        // Console.WriteLine("settings called ####");
    }

    private void SettingsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.OriginalSource is Image) return;

            this.DragMove();
        }
    }

    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // this.Close();
        this.Hide();

    }


    private void save_btn_click(object sender, RoutedEventArgs e)
    {
        // AppConfig.Save(ConfigurationSaveMode.Full);

        UISettings.Instance.Save();
    }
}