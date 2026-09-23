using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Androidplayer.Src.Keymap.K_store;
using Avalonia.Interactivity;

namespace Androidplayer.windows;

public partial class Keymap_Window : Window
{
    private Window mainWindow;
    private bool _positionedOnce = false;

    public Keymap_Window(Window mainWindow)
    {
        InitializeComponent();

        this.mainWindow = mainWindow;

        // this.Owner = mainWindow;

        Loaded += Keymap_Window_Loaded;

        // Avalonia: window position uses PositionChanged, not LocationChanged
        mainWindow.PositionChanged += MainWindow_PositionChanged;

        // Size and state are property changes, not separate events
        mainWindow.PropertyChanged += MainWindow_PropertyChanged;

        // mainWindow.Activated += MainWindow_Activated;
        
        // this.Activated += OnActivated;

        k_info.Instance.PropertyChanged += K_info_PropertyChanged;
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        
        this.mainWindow.Activate();
        if (this.IsVisible)
        {
            this.Topmost = true;
            this.Topmost = false;
            // Avalonia has no UpdateLayout() — layout is done automatically.
        }
    }

    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // Avalonia doesn't have a dedicated SizeChanged or StateChanged event on Window.
        // Watch the Bounds and WindowState properties instead.

        if (e.Property == Window.BoundsProperty)
        {
            UpdatePosition();
        }
        else if (e.Property == Window.WindowStateProperty)
        {
            UpdatePosition();
        }
        else if (e.Property == Window.ClientSizeProperty)
        {
            UpdatePosition();
        }
    }

    private void K_info_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(k_info.Collapsed):
                Dispatcher.UIThread.Post(() =>
                {
                    if (k_info.Instance.Collapsed)
                    {
                        this.Width = 24;
                    }
                    else
                    {
                        this.Width = 314;
                    }
                });
                break;
        }
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {

        Console.WriteLine("from keymapwindow MainWindow_Activated called");
        // this.Activate();
        if (this.IsVisible)
        {
            this.Topmost = true;
            this.Topmost = false;
            // Avalonia has no UpdateLayout() — layout is done automatically.
        }
    }

    private void MainWindow_PositionChanged(object? sender, PixelPointEventArgs e)
    {
        UpdatePosition();
    }

    private void Keymap_Window_Loaded(object? sender, RoutedEventArgs e)
    {
        UpdatePosition();
        this.Activate();
    }

    private void UpdatePosition()
    {
        if (mainWindow == null) return;

        // Avalonia: use Position (PixelPoint) instead of Left/Top
        // Main window's position + offset (3px right, 30px down)
        var mainPos = mainWindow.Position;

        this.Position = new PixelPoint(
            mainPos.X + 3,
            mainPos.Y + 30);

        // Set height based on main window height
        double myHeight = mainWindow.Bounds.Height - 32;
        if (myHeight >= 0)
        {
            this.Height = myHeight;
        }

        // Show the sidebar
        if (!this.IsVisible)
        {
            this.Show();
        }

        _positionedOnce = true;
    }

    private void Keymap_Window_OnActivated(object? sender, EventArgs e)
    {
        if (mainWindow != null && !mainWindow.IsActive)
        {
            mainWindow.Activate();
        }
    }
}