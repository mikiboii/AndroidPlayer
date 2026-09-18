using System;
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
    public My_window()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
        
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // MyView.IsVisible = true;
        
        
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