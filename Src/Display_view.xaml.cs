using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Androidplayer.Src.Keymap;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Src.Mouse;
using Androidplayer.Src.Rawinput;
using Androidplayer.Store;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Drawing.Color;
using my_MouseEventArgs = System.Windows.Input.MouseEventArgs;
using UserControl = System.Windows.Controls.UserControl;


//
// using FlyleafLib;
// using FlyleafLib.Controls.WPF;
// using FlyleafLib.MediaPlayer;

namespace Androidplayer.Src;

public partial class Display_view : UserControl
{
    private App_manager my_app_manager;
    
    private Mouse_Locker my_mouse_locker;
    
    private Mouse_normal mouse_normal;


    // protected LogHandler Log;
    
    private DispatcherTimer _resizeTimer;
    
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    public Display_view()
    {
        InitializeComponent();
        
        // var setter = typeof(Engine)
        //     .GetProperty("Config")?
        //     .GetSetMethod(true);
        //
        // setter?.Invoke(null, new object[]
        // {
        //     new EngineConfig()
        // });
        
        
        
        // Surface.MouseDoubleClick -= Surface_MouseDoubleClick;
        // var prop = typeof(FlyleafHost)
        //     .GetProperty("Surface", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        //
        // prop?.SetValue(this, someWindowInstance);
        
        // MouseUp="MainImage_OnMouseUp"
        // MouseDown="MainImage_OnMouseDown"
        // MouseMove="MainImage_OnMouseMove"
        //     
        // MouseWheel="MainImage_OnMouseWheel"


        MainImage.Surface.MouseDown += MainImage_OnMouseDown;
        MainImage.Surface.MouseUp += MainImage_OnMouseUp;
        MainImage.Surface.MouseMove += MainImage_OnMouseMove;
        MainImage.Surface.MouseWheel += MainImage_OnMouseWheel;
        
        
        
        MainImage.Surface.DragEnter += MainImage_DragEnter;
        MainImage.Surface.Drop += MainImage_Drop;


        MainImage.Surface.Focusable = true;
        MainImage.Focusable = true;
        
        Uri cursorUri = new Uri("pack://application:,,,/Icons/cursor/black_sword.cur");
        Cursor customCursor = new Cursor(Application.GetResourceStream(cursorUri).Stream);


        MainImage.Surface.Cursor = customCursor;
        MainImage.Overlay.Cursor = customCursor;

        this.Cursor = customCursor;
        
        
        // k_info.Instance.overlayManager?.Canvas_OnMouseDown(sender, e);
    
       
        
        
        
        this.DataContext = k_info.Instance;
        
        
        k_info.Instance.ImageContainer = ImageContainer;


        k_info.Instance.overlayManager = new OverlayManager(ImageContainer, this);
        
        
        
        
        Loaded += OnLoaded;
        
        LayoutUpdated += OnLayoutUpdated;
        
        SizeChanged += OnSizeChanged;
        
        Unloaded += OnUnloaded;
        
        
        _resizeTimer = new DispatcherTimer();
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(200); // Adjust delay as needed
        _resizeTimer.Tick += ResizeTimer_Tick;
        
        
        
        
        // k_info.Instance.PropertyChanged += (s, e) =>
        // {
        //     if (e.PropertyName == nameof(k_info.KeymapMode))
        //     {
        //
        //         Console.WriteLine("called from display");
        //         k_info.Instance.KeymapMenu.Visibility = k_info.Instance.KeymapMode ? Visibility.Visible : Visibility.Collapsed;
        //     }
        // };
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // Console.WriteLine("dv layout updated");
    }

    private void MainImage_DragEnter(object sender, DragEventArgs e)
    {
       k_info.Instance.overlayManager?.Canvas_DragEnter(sender, e);
    }

    private void MainImage_Drop(object sender, DragEventArgs e)
    {
       k_info.Instance.overlayManager?.Canvas_Drop(sender, e);
      
        
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        MainImage?.Dispose();
    }


