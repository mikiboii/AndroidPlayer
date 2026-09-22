using System;
using Androidplayer.Src;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Color = System.Drawing.Color;

namespace Androidplayer.Native_test;

public partial class My_window : Window
{
    
    private Adb_worker my_adb_worker;
    public My_window()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
        
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // MyView.IsVisible = true;
        
        
        my_adb_worker = new Adb_worker();
        // my_adb_worker.ProgressChanged += my_app_worker_ProgressChanged;
        // my_adb_worker.CountingCompleted += My_adb_workerOnCountingCompleted;
        // my_adb_worker.devicedisconnected += My_adb_workerOndevicedisconnected;

        my_adb_worker.StartCounting();
        
        
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