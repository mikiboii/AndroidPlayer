using Androidplayer.Native_test;
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
            
            desktop.MainWindow = new Home();
            
            
            
            
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
}