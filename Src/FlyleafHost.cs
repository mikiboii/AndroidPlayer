using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace Androidplayer;


public class FlyleafHost : Control
{
    
    public event EventHandler? HandleCreated;
    
    private VideoSurfaceWindow? _surfaceWindow;
    private VideoOverlayWindow? _overlayWindow;

    private bool _updating;
    private bool _started;

    
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<FlyleafHost, Control?>(
            nameof(Content));

    [Content]
    public Control? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
    
    
    
    // public static readonly StyledProperty<IBrush?> BackgroundProperty =
    //     AvaloniaProperty.Register<FlyleafHost, IBrush?>(
    //         nameof(Background), new SolidColorBrush(Colors.Red));
    //
    // public IBrush? Background
    // {
    //     get => GetValue(BackgroundProperty);
    //     set => SetValue(BackgroundProperty, value);
    // }
    //
    //
    
    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ContentProperty)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Content =
                    change.GetNewValue<Control?>();
            }
        }
    }
    
    
    public FlyleafHost()
    {
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        PropertyChanged  += OnBoundsChanged;
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    /// <summary>
    /// The native video surface window.
    /// </summary>
    public Window? Surface => _surfaceWindow;

    /// <summary>
    /// The Avalonia overlay window.
    /// </summary>
    public Window? Overlay => _overlayWindow;

    /// <summary>
    /// HWND of the video surface.
    /// Your D3D11 renderer can render to this.
    /// </summary>
    public IntPtr SurfaceHandle
    {
        get
        {
            return _surfaceWindow?
                       .TryGetPlatformHandle()?
                       .Handle
                   ?? IntPtr.Zero;
        }
    }

    // ============================================================
    // CREATE WINDOWS
    // ============================================================

    public void ShowVideo()
    {
        if (_started)
            return;

        var owner = TopLevel.GetTopLevel(this) as Window;

        if (owner == null)
            return;

        _started = true;

        // --------------------------------------------------------
        // Create surface
        // --------------------------------------------------------

        _surfaceWindow = new VideoSurfaceWindow();

        // Surface belongs to MainWindow
        _surfaceWindow.Show(owner);

        // --------------------------------------------------------
        // Create overlay
        // --------------------------------------------------------

        _overlayWindow = new VideoOverlayWindow();

        _overlayWindow.Background = new SolidColorBrush(Colors.Red); 
        // IMPORTANT:
        //
        // Overlay is owned by the SURFACE.
        //
        _overlayWindow.Show(_surfaceWindow);
        
        _overlayWindow.Content = Content;
        
        // _overlayWindow.PointerPressed += OverlayWindowOnPointerPressed;

        // --------------------------------------------------------
        // Synchronize
        // --------------------------------------------------------

        _surfaceWindow.PositionChanged += SurfacePositionChanged;
        _surfaceWindow.PropertyChanged += SurfacePropertyChanged;

        _overlayWindow.PositionChanged += OverlayPositionChanged;

        owner.PositionChanged += OwnerPositionChanged;
        owner.PropertyChanged += OwnerPropertyChanged;

        Dispatcher.UIThread.Post(
            SyncWindows,
            DispatcherPriority.Loaded);
        
        HandleCreated?.Invoke(this, EventArgs.Empty);
        
    }

    private void OverlayWindowOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var position = e.GetPosition(_overlayWindow);
    
        // Print to console
        Console.WriteLine($"Pointer pressed at: X={position.X}, Y={position.Y}");
    }

    // ============================================================
    // SYNCHRONIZATION
    // ============================================================

    // private void OnBoundsChanged(
    //     object? sender,
    //     EventArgs e)
    // {
    //     SyncWindows();
    // }
    
    private void OnBoundsChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            SyncWindows();
        }
    }

    private void OwnerPositionChanged(
        object? sender,
        PixelPointEventArgs e)
    {
        SyncWindows();
    }

    private void OwnerPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        SyncWindows();
    }

    private void SurfacePositionChanged(
        object? sender,
        PixelPointEventArgs e)
    {
        SyncOverlay();
    }

    private void SurfacePropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        SyncOverlay();
    }

    private void OverlayPositionChanged(
        object? sender,
        PixelPointEventArgs e)
    {
        // Don't allow the overlay to move independently.
        SyncOverlay();
    }

    // ============================================================
    // SYNCHRONIZE BOTH WINDOWS
    // ============================================================

    
    
    private void SyncWindows()
    {
        if (!_started)
            return;

        if (_surfaceWindow == null ||
            _overlayWindow == null)
            return;

        if (_updating)
            return;

        if (Bounds.Width <= 0 ||
            Bounds.Height <= 0)
            return;

        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel == null)
            return;

        var point = this.TranslatePoint(
            new Point(0, 0),
            topLevel);

        if (point == null)
            return;

        var screenPosition =
            topLevel.PointToScreen(point.Value);

        _updating = true;

        try
        {
            var position = new PixelPoint(
                (int)screenPosition.X,
                (int)screenPosition.Y);

            // Surface
            _surfaceWindow.Position = position;
            _surfaceWindow.Width = Bounds.Width;
            _surfaceWindow.Height = Bounds.Height;

            // Overlay
            _overlayWindow.Position = position;
            _overlayWindow.Width = Bounds.Width;
            _overlayWindow.Height = Bounds.Height;
        }
        finally
        {
            _updating = false;
        }
    }
    
    
    // private void SyncWindows()
    // {
    //     if (!_started)
    //         return;
    //
    //     if (_surfaceWindow == null ||
    //         _overlayWindow == null)
    //         return;
    //
    //     if (_updating)
    //         return;
    //
    //     if (Bounds.Width <= 0 ||
    //         Bounds.Height <= 0)
    //         return;
    //
    //     // var screenPosition =
    //     //     PointToScreen(new Point(0, 0));
    //     
    //     var topLevel = TopLevel.GetTopLevel(this);
    //
    //     if (topLevel == null)
    //         return;
    //
    //     var point = this.TranslatePoint(
    //         new Point(0, 0),
    //         topLevel);
    //
    //     if (point == null)
    //         return;
    //
    //     var screenPoint =
    //         topLevel.PointToScreen(point.Value);
    //
    //     _updating = true;
    //
    //     try
    //     {
    //         // ----------------------------------------------------
    //         // Surface
    //         // ----------------------------------------------------
    //
    //         // _surfaceWindow.Position =
    //         //     new PixelPoint(
    //         //         (int)Math.Round(screenPosition.X),
    //         //         (int)Math.Round(screenPosition.Y));
    //         
    //         
    //         _surfaceWindow.Position =
    //             new PixelPoint(
    //                 (int)screenPosition.X,
    //                 (int)screenPosition.Y);
    //
    //         _surfaceWindow.Width = Bounds.Width;
    //         _surfaceWindow.Height = Bounds.Height;
    //
    //         // ----------------------------------------------------
    //         // Overlay
    //         // ----------------------------------------------------
    //
    //         _overlayWindow.Position =
    //             _surfaceWindow.Position;
    //
    //         _overlayWindow.Width =
    //             _surfaceWindow.Width;
    //
    //         _overlayWindow.Height =
    //             _surfaceWindow.Height;
    //     }
    //     finally
    //     {
    //         _updating = false;
    //     }
    // }

    // ============================================================
    // SYNCHRONIZE ONLY OVERLAY
    // ============================================================

    private void SyncOverlay()
    {
        if (_surfaceWindow == null ||
            _overlayWindow == null)
            return;

        if (_updating)
            return;

        _updating = true;

        try
        {
            _overlayWindow.Position =
                _surfaceWindow.Position;

            _overlayWindow.Width =
                _surfaceWindow.Width;

            _overlayWindow.Height =
                _surfaceWindow.Height;
        }
        finally
        {
            _updating = false;
        }
    }

    // ============================================================
    // ATTACH / DETACH
    // ============================================================

    private void OnAttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        Dispatcher.UIThread.Post(
            ShowVideo,
            DispatcherPriority.Loaded);
    }

    private void OnDetachedFromVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        DestroyWindows();
    }

    // ============================================================
    // DESTROY
    // ============================================================

    private void DestroyWindows()
    {
        _started = false;

        if (_surfaceWindow != null)
        {
            _surfaceWindow.PositionChanged -=
                SurfacePositionChanged;

            _surfaceWindow.PropertyChanged -=
                SurfacePropertyChanged;

            _surfaceWindow.Close();

            _surfaceWindow = null;
        }

        if (_overlayWindow != null)
        {
            _overlayWindow.PositionChanged -=
                OverlayPositionChanged;

            _overlayWindow.Close();

            _overlayWindow = null;
        }
    }

    // ============================================================
    // VIDEO SURFACE WINDOW
    // ============================================================

    private class VideoSurfaceWindow : Window
    {
        public VideoSurfaceWindow()
        {
            Background = Brushes.Black;

            SystemDecorations =
                SystemDecorations.None;

            ShowInTaskbar = false;

            CanResize = false;

            ExtendClientAreaToDecorationsHint = true;

            ExtendClientAreaTitleBarHeightHint = 0;

            // ExtendClientAreaChromeHints =
            //     ExtendClientAreaChromeHints.NoChrome;
            //
            
            ExtendClientAreaChromeHints =
                Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;

            Content = new Border
            {
                Background = Brushes.Black
            };
        }

        public IntPtr Handle
        {
            get
            {
                return TryGetPlatformHandle()?
                           .Handle
                       ?? IntPtr.Zero;
            }
        }
    }

    // ============================================================
    // OVERLAY WINDOW
    // ============================================================

    private class VideoOverlayWindow : Window
    {
        public VideoOverlayWindow()
        {
            SystemDecorations =
                SystemDecorations.None;

            ShowInTaskbar = false;

            CanResize = false;

            Background = Brushes.Transparent;

            TransparencyLevelHint =
                new[]
                {
                    WindowTransparencyLevel.Transparent
                };

            ExtendClientAreaToDecorationsHint = true;

            ExtendClientAreaTitleBarHeightHint = 0;

            // ExtendClientAreaChromeHints =
            //     ExtendClientAreaChromeHints.NoChrome;
            
            ExtendClientAreaChromeHints =
                Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;

            
        }
    }

    // ============================================================
    // EXAMPLE OVERLAY CONTENT
    // ============================================================

}