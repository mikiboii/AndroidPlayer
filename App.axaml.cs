using System.Threading;
using System.Threading.Tasks;
using Androidplayer.Native_test;
using Androidplayer.windows;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Androidplayer;

public partial class App : Application
{
    public override void Initialize()
    {
        StartupTimer.Mark("App ctor");
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        StartupTimer.Mark("App.OnFrameworkInitializationCompleted");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // desktop.MainWindow = new MainWindow();
            // desktop.MainWindow = new My_window();
            // desktop.MainWindow = new My_window
            // {
            //     ShowInTaskbar = false,
            //     Opacity = 0,
            //     IsVisible = false // Keeps the window loaded but hidden
            // };
            
            // desktop.MainWindow = new Home();
            
            // demo();
            //
            // desktop.MainWindow = new Home
            // {
            //     
            //     Opacity = 0,
            //     ShowInTaskbar = false,
            //     IsVisible = false,
            //     WindowState = WindowState.Minimized
            // };
            
            
            
            // desktop.MainWindow.Hide();
            
            _ = StartAsync(desktop);

            // var settings = new settings();
            //
            // settings.Show();
            

            // Thread.Sleep(3000);
            //
            //
            // desktop.MainWindow = new Home();
            
            // StartAsync();
            
            // var mainWindow = new My_window();
            // mainWindow.Opacity = 0;
            // mainWindow.ShowInTaskbar = false;
            // mainWindow.ShowActivated = false;
            //
            // mainWindow.Show();
            //     
            // mainWindow.Hide();  

            
            StartupTimer.Mark("Home created");
            
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private async void demo ()
    {
        var splash = new SplashScreen();
        splash.Show();
        
        //     await Task.Run(() =>
        //     {
        //         System.Threading.Thread.Sleep(3000);
        //     });
    }
    
    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Don't let the app shut down just because the splash closes.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 1. Show splash
        var splash = new SplashScreen();
        splash.Show();

        // 2. Minimum splash time
        await Task.Delay(3000);

        // 3. Build Home with a *cheap* initializer — no IsVisible, no WindowState here
        var home = new Home
        {
            Opacity = 0,             
            ShowInTaskbar = false,
            IsVisible = false,
            WindowState = WindowState.Minimized
        };

        // 4. Show it so the handle exists and Loaded can fire
        home.Show();

        // 5. Reveal it once it's actually loaded and laid out
        // home.Loaded += (_, _) =>
        // {
        //     home.WindowState = WindowState.Normal;   // only now is this safe
        //     home.ShowInTaskbar = true;
        //     home.Opacity = 1;
        //     home.Activate();
        // };

        // 6. Now that Home is the main window, hook up the shutdown policy
        desktop.MainWindow = home;

        // 7. Close splash
        splash.Close();

        // 8. Only now enable the "exit when main window closes" behavior
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
    
    
}