    private void ResizeTimer_Tick(object? sender, EventArgs e)
    {
        _resizeTimer.Stop(); // Stop the timer once the delay is met
        // Handle resize finished

        
        // windowsFormsHost.Visibility = Visibility.Visible;
        // windowsFormsHost.Background = new SolidColorBrush(Colors.Transparent);

        MainImage.Visibility = Visibility.Visible;


        // Console.WriteLine("finished resizing #####");
     
        
        var resetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        resetTimer.Tick += (s, args) =>
        {
            resetTimer.Stop();
            my_info.Instance.Window_resizing = false;
        };
        resetTimer.Start();
        
        // Dispatcher.BeginInvoke(() =>
        // {
        //     my_info.Instance.Window_resizing = false;
        // },
        // DispatcherPriority.Render);
        // my_info.Instance.Window_resizing = false;
            
       
    }


    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        
        int v_width = My_Store.Instance.VideoWidth;
                
        int v_height = My_Store.Instance.VideoHeight;

        if (v_height > 0 && v_width > 0)
        {
        
            ScaleFormToFit(v_width, v_height);
        }
       
        _resizeTimer.Stop(); // Reset the timer on each SizeChanged event
        _resizeTimer.Start();

        if (my_info.Instance.Window_resizing == false)
        {
            
            MainImage.Visibility = Visibility.Hidden;
            Console.WriteLine("hiding mian image");
        }
        //
        // if (MainImage.Visibility == Visibility.Visible)
        // {
        //     MainImage.Visibility = Visibility.Hidden;
        //     Console.WriteLine("hiding mian image");
        // }


        my_info.Instance.Window_resizing = true;


        // windowsFormsHost.Visibility = Visibility.Hidden;


        // winFormsForm.Visible = false;

        // winFormsForm.BackColor = Color.Black;
       
        // windowsFormsHost.Background = Brushes.Black;
        // winFormsForm.BackColor = Color.Black;
        // winFormsForm.Invalidate();
        //


    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        
        
        
        
        // winFormsForm.Width = 1080;
        // winFormsForm.Height = 488;
        
        // winFormsForm.Width = 1600;
        // winFormsForm.Height = 1600;


        // Console.WriteLine("display view 2");
        
        
        if (MainImage.Surface != null && MainImage.isSurfaceCreated)
        {
            Console.WriteLine(this.Visibility);
            Console.WriteLine("surface already created");
        }
      
        

      
        
        
        
        // 328x720   1080x488
        my_app_manager = new App_manager(MainImage);

        Console.WriteLine(MainImage.ActualWidth);

        My_Store.Instance.PropertyChanged += OnStorePropertyChanged;

        my_info.Instance.PropertyChanged += on_my_info_propertychanged;
        
        
        int v_width = My_Store.Instance.VideoWidth;
                
        int v_height = My_Store.Instance.VideoHeight;

        if (v_height > 0 && v_width > 0)
        {
        
            ScaleFormToFit(v_width, v_height);
        }

        // my_mouse_locker = new Mouse_Locker(MainImage);

        mouse_normal = new Mouse_normal();

