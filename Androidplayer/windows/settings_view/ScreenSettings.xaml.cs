using System.Windows.Controls;

namespace Androidplayer.windows.settings_view;

public partial class ScreenSettings : UserControl
{
    
    string[] Themes = new string[] { "Dark", "Light" };
    string[] resolution = new string[] { "480p", "720p", "1080p" , "1280p", "1K", "2K" };
    
    int[] fps = new int[] { 10, 20, 30, 40, 60, 90 };
    
    
    public ScreenSettings()
    {
        InitializeComponent();


        resolution_combo.ItemsSource = resolution;

        fps_combo.ItemsSource = fps;

        
        this.DataContext = UISettings.Instance;
        
        //
        // var currentResolution = ((UISettings)this.DataContext).Resolution;
        // var currentFPS = ((UISettings)this.DataContext).FPS;
        // var currentBitrate = ((UISettings)this.DataContext).Bitrate;
        // var audioEnabled = ((UISettings)this.DataContext).AudioEnabled;
        //
        // Console.WriteLine($"Resolution: {currentResolution}, FPS: {currentFPS}, Bitrate: {currentBitrate}, AudioEnabled: {audioEnabled}");
        //
        //
        
    }
}