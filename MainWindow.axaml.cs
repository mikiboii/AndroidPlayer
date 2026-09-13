using System;
using Androidplayer.Pages;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Androidplayer;



//
// ExtendClientAreaToDecorationsHint="True"
// ExtendClientAreaChromeHints="NoChrome"
// ExtendClientAreaTitleBarHeightHint="-1"
// SystemDecorations="None"
// CanResize="True"



//
// CanResize="True"
// ExtendClientAreaToDecorationsHint="True"
// ExtendClientAreaChromeHints="NoChrome"
// ExtendClientAreaTitleBarHeightHint="-1"



public partial class MainWindow : Window
{
    private const double ResizeBorder = 6;
    public MainWindow()
    {
        InitializeComponent();
        
        
        // PointerPressed += Window_PointerPressed;
        
        
        Loaded += OnLoaded;

        
        
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("windows loaded");
        
        // AndroidVideo.Overlay.PointerPressed += OverlayOnPointerPressed;
        
        AndroidVideo.HandleCreated += AndroidVideoOnHandleCreated;

        Title = "new mm";
    }

    private void AndroidVideoOnHandleCreated(object? sender, EventArgs e)
    {
        // if (AndroidVideo.Overlay != null)
        // {
        //     // AndroidVideo.Overlay.PointerPressed += OverlayOnPointerPressed;
        //     Console.WriteLine("Overlay created and event subscribed!");
        // }
    }
    //
    // private void OverlayOnPointerPressed(object? sender, PointerPressedEventArgs e)
    // {
    //     var position = e.GetPosition(AndroidVideo.Overlay);
    //
    //     // Print to console
    //     Console.WriteLine($"Pointer pressed from mainwindow at: X={position.X}, Y={position.Y}");
    // }


    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("button pressed");
        // Title = "ssss";

        // Console.WriteLine();
        AppTitleBar.Title = "mini app22";
    }
}