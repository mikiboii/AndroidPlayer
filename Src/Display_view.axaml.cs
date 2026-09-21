using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
// using Androidplayer.Src.Controls;
using Androidplayer.Src.Keymap;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Src.Mouse;
using Androidplayer.Src.Rawinput;
using Androidplayer.Store;
using Brushes = Avalonia.Media.Brushes;
using Point = Avalonia.Point;
using Size = Avalonia.Size;

namespace Androidplayer.Src;

public partial class Display_view : UserControl
{
    private App_manager my_app_manager;
    private Mouse_Locker my_mouse_locker;
    private Mouse_normal mouse_normal;

    private DispatcherTimer _resizeTimer;

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    public Display_view()
    {
        InitializeComponent();

        // if (MainImage != null)
        // {
        //     
        //     // Wire up surface events — using the Avalonia host's Surface control
        //
        //     if (ImageContainer != null)
        //     {
        //
        //         Console.WriteLine("serring mainimage events");
        //         
        //         ImageContainer.PointerPressed += ImageContainer_OnMouseDown;
        //         ImageContainer.PointerReleased += ImageContainer_OnMouseUp;
        //         ImageContainer.PointerMoved += ImageContainer_OnMouseMove;
        //         ImageContainer.PointerWheelChanged += ImageContainer_OnMouseWheel;
        //
        //         ImageContainer.AddHandler(DragDrop.DragEnterEvent, ImageContainer_DragEnter);
        //         ImageContainer.AddHandler(DragDrop.DropEvent, ImageContainer_Drop);
        //
        //         ImageContainer.Focusable = true;
        //         MainImage.Focusable = true;
        //     }
        //     ImageContainer.Focusable = true;
        //     MainImage.Focusable = true;
        //     
        // }

        
        ModeOverlay.ZIndex = 999;
        
        
        // Custom cursor via avares:// URI
        // var cursorUri = new Uri("avares://Androidplayer/Icons/cursor/black_sword.cur");
        // using var cursorStream = AssetLoader.Open(cursorUri);
        // Cursor customCursor = new Cursor(cursorStream);
        
        // 1. Point to your converted PNG resource
        var cursorUri = new Uri("avares://Androidplayer/Icons/cursor/black_sword.png");

// 2. Open the asset stream
        using var cursorStream = AssetLoader.Open(cursorUri);

// 3. Load the image into an Avalonia Bitmap
        var cursorBitmap = new Avalonia.Media.Imaging.Bitmap(cursorStream);

// 4. Set the Hotspot (X, Y in pixels). 
// For a sword tip, it's typically the top-left corner (0, 0)
        var hotSpot = new PixelPoint(0, 0);

// 5. Instantiate the cursor correctly
        Cursor customCursor = new Cursor(cursorBitmap, hotSpot);


        if (MainImage != null)
        {
        MainImage.Cursor = customCursor;
            
        }
        
        // if (MainImage.Overlay != null)
        // {
        // MainImage.Overlay.Cursor = customCursor;
        //     
        // }
        
        

        this.Cursor = customCursor;
        
        
        
        
        
        

        this.DataContext = k_info.Instance;

        k_info.Instance.ImageContainer = ImageContainer;
        k_info.Instance.overlayManager = new OverlayManager(ImageContainer, this);

        Loaded += OnLoaded;
        
        // MainImage.HandleCreated += testOnHandleCreated;
        
        MainImage.HandleCreated += MainImageOnHandleCreated;
        
        MainImage.SizeChanged += MainImageOnSizeChanged;
        
        // SizeChanged += OnSizeChanged;
        
        
        Unloaded += OnUnloaded;

        _resizeTimer = new DispatcherTimer();
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(200);
        _resizeTimer.Tick += ResizeTimer_Tick;
    }

    private void MainImageOnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        int v_width = My_Store.Instance.VideoWidth;
        int v_height = My_Store.Instance.VideoHeight;

        if (v_height > 0 && v_width > 0)
        {
            ScaleFormToFit(v_width, v_height);
        }

        

        // MainImage.IsVisible = false;
        
        // k_info.Instance.directx?.ResizeSwapChain(
        //     (int)MainImage.Bounds.Width,
        //     (int)MainImage.Bounds.Height);
        //                     
                            
        
        // k_info.Instance.directx?.ResizeToClient(MainImage.NativeHandle);
        //
        // if (my_info.Instance.DeveloperMode)
        // {
        //     k_info.Instance.directx?.HandleResize();
        // }
        
        
        