        My_Store.Instance.SetDisplayResolution((int)MainImage.ActualWidth , (int)MainImage.ActualHeight);
        
        
        OverlayManager.Instance.rerender_overlay();


       
        
    }

    private void on_my_info_propertychanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // case nameof(My_Store.VideoWidth):
            case nameof(my_info.IsMouseLocked):
    
                if (!my_info.Instance.IsMouseLocked)
                {
                    
                    Dispatcher.Invoke(() =>
                    {
                        
                        var visual = KeyMapManager.Instance?.GetElement("Visual");
                        
                        if (visual != null)
                        {
    
                            // visual.ScaledHeight
                            //     
                            // var scaled_x = (int)visual.X;
                            // var scaled_y = (int)visual.Y;
                            
                            
                            double storedX = visual.X + (visual.ScaledWidth / 2);
                            double storedY = visual.Y + (visual.ScaledHeight / 2);
                            
                            double storedWidth = visual.ParentWidth;   // android/display width used when point was saved
                            double storedHeight = visual.ParentHeight; // android/display height used when point was saved
    
                            // Defensive: avoid division by zero
                            if (storedWidth <= 0 || storedHeight <= 0 || MainImage == null)
                            {
                                return;
                            }
    
                            // Current displayed UI size of the image (keeps same aspect ratio as Android)
                            double uiWidth = MainImage.ActualWidth;
                            double uiHeight = MainImage.ActualHeight;

                            Console.WriteLine($"image size : {uiWidth}, {uiHeight}");
    
                            // Scale from stored (android) coordinates -> current UI coordinates
                            double scaleX = uiWidth / storedWidth;
                            double scaleY = uiHeight / storedHeight;
    
                            double uiX = storedX * scaleX;
                            double uiY = storedY * scaleY;
                            
                            Console.WriteLine($"device xy : {storedX}, {storedY}");
                            
                            Console.WriteLine($"scaled xy : {uiX}, {uiY}");
    
                            // Clamp to image bounds to be safe
                            uiX = Math.Clamp(uiX, 0, Math.Max(0, uiWidth - 1));
                            uiY = Math.Clamp(uiY, 0, Math.Max(0, uiHeight - 1));
    
                            var relativePoint = new System.Windows.Point(uiX, uiY);
                            var screenPoint = MainImage.PointToScreen(relativePoint);
    
                            // Move cursor using your mouse locker (expects coordinates relative to MainImage or screen depending on implementation)
                            // If my_mouse_locker.MoveCursor expects image-relative coords:
                            // my_mouse_locker.MoveCursor(uiX, uiY);
    
                            SetCursorPos((int)screenPoint.X, (int)screenPoint.Y);
                            
                        }
    
                        
                        // ScaleFormToFit(v_width, v_height);
                        // scale_mainwindow();
                    });
                }
    
              
    
                
                    
    
    
    
                
    
                break;
    
            
            
            // case nameof(my_info.TakeScreenshot):
            //
            //
            //     Console.WriteLine("zzzzzzzzzzz");
            //     Console.WriteLine($"mainimage width {MainImage.Surface.Width}");
            //     if (my_info.Instance.TakeScreenshot)
            //     {
            //
            //
            //     }
            //     
            //         break;
            
        }
    }


    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // case nameof(My_Store.VideoWidth):
            case nameof(My_Store.VideoResolution):

                int v_width = My_Store.Instance.VideoWidth;
                
                int v_height = My_Store.Instance.VideoHeight;

              

                if (v_height > 0 && v_width > 0 && v_width != v_height )
                {
                    Console.WriteLine($"Video resolution changed: {v_width}x{v_height}");
                    
                    

                    Dispatcher.Invoke(() =>
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

                    // Console.WriteLine(My_Store.Instance.DeviceWidth);

                }
    
                break;
 
        }
    }
    
    private  void scale_mainwindow()
    {
        // if (!my_info.Instance.Auto_resizing)
        // {
        //     return;
        // }
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        
        
        double videoWidth = My_Store.Instance.VideoWidth;
        double videoHeight = My_Store.Instance.VideoHeight;

        const double scaleFactor = 0.9;
        double aspect = videoWidth / videoHeight;

        double targetWidth, targetHeight;

        // if (videoWidth > videoHeight)
        // {
        //     targetWidth = screenWidth * scaleFactor;
        //     targetHeight = targetWidth / aspect;
        // }
        // else
        // {
        //     targetHeight = screenHeight * scaleFactor;
        //     targetWidth = targetHeight * aspect;
        // }
        
        if (My_Store.Instance.VideoWidth > My_Store.Instance.VideoHeight)
        {
            targetWidth = screenWidth * scaleFactor;
            targetHeight = targetWidth / aspect;

            // Prevent it from being taller than screen
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

            // Prevent it from being wider than screen
            if (targetWidth > screenWidth * scaleFactor)
            {
                targetWidth = screenWidth * scaleFactor;
                targetHeight = targetWidth / aspect;
            }
        }

        Home.Instance.Width = targetWidth;
        
        Home.Instance.Height = targetHeight + 30;

        
        Home.Instance.UpdateLayout();
        
        ScaleFormToFit((int)videoWidth, (int)videoHeight);
        
        Home.Instance.Left = (screenWidth - targetWidth) / 2;
        Home.Instance.Top = (screenHeight - targetHeight) / 2;
        
        my_info.Instance.Auto_resizing = false;
        
        OverlayManager.Instance?.rerender_overlay();
        
    }


    public void ScaleFormToFit(int videoWidth, int videoHeight)
    {

        // Console.WriteLine("resize image....");
       
        double availableWidth = this.ActualWidth;
        double availableHeight = this.ActualHeight;

        System.Windows.Size frame_size = new System.Windows.Size(this.ActualWidth, this.ActualHeight);
        
        double original_width = videoWidth;
        double original_height = videoHeight;
        double aspect_ratio = original_width / original_height;

        int new_width, new_height;

        // Calculate the new size based on the aspect ratio
        if (frame_size.Width / frame_size.Height > aspect_ratio)
        {
            // Container is wider relative to video aspect ratio - fit to height
            new_height = (int)frame_size.Height;
            new_width = (int)(new_height * aspect_ratio);
            // Console.WriteLine("portrait mode");
        }
        else
        {
            // Container is taller relative to video aspect ratio - fit to width
            new_width = (int)frame_size.Width;
            new_height = (int)(new_width / aspect_ratio);
            // Console.WriteLine("landscape mode");
        }

        // Update the form size
        // winFormsForm.Width = new_width;
        // winFormsForm.Height = new_height;
        // Console.WriteLine($"main size : {new_width},{new_height}");
        
        ImageContainer.Width = new_width;
        
        ImageContainer.Height = new_height;


        MainImage.Width = new_width;
        MainImage.Height = new_height;
        
        ModeOverlay.Width = new_width;
        ModeOverlay.Height = new_height;
        
        // ToastMessage.Width = new_width;
        // ToastMessage.Height = new_height;
        
        // MainImage.Surface.Width = new_width;
        // MainImage.Surface.Height = new_height;
        //
        // MainImage.Overlay.Width = new_width;
        // MainImage.Overlay.Height = new_height;

        // Console.WriteLine($"container width: ");
        
        
        
        My_Store.Instance.SetDisplayResolution((int)MainImage.ActualWidth , (int)MainImage.ActualHeight);

    }

    private void MainImage_OnMouseDown(object? sender, my_MouseEventArgs e)
    {
        // MainImage.CaptureMouse();
        
        
        var pos = e.GetPosition(MainImage.Surface);
        // var pos = e.GetPosition(ImageContainer);
        
        double x = pos.X;
        double y = pos.Y;

        // Console.WriteLine($"{x} : {y}");

        // Console.WriteLine($" {MainImage.ActualHeight} printing from display view");
        
        if (!my_info.Instance.IsMouseLocked && !k_info.Instance.KeymapMode)
        {
            // Console.WriteLine($"{x} , {y}");
            
            
            
            
            
            mouse_normal.OnMouseDown(e, MainImage);
            // mouse_normal.OnMouseDown(e, sender);
                
        }

        if (k_info.Instance.KeymapMode)
        {
            k_info.Instance.overlayManager?.Canvas_OnMouseDown(sender, e);
        }
        
        
    }

    private void MainImage_OnMouseUp(object? sender, my_MouseEventArgs e)
    {
        
        MainImage.ReleaseMouseCapture();
        if (!my_info.Instance.IsMouseLocked && !k_info.Instance.KeymapMode)
        {
            mouse_normal.OnMouseUp(e, MainImage);
                
        }
    }

    private void MainImage_OnMouseMove(object? sender, my_MouseEventArgs e)
    {
        if (!my_info.Instance.IsMouseLocked && !k_info.Instance.KeymapMode)
        {
            mouse_normal.mouse_Move(e, MainImage);
                
        }
    }


    private void MainImage_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!my_info.Instance.IsMouseLocked && !k_info.Instance.KeymapMode)
        {
            mouse_normal.mouse_Wheel(e, MainImage);
                
        }
    }
}



// adb push scrcpy-server-3.jar /data/local/tmp/scrcpy-server-manual.jar

// adb shell CLASSPATH=/data/local/tmp/scrcpy-server-manual.jar app_process / com.genymobile.scrcpy.Server 3.3.2 tunnel_forward=true audio=false control=false cleanup=false max_size=1080