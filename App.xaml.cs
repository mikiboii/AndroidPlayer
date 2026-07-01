using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

using Androidplayer.windows;

namespace Androidplayer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        // var home = new My_window();
        
        
        // var home = new Overlay_window();
        var home = new Home();
        // var home = new Keymap_Window();
        
        

        // 2️⃣ Load main window in background

        // var app_process = Environment.Is64BitProcess ? @"c:\deps\x64" : @"c:\deps\x32";
        //
        // Console.WriteLine(app_process);
      
       
        
        var splash = new Splash_screen();
        splash.Show();
        // Optionally do some heavy initialization here
        await Task.Run(() =>
        {
            // Simulate work like loading data, initializing services, etc.
            System.Threading.Thread.Sleep(3000);
        });
        
        // 3️⃣ Close splash and show main window
        splash.Close();
        
        
        
        home.Show();
        
        
    }
}