        var dx = k_info.Instance.directx;
        if (dx != null)
        {
            // Take the SAME lock the decoder uses, so resize and decode
            // cannot touch the D3D11 context at the same time.
            dx.RunOnContext(_ =>
            {
                dx.ResizeToClient(MainImage.NativeHandle);

                // Console.WriteLine("resizing directx handle");

                if (my_info.Instance.DeveloperMode)
                {
                    dx.HandleResize();
                }
            });
        }
        
        
        _resizeTimer.Stop();
        _resizeTimer.Start();
        
    }

    private void testOnHandleCreated(object? sender, IPlatformHandle e)
    {

        Console.WriteLine($" main image size {MainImage.Bounds.Width } x {MainImage.Bounds.Height}");
            
        
        
        
        
        
    }




    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        My_Store.Instance.PropertyChanged += OnStorePropertyChanged;
        my_info.Instance.PropertyChanged += on_my_info_propertychanged;
    
        my_mouse_locker = new Mouse_Locker(ImageContainer);
        mouse_normal = new Mouse_normal();
    
        Console.WriteLine("display_view file loaded");

        // Console.WriteLine(Width);
        // MainImage.Width = Width;
        // MainImage.Height = Height;
        
        
        // Dispatcher.UIThread.Post(() =>
        // {
        //     if (Home.Instance != null)
        //     {
        //         Home.Instance.loadingpage.IsVisible = false;
        //         Home.Instance.displayView.IsVisible = true;
        //     }
        //
        //
        //
        //     Console.WriteLine("displayview ready ##################");
        //
        //     // OverlayManager.Instance?.rerender_overlay();
        // });
        
        
        // var timer = new DispatcherTimer
        // {
        //     Interval = TimeSpan.FromSeconds(5)
        // };
        // timer.Tick += (s, args) =>
        // {
        //     timer.Stop();   // one-shot
        //
        //     if (Home.Instance != null)
        //     {
        //         Home.Instance.loadingpage.IsVisible = false;
        //         Home.Instance.displayView.IsVisible = true;
        //     }
        //
        //     Console.WriteLine("displayview ready ##################");
        //
        //     OverlayManager.Instance?.rerender_overlay();
        // };
        // timer.Start();
    
        
        
        
        
        
        // if (MainImage != null && MainImage.isSurfaceCreated)
        // {
        //     InitAppManager();   // safe now
        // }
        
        
        
        // else: do NOT construct App_manager here — wait for HandleCreated
    }
    
    private void MainImageOnHandleCreated(object? sender, IPlatformHandle e)
    {
        Console.WriteLine("handle created in display view");
        
        
        
        
        
        
        if (MainImage != null)
        {
            // MainImage._floatingContent.Background = Brushes.Transparent;
            // Wire up surface events — using the Avalonia host's Surface control

            if (ImageContainer != null)
            {

                Console.WriteLine("serring mainimage events");
                
                ImageContainer.PointerPressed += ImageContainer_OnMouseDown;
                ImageContainer.PointerReleased += ImageContainer_OnMouseUp;
                ImageContainer.PointerMoved += ImageContainer_OnMouseMove;
                ImageContainer.PointerWheelChanged += ImageContainer_OnMouseWheel;

                ImageContainer.AddHandler(DragDrop.DragEnterEvent, ImageContainer_DragEnter);
                ImageContainer.AddHandler(DragDrop.DropEvent, ImageContainer_Drop);

                MainImage.Focusable = true;
                ImageContainer.Focusable = true;
            }
            ImageContainer.Focusable = true;
            MainImage.Focusable = true;
            
        }
        
        InitAppManager();
    }
    
    private void InitAppManager()
    {
        if (my_app_manager != null) return; // avoid double-init
    
        my_app_manager = new App_manager(MainImage);
    
        int v_width = My_Store.Instance.VideoWidth;
        int v_height = My_Store.Instance.VideoHeight;
        if (v_height > 0 && v_width > 0)
            ScaleFormToFit(v_width, v_height);
    
        My_Store.Instance.SetDisplayResolution(
            (int)ImageContainer.Bounds.Width,
            (int)ImageContainer.Bounds.Height);
    
        
        // MainImage.
        // OverlayManager.Instance?.rerender_overlay();
    }

    private void ImageContainer_DragEnter(object? sender, DragEventArgs e)
    {
        k_info.Instance.overlayManager?.Canvas_DragEnter(sender, e);
    }

    private void ImageContainer_Drop(object? sender, DragEventArgs e)
    {
        k_info.Instance.overlayManager?.Canvas_Drop(sender, e);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        
        
        
        MainImage?.Dispose();
    }

    private void ResizeTimer_Tick(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();

        MainImage.IsVisible = true;

        var resetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        resetTimer.Tick += (s, args) =>
        {
            resetTimer.Stop();
            // my_info.Instance.Window_resizing = false;
            
            //
            // k_info.Instance.directx?.ResizeSwapChain(
            //     (int)MainImage.Bounds.Width,
            //     (int)MainImage.Bounds.Height);
            //                 
            //         
            
            // k_info.Instance.directx?.ResizeToClient(MainImage.NativeHandle);
            //
            // if (my_info.Instance.DeveloperMode)
            // {
            //     k_info.Instance.directx?.HandleResize();
            // }
            
            
            
            // MainImage._floatingContent.Background = Brushes.Transparent;
            
            
            var dx = k_info.Instance.directx;
            if (dx != null)
            {
                // Take the SAME lock the decoder uses, so resize and decode
                // cannot touch the D3D11 context at the same time.
                dx.RunOnContext(_ =>
                {
                    dx.ResizeToClient(MainImage.NativeHandle);

                    if (my_info.Instance.DeveloperMode)
                    {
                        dx.HandleResize();
                    }
                });
            }

            
            
             // Console.WriteLine($" Display view _resizeTimer  {MainImage.Bounds.Width}, {MainImage.Bounds.Height}");

        };
        resetTimer.Start();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        int v_width = My_Store.Instance.VideoWidth;
        int v_height = My_Store.Instance.VideoHeight;

        if (v_height > 0 && v_width > 0)
        {
            ScaleFormToFit(v_width, v_height);
        }

        _resizeTimer.Stop();
        _resizeTimer.Start();

        if (my_info.Instance.Window_resizing == false)
        {
            // WPF had Visibility.Hidden — Avalonia has only IsVisible = false
            MainImage.IsVisible= false;
            // Console.WriteLine("hiding mian image");
        }

        my_info.Instance.Window_resizing = true;
    }

    // private void OnLoaded(object? sender, RoutedEventArgs e)
    // {
    //     if (MainImage != null && MainImage.isSurfaceCreated)
    //     {
    //         Console.WriteLine(this.IsVisible);
    //         Console.WriteLine("surface already created");
    //     }
    //
    //     my_app_manager = new App_manager(MainImage);
    //
    //     Console.WriteLine(MainImage.Bounds.Width);
    //
    //     My_Store.Instance.PropertyChanged += OnStorePropertyChanged;
    //     my_info.Instance.PropertyChanged += on_my_info_propertychanged;
    //
    //     int v_width = My_Store.Instance.VideoWidth;
    //     int v_height = My_Store.Instance.VideoHeight;
    //
    //     if (v_height > 0 && v_width > 0)
    //     {
    //         ScaleFormToFit(v_width, v_height);
    //     }
    //
    //     my_mouse_locker = new Mouse_Locker(MainImage);
    //     mouse_normal = new Mouse_normal();
    //
    //     My_Store.Instance.SetDisplayResolution(
    //         (int)MainImage.Bounds.Width,
    //         (int)MainImage.Bounds.Height);
    //
    //     OverlayManager.Instance?.rerender_overlay();
    // }
    //
    
   
    private void on_my_info_propertychanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(my_info.IsMouseLocked):

                if (!my_info.Instance.IsMouseLocked)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var visual = KeyMapManager.Instance?.GetElement("Visual");

                        if (visual != null)
                        {
                            double storedX = visual.X + (visual.ScaledWidth / 2);
                            double storedY = visual.Y + (visual.ScaledHeight / 2);

                            double storedWidth = visual.ParentWidth;
                            double storedHeight = visual.ParentHeight;

                            if (storedWidth <= 0 || storedHeight <= 0 || ImageContainer == null)
                            {
                                return;
                            }

                            double uiWidth = ImageContainer.Bounds.Width;
                            double uiHeight = ImageContainer.Bounds.Height;

                            Console.WriteLine($"image size : {uiWidth}, {uiHeight}");

                            double scaleX = uiWidth / storedWidth;
                            double scaleY = uiHeight / storedHeight;

                            double uiX = storedX * scaleX;
                            double uiY = storedY * scaleY;

                            Console.WriteLine($"device xy : {storedX}, {storedY}");
                            Console.WriteLine($"scaled xy : {uiX}, {uiY}");

                            uiX = Math.Clamp(uiX, 0, Math.Max(0, uiWidth - 1));
                            uiY = Math.Clamp(uiY, 0, Math.Max(0, uiHeight - 1));

                            var relativePoint = new Point(uiX, uiY);
                            var screenPoint = ImageContainer.PointToScreen(relativePoint);

                            SetCursorPos((int)screenPoint.X, (int)screenPoint.Y);
                        }
                    });
                }

                break;
        }
    }

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(My_Store.VideoResolution):

                int v_width = My_Store.Instance.VideoWidth;
                int v_height = My_Store.Instance.VideoHeight;

                if (v_height > 0 && v_width > 0 && v_width != v_height)
                {
                    Console.WriteLine($"Video resolution changed: {v_width}x{v_height}");

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (My_Store.Instance.VideoWidth > My_Store.Instance.VideoHeight)
                        {
                            my_info.Instance.IsLandscapemode = true;
                        }
                        else
                        {
                            my_info.Instance.IsLandscapemode = false;
                        }
                        ScaleFormToFit(v_width, v_height);
                        scale_mainwindow();
                    });
                }

                break;
        }
    }

    private void scale_mainwindow()
    {
        // var screen = Screens.Primary;
        var screen = TopLevel.GetTopLevel(this)?.Screens.Primary;
        if (screen == null) return;

        double screenWidth = screen.Bounds.Width / screen.Scaling;
        double screenHeight = screen.Bounds.Height / screen.Scaling;

        double videoWidth = My_Store.Instance.VideoWidth;
        double videoHeight = My_Store.Instance.VideoHeight;

        const double scaleFactor = 0.9;
        double aspect = videoWidth / videoHeight;

        double targetWidth, targetHeight;

        if (My_Store.Instance.VideoWidth > My_Store.Instance.VideoHeight)
        {
            targetWidth = screenWidth * scaleFactor;
            targetHeight = targetWidth / aspect;

            if (targetHeight > screenHeight * scaleFactor)
            {
                targetHeight = screenHeight * scaleFactor;
                targetWidth = targetHeight * aspect;
            }
        }
        else
        {
            targetHeight = screenHeight * scaleFactor;
            targetWidth = targetHeight * aspect;

            if (targetWidth > screenWidth * scaleFactor)
            {
                targetWidth = screenWidth * scaleFactor;
                targetHeight = targetWidth / aspect;
            }
        }

        if (Home.Instance != null)
        {
            Home.Instance.Width = targetWidth;
            Home.Instance.Height = targetHeight + 30;

            // Avalonia: Window.Position (PixelPoint) instead of Left/Top
            Home.Instance.Position = new PixelPoint(
                (int)((screenWidth - targetWidth) / 2),
                (int)((screenHeight - targetHeight) / 2));
        }

        ScaleFormToFit((int)videoWidth, (int)videoHeight);

        my_info.Instance.Auto_resizing = false;

        OverlayManager.Instance?.rerender_overlay();
    }

    // public void ScaleFormToFit(int videoWidth, int videoHeight)
    // {
    //     // double availableWidth = this.Bounds.Width;
    //     // double availableHeight = this.Bounds.Height;
    //
    //     var margin = MainImages_parent.Margin;
    //     
    //     double availableWidth  = this.Bounds.Width  - margin.Left - margin.Right;
    //     double availableHeight = this.Bounds.Height - margin.Top  - margin.Bottom;
    //
    //     var frame_size = new Size(this.Bounds.Width, this.Bounds.Height);
    //
    //     double original_width = videoWidth;
    //     double original_height = videoHeight;
    //     double aspect_ratio = original_width / original_height;
    //
    //     int new_width, new_height;
    //
    //     if (frame_size.Width / frame_size.Height > aspect_ratio)
    //     {
    //         new_height = (int)frame_size.Height;
    //         new_width = (int)(new_height * aspect_ratio);
    //     }
    //     else
    //     {
    //         new_width = (int)frame_size.Width;
    //         new_height = (int)(new_width / aspect_ratio);
    //     }
    //
    //     ImageContainer.Width = new_width;
    //     ImageContainer.Height = new_height;
    //
    //     MainImage.Width = new_width;
    //     MainImage.Height = new_height;
    //
    //     ModeOverlay.Width = new_width;
    //     ModeOverlay.Height = new_height;
    //
    //     My_Store.Instance.SetDisplayResolution(
    //         (int)MainImage.Bounds.Width,
    //         (int)MainImage.Bounds.Height);
    // }
    
    
    public void ScaleFormToFit(int videoWidth, int videoHeight)
    {
        // Available area = this UserControl's bounds, minus the margins
        // that the layout system will apply around MainImage.
        var margin = MainImages_parent.Margin;
        // Console.WriteLine($" main image size {MainImage.Bounds.Width } x {MainImage.Bounds.Height}");

        // double availableWidth  = this.Bounds.Width  - margin.Left - margin.Right;
        // double availableHeight = this.Bounds.Height - margin.Top  - margin.Bottom;
        
        
        double availableWidth  = MainImage.Bounds.Width ;
        double availableHeight = MainImage.Bounds.Height ;

        // Console.WriteLine($" from scaletofit {availableWidth} x {availableHeight}");

        if (availableWidth <= 0 || availableHeight <= 0)
            return;

        double aspect_ratio = (double)videoWidth / videoHeight;

        int new_width, new_height;

        if (availableWidth / availableHeight > aspect_ratio)
        {
            new_height = (int)availableHeight;
            new_width  = (int)(new_height * aspect_ratio);
        }
        else
        {
            new_width  = (int)availableWidth;
            new_height = (int)(new_width / aspect_ratio);
        }

        
        
        
        double scale = Math.Min(
            availableWidth / videoWidth,
            (availableHeight - 30) / videoHeight
        );

        double displayWidth = videoWidth * scale;
        double displayHeight = videoHeight * scale;
        
        
        
        ImageContainer.Width  = displayWidth;
        ImageContainer.Height = displayHeight;

        // Console.WriteLine($"imagecontainer scaled {new_width}x{new_height}");
        
        // MainImage.Width  = new_width;
        // MainImage.Height = new_height;

        ModeOverlay.Width  = new_width;
        ModeOverlay.Height = new_height;

        My_Store.Instance.SetDisplayResolution(
            (int)new_width,
            (int)new_height);
        //
        // Console.WriteLine($"[SFT] set ImageContainer={new_width}x{new_height}, " +
        //                   $"ModeOverlay={new_width}x{new_height}, " +
        //                   $"video={videoWidth}x{videoHeight}, " +
        //                   $"avail={availableWidth}x{availableHeight}");
        // Console.WriteLine($" main image size {MainImage.Width } x {MainImage.Height}");
    }

    private void ImageContainer_OnMouseDown(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(ImageContainer);

        Console.WriteLine("clicking image");
        
        
        
        
        // int v_width = My_Store.Instance.VideoWidth;
        // int v_height = My_Store.Instance.VideoHeight;
        //
        // if (v_height > 0 && v_width > 0)
        // {
        //     ScaleFormToFit(v_width, v_height);
        // }
        
        
        // k_info.Instance.directx?.ResizeSwapChain(
        //     (int)MainImage.Bounds.Width,
        //     (int)MainImage.Bounds.Height);
        //                     
        //                     
        //
        // if (my_info.Instance.DeveloperMode)
        // {
        //     k_info.Instance.directx?.HandleResize();
        // }
        
        

        double x = pos.X;
        double y = pos.Y;

        if (!my_info.Instance.IsMouseLocked && !k_info.Instance.KeymapMode)
        {
            mouse_normal.OnMouseDown(e, ImageContainer);
        }

        if (k_info.Instance.KeymapMode)
        {
            k_info.Instance.overlayManager?.Canvas_OnMouseDown(sender, e);
        }
    }

    private void ImageContainer_OnMouseUp(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        if (!my_info.Instance.IsMouseLocked && !k_info.Instance.KeymapMode)
        {
            mouse_normal.OnMouseUp(e, ImageContainer);
        }
    }

    private void ImageContainer_OnMouseMove(object? sender, PointerEventArgs e)
    {
        if (!my_info.Instance.IsMouseLocked && !k_info.Instance.KeymapMode)
        {
            mouse_normal.mouse_Move(e, ImageContainer);
        }
    }

    private void ImageContainer_OnMouseWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!my_info.Instance.IsMouseLocked && !k_info.Instance.KeymapMode)
        {
            mouse_normal.mouse_Wheel(e, ImageContainer);
        }
    }
}