using System;
using Androidplayer.Src;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Color = System.Drawing.Color;

namespace Androidplayer.Native_test;

public partial class My_window : Window
{
    
    private Adb_worker_test my_adb_worker;
    public My_window()
    {
      
        InitializeComponent();
        
        // this.Opacity = 0;
        // this.ShowInTaskbar = false;
        // this.IsHitTestVisible = false;
        
        // my_view.HandleCreated += My_viewOnHandleCreated;

        Console.WriteLine("my constructor");
        Loaded += OnLoaded;
        
    }

    private void My_viewOnHandleCreated(object? sender, IPlatformHandle e)
    {
        Console.WriteLine("My_viewOnHandleCreated");
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // MyView.IsVisible = true;
      
        
        // my_adb_worker = new Adb_worker_test();
        // my_adb_worker.ProgressChanged += my_app_worker_ProgressChanged;
        // my_adb_worker.CountingCompleted += My_adb_workerOnCountingCompleted;
        // my_adb_worker.devicedisconnected += My_adb_workerOndevicedisconnected;

        // my_adb_worker.StartCounting();
        
        
        // my_adb_worker.Start();
        
        
        // var timer = new DispatcherTimer
        // {
        //     Interval = TimeSpan.FromSeconds(5)
        // };
        // timer.Tick += (s, args) =>
        // {
        //     timer.Stop();   // one-shot
        //
        //    
        //
        //     Console.WriteLine("my_view ready ##################");
        //     
        //     MyView.IsVisible = true;
        //
        //    
        // };
        // timer.Start();
        
    }
}